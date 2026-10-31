using System.Threading.Tasks;
using Xunit;

namespace Marius.DataContracts.SourceGenerators.Tests;

public class TypedDispatchSubclassRegressionTests
{
    [Fact]
    public async Task TypedValues_RespectCustomContextObjectDispatchOverrides()
    {
        const string contracts =
            """
            [DataContract(Namespace = "urn:override")]
            public struct TypedPoint
            {
                [DataMember] public int Number;
            }

            public sealed class InternalOverride : Marius.DataContracts.Runtime.XmlObjectSerializerReadContext
            {
                public InternalOverride(Marius.DataContracts.Runtime.DataContractSerializer serializer, Marius.DataContracts.Runtime.DataContract contract) : base(serializer, contract) { }
                public override object? InternalDeserialize(Marius.DataContracts.Runtime.XmlReaderDelegator reader, Type type, Marius.DataContracts.Runtime.DataContract? contract, string? name, string? ns) => new TypedPoint { Number = 91 };
            }

            public sealed class ValueOverride : Marius.DataContracts.Runtime.XmlObjectSerializerReadContext
            {
                public ValueOverride(Marius.DataContracts.Runtime.DataContractSerializer serializer, Marius.DataContracts.Runtime.DataContract contract) : base(serializer, contract) { }
                protected override object? ReadDataContractValue(Marius.DataContracts.Runtime.DataContract contract, Marius.DataContracts.Runtime.XmlReaderDelegator reader) => new TypedPoint { Number = 92 };
            }

            public sealed class ComplexOverride : Marius.DataContracts.Runtime.XmlObjectSerializerReadContextComplex
            {
                public ComplexOverride(Marius.DataContracts.Runtime.DataContractSerializer serializer, Marius.DataContracts.Runtime.DataContract contract) : base(serializer, contract) { }
                public override object? InternalDeserialize(Marius.DataContracts.Runtime.XmlReaderDelegator reader, Type type,
                    Marius.DataContracts.Runtime.DataContract? contract, string? name, string? ns) => new TypedPoint { Number = 93 };
            }
            """;

        const string assertions =
            """
            Marius.DataContracts.Runtime.IDataContractProvider provider = Marius.DataContracts.Runtime.DataContractContext.DataContractProvider;
            var contract = provider.GetDataContract(typeof(TypedPoint));
            var serializer = new Marius.DataContracts.Runtime.DataContractSerializer(provider, typeof(TypedPoint));
            Marius.DataContracts.Runtime.XmlObjectSerializerReadContext[] contexts = 
            {
                new InternalOverride(serializer,contract), new ValueOverride(serializer,contract), new ComplexOverride(serializer,contract)
            };
            
            for (var i = 0; i < contexts.Length; i++)
            {
                using var reader = System.Xml.XmlReader.Create(new StringReader("<TypedPoint xmlns='urn:override'><Number>7</Number></TypedPoint>"));
                reader.MoveToContent();

                var value = contexts[i].Read<TypedPoint>(new Marius.DataContracts.Runtime.XmlReaderDelegator(reader), "TypedPoint", "urn:override", contract);
                if (value.Number != 91 + i)
                    throw new Exception("Typed read bypassed custom context override " + i + ": " + value.Number);
            }
            
            return new SerializationTestResultData { Success = true, Details = "All custom context overrides retained" };
            """;

        var result = await new SerializationTestCompiler().CompileAndRunAsync(contracts, assertions);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.Equal("All custom context overrides retained", result.DeserializationDetails);
    }

