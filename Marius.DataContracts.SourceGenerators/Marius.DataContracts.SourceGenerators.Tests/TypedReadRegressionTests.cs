using System.Linq;
using System.Threading.Tasks;
using Marius.DataContracts.Runtime;
using Xunit;

namespace Marius.DataContracts.SourceGenerators.Tests;

public class TypedReadRegressionTests
{
    [Fact]
    public void ClassRead_ReturnsTypedValueAndExplicitReplacementDiscriminant()
    {
        var readProperty = typeof(ClassDataContract<int>).GetProperty("Read")!;
        var readerDelegate = readProperty.PropertyType.GetMethod("Invoke")!;
        var parameters = readerDelegate.GetParameters();

        Assert.Equal(typeof(int), readerDelegate.ReturnType);
        Assert.Equal(5, parameters.Length);
        Assert.True(parameters[4].IsOut);

        var replacementType = parameters[4].ParameterType.GetElementType()!;
        Assert.Equal("ReadReplacement", replacementType.Name);
        Assert.Equal(typeof(bool), replacementType.GetProperty("HasReplacement")!.PropertyType);
        Assert.Equal(typeof(object), replacementType.GetProperty("Value")!.PropertyType);
        Assert.Null(typeof(ClassDataContract<int>).GetProperty("ReadWithReplacement"));
    }

    [Fact]
    public void GeneratedReaders_UseTypedAdapterContractAndOriginalReturn()
    {
        var (output, driver) = ReviewGeneratorDriver.Generate(
            """
            using System;
            using System.Collections.Generic;
            using System.Runtime.Serialization;
            [DataContract]
            public struct Point
            {
                [DataMember] public long Value;
            }

            [DataContract]
            public class Holder
            {
                [DataMember] public Point Value;
                [DataMember] public DateTimeOffset Date;
                [DataMember] public List<Point> Points;
                [DataMember] public DateTimeOffset? OptionalDate;
            }
            """);
        ReviewGeneratorDriver.AssertCompiles(output);
        Assert.DoesNotContain(output.GetDiagnostics(), diagnostic => diagnostic.Id == "CS8669");

        var source = string.Join("\n", driver.GetRunResult().Results.Single().GeneratedSources.Select(s => s.SourceText.ToString()));
        Assert.Contains("out global::Marius.DataContracts.Runtime.ReadReplacement", source);
        Assert.Contains("ClassDataContract<global::Point>", source);
        Assert.DoesNotContain("return (object)__instance", source);
        // Adapter reader and conversion must have both wire and original types.
        Assert.Matches(@"[A-Za-z]+DataContract<global::Marius\.DataContracts\.Runtime\.Adapters\.DateTimeOffsetAdapter,\s*global::System\.DateTimeOffset>", source);
    }

    private static async Task AssertScenario(string contracts, string assertions)
    {
        const string check =
            """
            void Check(bool condition, string message)
            {
                if (!condition)
                    throw new Exception(message);
            }
            """;

        const string completed =
            """
            return new SerializationTestResultData
            {
                Success = true,
                Details = "Typed read assertions completed"
            };
            """;

        var result = await new SerializationTestCompiler().CompileAndRunAsync(contracts, check + assertions + completed);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.Equal("Typed read assertions completed", result.DeserializationDetails);
    }

