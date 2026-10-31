using System.Threading.Tasks;
using Xunit;

namespace Marius.DataContracts.SourceGenerators.Tests;

public class UnsupportedSettingsRegressionTests
{
    [Fact]
    public async Task CustomResolverIsRejectedInsteadOfSilentlyIgnored()
    {
        var result = await new SerializationTestCompiler().CompileAndRunAsync(
            """
            [DataContract] public class Root { [DataMember] public int Value { get; set; } }
            public class Resolver : DataContractResolver
            {
                public override bool TryResolveType(Type type, Type declaredType, DataContractResolver knownTypeResolver, out XmlDictionaryString typeName, out XmlDictionaryString typeNamespace)
                {
                    typeName = null;
                    typeNamespace = null;
                    return false;
                }
                
                public override Type ResolveName(string name, string ns, Type declaredType, DataContractResolver knownTypeResolver) => null;
            }
            """,
            """
            try
            {
                _ = new Marius.DataContracts.Runtime.DataContractSerializer(DataContractContext.DataContractProvider, typeof(Root), new System.Runtime.Serialization.DataContractSerializerSettings { DataContractResolver = new Resolver() });
                return new SerializationTestResultData { Success = true, DeserializedEqual = false };
            }
            catch (NotSupportedException ex)
            {
                return new SerializationTestResultData { Success = true, DeserializedEqual = ex.Message.Contains("DataContractResolver") };
            }
            """);
        Assert.True(result.Success, result.ErrorMessage);
        Assert.True(result.DeserializedEqual, "An explicitly supplied custom resolver must be rejected clearly.");
    }
}