    [Fact]
    public async Task AdapterWithIdenticalWireAndValueTypes_StillConvertsExactlyOnce()
    {
        const string contracts =
            """
            [DataContract(Namespace = "urn:adapter")]
            public struct SameValue
            {
                [DataMember] public int Number;
            }
            """;

        const string assertions =
            """
            var source = (Marius.DataContracts.Runtime.ClassDataContract<SameValue>)Marius.DataContracts.Runtime.DataContractContext.TypeDataContracts[typeof(SameValue)];
            var conversions = 0;
            var adapter = new Marius.DataContracts.Runtime.ClassDataContract<SameValue, SameValue>
            {
                Id = source.Id,
                UnderlyingType = typeof(SameValue),
                OriginalUnderlyingType = typeof(SameValue),
                Name = source.Name,
                Namespace = source.Namespace,
                XmlName = source.XmlName,
                IsISerializable = false,
                HasRoot = true,
                IsPrimitive = false,
                IsReference = false,
                CanContainReferences = false,
                IsBuiltInDataContract = false,
                TopLevelElementName = source.TopLevelElementName,
                TopLevelElementNamespace = source.TopLevelElementNamespace,
                MemberNames = source.MemberNames,
                MemberNamespaces = source.MemberNamespaces,
                ChildElementNamespaces = source.ChildElementNamespaces,
                ContractNamespaces = source.ContractNamespaces,
                Read = source.Read,
                Write = source.Write,
                ReadAdapter = wire =>
                {
                    conversions++;
                    return new SameValue { Number = wire.Number + 10 };
                },
                WriteAdapter = value => value
            };
            var serializer = new Marius.DataContracts.Runtime.DataContractSerializer(Marius.DataContracts.Runtime.DataContractContext.DataContractProvider, typeof(SameValue));
            var context = new Marius.DataContracts.Runtime.XmlObjectSerializerReadContext(serializer, adapter);
            
            using var reader = System.Xml.XmlReader.Create(new StringReader("<SameValue xmlns='urn:adapter'><Number>7</Number></SameValue>"));
            reader.MoveToContent();

            var value = context.Read<SameValue>(new Marius.DataContracts.Runtime.XmlReaderDelegator(reader), "SameValue", "urn:adapter", adapter);
            if (value.Number != 17 || conversions != 1)
                throw new Exception("Typed adapter conversion skipped/duplicated: value=" + value.Number + ", conversions=" + conversions);
            
            return new SerializationTestResultData { Success = true, Details = "Adapter conversion executed once" };
            """;

        var result = await new SerializationTestCompiler().CompileAndRunAsync(contracts, assertions);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.Equal("Adapter conversion executed once", result.DeserializationDetails);
    }


    [Fact]
    public async Task InvalidAdapterConversion_PreservesFrameworkReaderPosition()
    {
        const string contracts =
            """
            [DataContract]
            public class DateAnchor
            {
                [DataMember] public DateTimeOffset Value;
            }
            """;

        const string assertions =
            """
            const string xml = "<Outer><DateTimeOffset xmlns='http://schemas.datacontract.org/2004/07/System'><DateTime>2024-06-15T03:15:00Z</DateTime><OffsetMinutes>900</OffsetMinutes></DateTimeOffset><Tail/></Outer>";
            (System.Xml.XmlNodeType Kind, string Name, Type Error) Read(bool generated, bool typed, bool objectContext = false)
            {
                using var reader = System.Xml.XmlReader.Create(new StringReader(xml));
                reader.MoveToContent();
                reader.ReadStartElement("Outer");
                reader.MoveToContent();

                Exception? error = null;
                try
                {
                    if (generated)
                    {
                        var serializer = new Marius.DataContracts.Runtime.DataContractSerializer(Marius.DataContracts.Runtime.DataContractContext.DataContractProvider, typeof(DateTimeOffset));
                        if (typed || objectContext)
                        {
                            var contract = Marius.DataContracts.Runtime.DataContractContext.TypeDataContracts[typeof(DateTimeOffset)];
                            var context = new Marius.DataContracts.Runtime.XmlObjectSerializerReadContext(serializer, contract);
                            var delegator = new Marius.DataContracts.Runtime.XmlReaderDelegator(reader);
                            if (typed)
                                _ = context.Read<DateTimeOffset>(delegator, reader.LocalName, reader.NamespaceURI, contract);
                            else
                                _ = context.InternalDeserialize(delegator, typeof(DateTimeOffset), contract, reader.LocalName, reader.NamespaceURI);
                        }
                        else
                            _ = serializer.ReadObject(reader);
                    }
                    else
                    {
                        _ = new System.Runtime.Serialization.DataContractSerializer(typeof(DateTimeOffset)).ReadObject(reader);
                    }
                }
                catch (Exception exception)
                {
                    error = exception;
                }

                if (error is null)
                    throw new Exception("Invalid adapter payload was accepted");
                return (reader.NodeType, reader.LocalName, error.GetType());
            }

            var expected = Read(false, false);
            var publicRead = Read(true, false);
            if (publicRead != expected)
                throw new Exception("Public adapter failure differs: expected=" + expected + ", actual=" + publicRead);

            // Context calls expose the XmlException before the public serializer
            // wraps it. Compare typed context dispatch with its object control.
            var objectRead = Read(true, false, true);
            var typedRead = Read(true, true);
            if (typedRead != objectRead || typedRead.Kind != expected.Kind || typedRead.Name != expected.Name)
                throw new Exception("Adapter context failure differs: framework=" + expected + ", object=" + objectRead + ", typed=" + typedRead);

            return new SerializationTestResultData { Success = true, Details = "Adapter failure reader position retained" };
            """;

        var result = await new SerializationTestCompiler().CompileAndRunAsync(contracts, assertions);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.Equal("Adapter failure reader position retained", result.DeserializationDetails);
    }