    [Fact]
    public Task GeneratedFactoryRead_ReturnsOriginalAndReplacementBeforeOriginalCallbacks()
    {
        const string contracts =
            """
            public static class Events
            {
                public static List<string> Values = new();
            }

            [DataContract]
            public class Factory : IObjectReference, IDeserializationCallback
            {
                [DataMember] public int Value;
                public object GetRealObject(StreamingContext context)
                {
                    Events.Values.Add("replace:" + Value);
                    return "unrelated";
                }

                void IDeserializationCallback.OnDeserialization(object? sender)
                {
                    Events.Values.Add("interface:" + Value);
                    Value++;
                }

                [OnDeserialized]
                private void After(StreamingContext context)
                {
                    Events.Values.Add("after:" + Value);
                    Value++;
                }
            }
            """;

        const string assertions =
            """
            var contract = (ClassDataContract<Factory>)DataContractContext.TypeDataContracts[typeof(Factory)];
            using var stream = new MemoryStream();
            var frameworkSerializer = new System.Runtime.Serialization.DataContractSerializer(typeof(Factory));
            frameworkSerializer.WriteObject(stream, new Factory { Value = 7 });
            stream.Position = 0;

            using var xml = XmlReader.Create(stream);
            xml.MoveToContent();

            var reader = new XmlReaderDelegator(xml);
            var serializer = new Marius.DataContracts.Runtime.DataContractSerializer(DataContractContext.DataContractProvider, typeof(Factory));
            var context = new XmlObjectSerializerReadContext(serializer, contract);
            context.ReadAttributes(reader);
            reader.Read();

            var original = contract.Read(reader, context, contract.MemberNames, contract.MemberNamespaces, out var replacement);

            Check(original.Value == 9, "Both callbacks must mutate the original instance.");
            Check(replacement.HasReplacement, "GetRealObject must supply a replacement.");
            Check(replacement.Value is string text && text == "unrelated", "The replacement must retain its unrelated CLR type and value.");
            Check(string.Join(",", Events.Values) == "replace:7,interface:7,after:8", "Replacement must precede callbacks on the original: " + string.Join(",", Events.Values));
            """;

        return AssertScenario(contracts, assertions);
    }

    [Fact]
    public Task GeneratedStructRead_ReturnsCallbackMutatedOriginal()
    {
        const string contracts =
            """
            [DataContract]
            public struct Mutable : IDeserializationCallback
            {
                [DataMember] public int Value;
                [OnDeserializing]
                private void Before(StreamingContext c)
                {
                    Value = 10;
                }

                void IDeserializationCallback.OnDeserialization(object? sender)
                {
                    Value++;
                }

                [OnDeserialized]
                private void After(StreamingContext c)
                {
                    Value++;
                }
            }
            """;

        const string assertions =
            """
            var contract = (ClassDataContract<Mutable>)DataContractContext.TypeDataContracts[typeof(Mutable)];
            using var stream = new MemoryStream();
            new System.Runtime.Serialization.DataContractSerializer(typeof(Mutable)).WriteObject(stream, new Mutable { Value = 5 });
            stream.Position = 0;

            using var xml = XmlReader.Create(stream);
            xml.MoveToContent();

            var reader = new XmlReaderDelegator(xml);
            var serializer = new Marius.DataContracts.Runtime.DataContractSerializer(DataContractContext.DataContractProvider, typeof(Mutable));
            var context = new XmlObjectSerializerReadContext(serializer, contract);
            context.ReadAttributes(reader);
            reader.Read();

            var original = contract.Read(reader, context, contract.MemberNames, contract.MemberNamespaces, out var replacement);

            Check(original.Value == 7, "Callback mutations must be retained in the returned struct.");
            Check(!replacement.HasReplacement, "An ordinary struct must not produce a replacement.");
            Check(replacement.Value == null, "An absent replacement must leave the object slot empty.");
            """;

        return AssertScenario(contracts, assertions);
    }

