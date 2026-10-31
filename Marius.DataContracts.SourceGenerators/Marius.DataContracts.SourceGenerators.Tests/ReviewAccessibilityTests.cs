using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Xunit;

namespace Marius.DataContracts.SourceGenerators.Tests;

public class ReviewAccessibilityTests
{
    [Theory]
    [InlineData("[DataContract] internal class Model { [DataMember] public int Value; }")]
    [InlineData("[DataContract] internal struct Model { [DataMember] public int Value; }")]
    [InlineData("[DataContract] internal enum Model { [EnumMember] One }")]
    [InlineData("internal class Outer { [DataContract] public class Model { [DataMember] public int Value; } }")]
    [InlineData("[DataContract] internal class Model { [DataMember] public System.Collections.Generic.List<Model> Values; [DataMember] public Model[] Array; }")]
    [InlineData("[DataContract] internal class Root { [DataMember] public Bag Values; } [CollectionDataContract] internal class Bag : System.Collections.Generic.List<int> { }")]
    [InlineData("""
        [DataContract] internal class Root { [DataMember] public XmlValue Value; }
        internal class XmlValue : System.Xml.Serialization.IXmlSerializable {
            public System.Xml.Schema.XmlSchema GetSchema() => null;
            public void ReadXml(System.Xml.XmlReader reader) { reader.Skip(); }
            public void WriteXml(System.Xml.XmlWriter writer) { }
        }
        """)]
    public void InternalContracts_ExposeOnlyAccessibleGeneratedFields(string contracts)
    {
        var (output, _) = ReviewGeneratorDriver.Generate("using System.Runtime.Serialization;\n" + contracts);
        ReviewGeneratorDriver.AssertCompiles(output);
        var fields = output.GetTypeByMetadataName("Marius.DataContracts.Runtime.DataContractContext")!
            .GetMembers().OfType<IFieldSymbol>()
            .Where(field => field.Type is INamedTypeSymbol { IsGenericType: true, Name: not "FrozenDictionary" })
            .ToArray();
        Assert.Contains(fields, field => field.DeclaredAccessibility == Accessibility.Internal);
        Assert.Contains(fields, field => field.Name == "Int32" && field.DeclaredAccessibility == Accessibility.Public);
    }

    [Fact]
    public Task InternalClassEnumAndCollections_InteroperateWithDotNet() => ReviewRegression.AssertAsync(
        """
        [DataContract] internal enum State { [EnumMember] Ready = 1 }
        [DataContract] internal class Item { [DataMember] public int Value; }
        [DataContract] internal class Root {
            [DataMember] public State State;
            [DataMember] public List<Item> Items;
            [DataMember] public Item[] Array;
        }
        """,
        """
        return ReviewOracle.Parity(new Root { State = State.Ready, Items = new List<Item> { new Item { Value = 11 } },
            Array = new[] { new Item { Value = 22 } } }, value => {
                ReviewOracle.Check(value.State == State.Ready, "Internal enum");
                ReviewOracle.Check(value.Items.Count == 1 && value.Items[0].Value == 11, "Internal generic collection");
                ReviewOracle.Check(value.Array.Length == 1 && value.Array[0].Value == 22, "Internal array");
            });
        """);
}