    [Theory]
    [InlineData("Value")]
    [InlineData("Date")]
    public async Task TypedMembers_KeepAttributeValidationAndKnownTypeResolution(string memberName)
    {
        var result = await new SerializationTestCompiler().CompileAndRunAsync(
            """
            [DataContract(Namespace = "urn:attributes")]
            public struct Point
            {
                [DataMember] public int Number;
            }

            [DataContract(Namespace = "urn:attributes")]
            public class Root
            {
                [DataMember(Order = 0)] public Point Value;
                [DataMember(Order = 1)] public DateTimeOffset Date;
            }
            """,
            """
            var serializer = new System.Runtime.Serialization.DataContractSerializer(typeof(Root));
            using var stream = new MemoryStream();
            serializer.WriteObject(stream, new Root { Value = new Point { Number = 7 }, Date = new DateTimeOffset(2024, 6, 15, 9, 0, 0, TimeSpan.FromMinutes(345)) });

            var xml = System.Text.Encoding.UTF8.GetString(stream.ToArray());
            object? Read(string payload, bool generated)
            {
                using var input = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(payload));
                return generated ? new Marius.DataContracts.Runtime.DataContractSerializer(Marius.DataContracts.Runtime.DataContractContext.DataContractProvider, typeof(Root)).ReadObject(input) : serializer.ReadObject(input);
            }

            const string xsi = "http://www.w3.org/2001/XMLSchema-instance", z = "http://schemas.microsoft.com/2003/10/Serialization/";
            foreach (var kind in new[] { "nil", "ref", "id", "unknown-type", "own-type" })
            {
                var document = System.Xml.Linq.XDocument.Parse(xml);
                var member = document.Root!.Element(System.Xml.Linq.XName.Get("MEMBER_NAME", "urn:attributes"))!;
                if (kind == "nil")
                {
                    member.RemoveNodes();
                    member.SetAttributeValue(System.Xml.Linq.XName.Get("nil", xsi), "true");
                }
                else if (kind == "ref")
                {
                    member.RemoveNodes();
                    member.SetAttributeValue(System.Xml.Linq.XName.Get("Ref", z), "missing");
                }
                else if (kind == "id")
                {
                    member.SetAttributeValue(System.Xml.Linq.XName.Get("Id", z), "value1");
                }
                else
                {
                    var typeName = kind == "unknown-type" ? "Missing" : "MEMBER_NAME" == "Value" ? "Point" : "DateTimeOffset";
                    var typeNamespace = kind == "unknown-type" ? "urn:unknown" : "MEMBER_NAME" == "Value" ? "urn:attributes" : "http://schemas.datacontract.org/2004/07/System";
                    member.SetAttributeValue(System.Xml.Linq.XNamespace.Xmlns + "a", typeNamespace);
                    member.SetAttributeValue(System.Xml.Linq.XName.Get("type", xsi), "a:" + typeName);
                }

                var payload = document.ToString();
                Exception? expectedError = null, actualError = null;
                Root? expected = null, actual = null;
                try
                {
                    expected = (Root?)Read(payload, false);
                }
                catch (Exception error)
                {
                    expectedError = error;
                }

                try
                {
                    actual = (Root?)Read(payload, true);
                }
                catch (Exception error)
                {
                    actualError = error;
                }

                if (expectedError?.GetType() != actualError?.GetType())
                    throw new Exception(kind + " attribute parity: framework=" + expectedError + ", custom=" + actualError);
                if (expectedError == null && (actual!.Value.Number != expected!.Value.Number || !actual.Date.EqualsExact(expected.Date)))
                    throw new Exception(kind + " attribute value differs");
            }

            return new SerializationTestResultData { Success = true, Details = "Attribute and known-type validation retained" };
            """.Replace("MEMBER_NAME", memberName));

        Assert.True(result.Success, result.ErrorMessage);
        Assert.Equal("Attribute and known-type validation retained", result.DeserializationDetails);
    }
}