    [Fact]
    public Task ReadReplacement_DistinguishesAbsentFromExplicitNullInObjectBridge()
    {
        const string contracts =
            """
            [DataContract]
            public struct Value
            {
                [DataMember] public int Number;
            }
            """;

        const string assertions =
            """
            var source = (ClassDataContract<Value>)DataContractContext.TypeDataContracts[typeof(Value)];
            foreach (var mode in new[] { "absent", "absent-with-value", "present-null", "present-unrelated" })
            {
                var contract = new ClassDataContract<Value>
                {
                    Id = source.Id,
                    UnderlyingType = source.UnderlyingType,
                    OriginalUnderlyingType = source.OriginalUnderlyingType,
                    XmlName = source.XmlName,
                    IsISerializable = false,
                    HasRoot = true,
                    IsPrimitive = false,
                    IsReference = false,
                    TopLevelElementName = source.TopLevelElementName,
                    TopLevelElementNamespace = source.TopLevelElementNamespace,
                    Name = source.Name,
                    Namespace = source.Namespace,
                    IsBuiltInDataContract = false,
                    CanContainReferences = false,
                    MemberNames = source.MemberNames,
                    MemberNamespaces = source.MemberNamespaces,
                    ChildElementNamespaces = source.ChildElementNamespaces,
                    ContractNamespaces = source.ContractNamespaces,
                    Read = (XmlReaderDelegator reader, XmlObjectSerializerReadContext? context, System.Collections.Immutable.ImmutableArray<XmlDictionaryString> memberNames, System.Collections.Immutable.ImmutableArray<XmlDictionaryString> memberNamespaces, out ReadReplacement replacement) =>
                    {
                        replacement = mode switch
                        {
                            "absent" => default,
                            "absent-with-value" => new(false, "ignored"),
                            "present-null" => new(true, null),
                            _ => new(true, "other")
                        };
                        return new Value { Number = 42 };
                    },
                    Write = source.Write
                };

                using var xml = XmlReader.Create(new StringReader("<Value></Value>"));
                xml.MoveToContent();

                var result = contract.ReadXmlValue(new XmlReaderDelegator(xml), null);

                switch (mode)
                {
                    case "present-null":
                        Check(result == null, "An explicitly present null must replace the original.");
                        break;
                    case "present-unrelated":
                        Check(result is string text && text == "other", "An unrelated replacement must be returned.");
                        break;
                    default:
                        Check(result is Value value && value.Number == 42,"An absent replacement must return the original, even when Value is populated: " + mode);
                        break;
                }
            }
            """;

        return AssertScenario(contracts, assertions);
    }

    [Fact]
    public Task TypedAdapter_UsesWireAndValueDelegatesWithoutLeakingWireIdentity()
    {
        const string contracts =
            """
            [DataContract]
            public class Holder
            {
                [DataMember] public DateTimeOffset Value;
            }
            """;

        const string assertions =
            """
            var contract = (ClassDataContract<Marius.DataContracts.Runtime.Adapters.DateTimeOffsetAdapter, DateTimeOffset>)DataContractContext.TypeDataContracts[typeof(DateTimeOffset)];
            var expected = new DateTimeOffset(2024, 6, 15, 9, 0, 0, TimeSpan.FromMinutes(345));
            Func<Marius.DataContracts.Runtime.Adapters.DateTimeOffsetAdapter, DateTimeOffset> read = contract.ReadAdapter;
            Func<DateTimeOffset, Marius.DataContracts.Runtime.Adapters.DateTimeOffsetAdapter> write = contract.WriteAdapter;

            Check(read(write(expected)).EqualsExact(expected), "Typed adapter conversion must preserve value and offset.");
            Check(contract.OriginalUnderlyingType == typeof(DateTimeOffset), "Original metadata type must be DateTimeOffset.");
            Check(contract.UnderlyingType == typeof(Marius.DataContracts.Runtime.Adapters.DateTimeOffsetAdapter), "Wire metadata type must be DateTimeOffsetAdapter.");

            using var stream = new MemoryStream();
            var serializer = new Marius.DataContracts.Runtime.DataContractSerializer(DataContractContext.DataContractProvider, typeof(DateTimeOffset));
            serializer.WriteObject(stream, expected);
            stream.Position = 0;

            var result = serializer.ReadObject(stream);

            Check(result is DateTimeOffset actual && actual.EqualsExact(expected), "The public object bridge must return DateTimeOffset with the original value and offset.");
            """;

        return AssertScenario(contracts, assertions);
    }
}
