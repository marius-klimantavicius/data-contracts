using System.Threading.Tasks;
using System.Linq;
using Xunit;

namespace Marius.DataContracts.SourceGenerators.Tests;

public class CallbackAdapterRegressionTests
{
    private const string Oracle = """
        public static class RuntimeOracle
        {
            public static void Check(bool condition, string message)
            {
                if (!condition)
                    throw new Exception(message);
            }

            public static void ThrowsSerialization(Action action, string message)
            {
                try
                {
                    action();
                }
                catch (System.Runtime.Serialization.SerializationException)
                {
                    return;
                }

                throw new Exception(message + ": expected SerializationException, but operation succeeded");
            }

            public static string Write(bool custom, Type type, object value,System.Runtime.Serialization.DataContractSerializerSettings? settings = null)
            {
                settings ??= new System.Runtime.Serialization.DataContractSerializerSettings();

                using var stream = new MemoryStream();
                if (custom)
                    new Marius.DataContracts.Runtime.DataContractSerializer(Marius.DataContracts.Runtime.DataContractContext.DataContractProvider, type, settings).WriteObject(stream, value);
                else
                    new System.Runtime.Serialization.DataContractSerializer(type, settings).WriteObject(stream, value);

                return System.Text.Encoding.UTF8.GetString(stream.ToArray());
            }

            public static object Read(bool custom, Type type, string xml,System.Runtime.Serialization.DataContractSerializerSettings? settings = null)
            {
                settings ??= new System.Runtime.Serialization.DataContractSerializerSettings();

                using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(xml));
                return custom
                    ? new Marius.DataContracts.Runtime.DataContractSerializer(Marius.DataContracts.Runtime.DataContractContext.DataContractProvider, type, settings).ReadObject(stream)!
                    : new System.Runtime.Serialization.DataContractSerializer(type, settings).ReadObject(stream)!;
            }

            public static void SameXml(string expected, string actual)
            {
                XElement Normalize(XElement element) => new XElement(element.Name,
                    element.Attributes().Where(a => !a.IsNamespaceDeclaration)
                        .OrderBy(a => a.Name.ToString())
                        .Select(a => new XAttribute(a.Name,a.Name == XName.Get("type", "http://www.w3.org/2001/XMLSchema-instance")? ExpandQName(element, a.Value) : a.Value)),
                    element.HasElements ? element.Elements().Select(Normalize) : (object)element.Value);
                
                string ExpandQName(XElement element, string value)
                {
                    var colon = value.IndexOf(':');
                    return colon < 0 ? element.GetDefaultNamespace().NamespaceName + ":" + value: element.GetNamespaceOfPrefix(value.Substring(0, colon))!.NamespaceName + ":" + value.Substring(colon + 1);
                }

                Check(XNode.DeepEquals(Normalize(XElement.Parse(expected)), Normalize(XElement.Parse(actual))), "XML differs. Expected: " + expected + " Actual: " + actual);
            }
        }
        """;

    private const string Completed =
        """
        return new SerializationTestResultData { Success = true, Details = "Explicit assertions completed" };
        """;

    private static async Task AssertScenario(string contracts, string assertions)
    {
        var result = await new SerializationTestCompiler().CompileAndRunAsync(contracts + Oracle, assertions + Completed);
        Assert.True(result.Success, result.ErrorMessage);
        Assert.Equal("Explicit assertions completed", result.DeserializationDetails);
    }


    [Theory]
    [InlineData("[OnSerializing] private void Callback(ref StreamingContext context) { }")]
    [InlineData("[OnDeserialized] private void Callback(in StreamingContext context) { }")]
    [InlineData("[OnSerializing] public override void Existing(StreamingContext context) { }")]
    [InlineData("[OnSerialized] private int Callback(StreamingContext context) => 0;")]
    [InlineData("[OnSerializing] public virtual void Callback(StreamingContext context) { }")]
    [InlineData("[OnSerializing, OnSerialized] private void Callback(StreamingContext context) { }")]
    [InlineData("[OnSerializing] private void A(StreamingContext context) { } [OnSerializing] private void B(StreamingContext context) { }")]
    public void CallbackValidation_ReportsInvalidContract(string method)
    {
        var compilation = ReviewGeneratorDriver.Compilation(
            $$"""
            using System.Runtime.Serialization;

            [DataContract]
            public class Base
            {
                public virtual void Existing(StreamingContext context) { }
            }

            [DataContract]
            public class Root : Base
            {
                {{method}}
            }
            """);

        var driver = ReviewGeneratorDriver.Driver().RunGeneratorsAndUpdateCompilation(compilation, out _, out var diagnostics);

        Assert.Null(driver.GetRunResult().Results.Single().Exception);
        Assert.Contains(diagnostics, diagnostic => diagnostic.Id == "DCS1001");
    }

    [Theory]
    [InlineData("[OnSerializing] public void Invoke(StreamingContext c) { }")]
    [InlineData("[OnSerializing] void ICallback.Invoke(StreamingContext c) { }")]
    public void CallbackValidation_InterfaceImplementationsAreVirtualInMetadata(string method)
    {
        var compilation = ReviewGeneratorDriver.Compilation(
            $$"""
            using System.Runtime.Serialization;

            public interface ICallback
            {
                void Invoke(StreamingContext context);
            }

            [DataContract]
            public class Root : ICallback
            {
                {{method}}
            }
            """);

        var driver = ReviewGeneratorDriver.Driver().RunGeneratorsAndUpdateCompilation(compilation, out _, out var diagnostics);

        Assert.Null(driver.GetRunResult().Results.Single().Exception);
        Assert.Contains(diagnostics, diagnostic => diagnostic.Id == "DCS1001");
    }

    [Fact]
    public void CallbackValidation_ValidPrivateCallbacksDoNotReportDcs4002Through4005()
    {
        var (output, driver) = ReviewGeneratorDriver.Generate(
            """
            using System.Runtime.Serialization;
            [DataContract]
            public class Root
            {
                [OnSerializing]
                private void A(StreamingContext c)
                {
                }

                [OnSerialized]
                private void B(StreamingContext c)
                {
                }

                [OnDeserializing]
                private void C(StreamingContext c)
                {
                }

                [OnDeserialized]
                private void D(StreamingContext c)
                {
                }
            }
            """);

        ReviewGeneratorDriver.AssertCompiles(output);
        Assert.DoesNotContain(driver.GetRunResult().Diagnostics, diagnostic => diagnostic.Id is "DCS4002" or "DCS4003" or "DCS4004" or "DCS4005");
    }

    [Theory]
    [InlineData("OnSerializing")]
    [InlineData("OnSerialized")]
    [InlineData("OnDeserializing")]
    [InlineData("OnDeserialized")]
    public void CallbackValidation_GenericMethodsAreExplicitlyUnsupported(string attribute)
    {
        var compilation = ReviewGeneratorDriver.Compilation(
            $$"""
            using System.Runtime.Serialization;

            [DataContract]
            public class Root
            {
                [{{attribute}}]
                private void Callback<T>(StreamingContext context) { }
            }
            """);

        var driver = ReviewGeneratorDriver.Driver().RunGeneratorsAndUpdateCompilation(compilation, out _, out var diagnostics);

        Assert.Null(driver.GetRunResult().Results.Single().Exception);
        Assert.Contains(diagnostics, diagnostic =>
            diagnostic.Id == "DCS1007" &&
            diagnostic.GetMessage().Contains("Generic callback methods are unsupported"));
    }

    [Fact]
    public Task Callbacks_ObserveOrderContextAndOriginalMutation()
    {
        const string contracts =
            """
            public static class Events
            {
                public static List<string> Log = new();
            }

            [DataContract]
            public class Base
            {
                [DataMember(Order = 0)] public int Value;
                protected void Log(string name, StreamingContext c)
                {
                    if (c.State != StreamingContextStates.All || c.Context != null)
                        throw new Exception("Context");
                    Events.Log.Add(name + ":" + Value);
                }

                [OnSerializing]
                private void Before(StreamingContext c)
                {
                    Log("base.before", c);
                    Value++;
                }

                [OnSerialized]
                private void After(StreamingContext c)
                {
                    Log("base.after", c);
                    Value++;
                }

                [OnDeserializing]
                private void Reading(StreamingContext c)
                {
                    Log("base.reading", c);
                    Value = 10;
                }

                [OnDeserialized]
                private void Read(StreamingContext c)
                {
                    Log("base.read", c);
                    Value++;
                }
            }

            [DataContract]
            public class Root : Base, IDeserializationCallback
            {
                [OnSerializing]
                private void Before(StreamingContext c)
                {
                    Log("root.before", c);
                    Value++;
                }

                [OnSerialized]
                private void After(StreamingContext c)
                {
                    Log("root.after", c);
                    Value++;
                }

                [OnDeserializing]
                private void Reading(StreamingContext c)
                {
                    Log("root.reading", c);
                    Value++;
                }

                [OnDeserialized]
                private void Read(StreamingContext c)
                {
                    Log("root.read", c);
                    Value++;
                }

                void IDeserializationCallback.OnDeserialization(object? sender)
                {
                    Events.Log.Add("interface:" + Value);
                    Value++;
                }
            }
            """;

        const string assertions =
            """
            var frameworkOriginal = new Root { Value = 20 };
            var generatedOriginal = new Root { Value = 20 };

            Events.Log.Clear();

            var frameworkXml = RuntimeOracle.Write(false, typeof(Root), frameworkOriginal);
            var frameworkWriteLog = string.Join(",", Events.Log);

            Events.Log.Clear();

            var generatedXml = RuntimeOracle.Write(true, typeof(Root), generatedOriginal);
            var generatedWriteLog = string.Join(",", Events.Log);

            RuntimeOracle.Check(generatedWriteLog == frameworkWriteLog, "Write callback order: " + generatedWriteLog + " vs " + frameworkWriteLog);
            RuntimeOracle.Check(frameworkOriginal.Value == 24, "Framework callbacks must mutate the original to 24.");
            RuntimeOracle.Check(generatedOriginal.Value == frameworkOriginal.Value, "Generated callbacks must preserve the same original-instance mutations.");
            RuntimeOracle.SameXml(frameworkXml, generatedXml);

            Events.Log.Clear();

            var frameworkResult = (Root)RuntimeOracle.Read(false, typeof(Root), frameworkXml);
            var frameworkReadLog = string.Join(",", Events.Log);

            Events.Log.Clear();

            var generatedResult = (Root)RuntimeOracle.Read(true, typeof(Root), frameworkXml);
            var generatedReadLog = string.Join(",", Events.Log);

            RuntimeOracle.Check(generatedReadLog == frameworkReadLog, "Read callback order: " + generatedReadLog + " vs " + frameworkReadLog);
            RuntimeOracle.Check(generatedResult.Value == frameworkResult.Value, "Read callbacks must produce the same member value.");
            """;

        return AssertScenario(contracts, assertions);
    }

    [Fact]
    public Task Callbacks_StructInterfaceMutationPrecedesAttribute()
    {
        const string contracts =
            """
            [DataContract]
            public struct Root : IDeserializationCallback
            {
                [DataMember] public int Value;
                void IDeserializationCallback.OnDeserialization(object? sender)
                {
                    Value += 10;
                }

                [OnDeserialized]
                private void After(StreamingContext context)
                {
                    Value *= 2;
                }
            }
            """;

        const string assertions =
            """
            var xml = RuntimeOracle.Write(false, typeof(Root), new Root { Value = 3 });
            var expected = (Root)RuntimeOracle.Read(false, typeof(Root), xml);
            var actual = (Root)RuntimeOracle.Read(true, typeof(Root), xml);
            RuntimeOracle.Check(expected.Value == 26, "Oracle constrained interface callback mutation");
            RuntimeOracle.Check(actual.Value == expected.Value, "Struct interface callback must mutate original local before OnDeserialized: " + actual.Value);
            """;

        return AssertScenario(contracts, assertions);
    }

    [Fact]
    public Task AdapterRoots_DoNotRequireAnAnnotatedHolder()
    {
        const string contracts = "";

        const string assertions =
            """
            foreach (var value in new object[] { new DateTimeOffset(2024, 1, 1, 3, 0, 0, TimeSpan.FromHours(3)), new MemoryStream(new byte[] { 1, 2, 3 }) })
            {
                var expected = RuntimeOracle.Write(false, value.GetType(), value);
                var actual = RuntimeOracle.Write(true, value.GetType(), value);
                RuntimeOracle.SameXml(expected, actual);
                RuntimeOracle.Check(RuntimeOracle.Read(true, value.GetType(), expected).GetType() == value.GetType(), "Adapter root must return original CLR type");
            }
            """;

        return AssertScenario(contracts, assertions);
    }

    [Fact]
    public Task AdapterObjectPositions_RequireScopedKnownTypes()
    {
        const string contracts =
            """
            [DataContract]
            public class Root
            {
                [DataMember] public object Value = null!;
            }
            """;

        const string assertions =
            """
            foreach (var value in new object[] { new DateTimeOffset(2024, 1, 1, 3, 0, 0, TimeSpan.FromHours(3)), new MemoryStream(new byte[] { 1, 2, 3 }) })
            {
                var root = new Root { Value = value };
                RuntimeOracle.ThrowsSerialization(() => RuntimeOracle.Write(false, typeof(Root), root), "Framework unlisted adapter");
                RuntimeOracle.ThrowsSerialization(() => RuntimeOracle.Write(true, typeof(Root), root), "Generated unlisted adapter");

                var settings = new System.Runtime.Serialization.DataContractSerializerSettings { KnownTypes = new[] { value.GetType() } };
                foreach (var type in new[] { typeof(object), typeof(Root) })
                {
                    object original = type == typeof(object) ? value : root;
                    var expected = RuntimeOracle.Write(false, type, original, settings);
                    var actual = RuntimeOracle.Write(true, type, original, settings);
                    RuntimeOracle.SameXml(expected, actual);

                    var read = RuntimeOracle.Read(true, type, expected, settings);
                    RuntimeOracle.Check((read is Root r ? r.Value : read).GetType() == value.GetType(), "Known adapter original CLR type");
                }
            }
            """;

        return AssertScenario(contracts, assertions);
    }

    [Fact]
    public Task DateTimeOffset_UsesFrameworkAdapterContract()
    {
        const string contracts =
            """
            [DataContract, KnownType(typeof(DateTimeOffset))]
            public class Root
            {
                [DataMember] public DateTimeOffset Value;
                [DataMember] public DateTimeOffset? Optional;
                [DataMember] public List<DateTimeOffset> Values = new();
                [DataMember] public object Boxed = null!;
            }
            """;

        const string assertions =
            """
            foreach (var value in new[] { default(DateTimeOffset), DateTimeOffset.MinValue, DateTimeOffset.MaxValue, new DateTimeOffset(2024, 6, 15, 9, 0, 0, TimeSpan.FromMinutes(345)) })
            {
                foreach (var type in new[] { typeof(DateTimeOffset), typeof(DateTimeOffset?), typeof(Root) })
                {
                    object original = type == typeof(Root) ? new Root { Value = value, Optional = value, Values = new() { value }, Boxed = value } : value;
                    var net = RuntimeOracle.Write(false, type, original);
                    var generated = RuntimeOracle.Write(true, type, original);
                    RuntimeOracle.SameXml(net, generated);
                    foreach (var custom in new[] { false, true })
                    {
                        var read = RuntimeOracle.Read(custom, type, custom ? net : generated);
                        var dto = read is Root r ? (DateTimeOffset)r.Boxed : (DateTimeOffset)read;
                        RuntimeOracle.Check(dto.EqualsExact(value), "DTO value/offset/return type");
                    }
                }
            }
            """;

        return AssertScenario(contracts, assertions);
    }

    [Fact]
    public Task MemoryStream_UsesFrameworkAdapterContractAndState()
    {
        const string contracts =
            """
            [DataContract, KnownType(typeof(MemoryStream))]
            public class Root
            {
                [DataMember] public MemoryStream Value = null!;
                [DataMember] public object Boxed = null!;
            }
            """;

        const string assertions =
            """
            foreach (var value in new[] { new MemoryStream(), new MemoryStream(64), new MemoryStream(new byte[] { 1, 2, 3 }, false), new MemoryStream(new byte[] { 9, 1, 2, 3, 8 }, 1, 3, true, true) })
            {
                value.Position = value.Length > 0 ? 1 : 0;
                foreach (var type in new[] { typeof(MemoryStream), typeof(Root) })
                {
                    object original = type == typeof(Root) ? new Root { Value = value, Boxed = value } : value;
                    var frameworkXml = RuntimeOracle.Write(false, type, original);
                    var generatedXml = RuntimeOracle.Write(true, type, original);
                    RuntimeOracle.SameXml(frameworkXml, generatedXml);

                    var frameworkResult = RuntimeOracle.Read(false, type, frameworkXml);
                    var generatedResult = RuntimeOracle.Read(true, type, frameworkXml);
                    var expectedStream = frameworkResult is Root expectedRoot ? (MemoryStream)expectedRoot.Boxed : (MemoryStream)frameworkResult;
                    var actualStream = generatedResult is Root actualRoot ? (MemoryStream)actualRoot.Boxed : (MemoryStream)generatedResult;

                    RuntimeOracle.Check(actualStream.ToArray().SequenceEqual(expectedStream.ToArray()), "Stream bytes differ.");
                    RuntimeOracle.Check(actualStream.Length == expectedStream.Length, "Stream length differs.");
                    RuntimeOracle.Check(actualStream.Capacity == expectedStream.Capacity, "Stream capacity differs.");
                    RuntimeOracle.Check(actualStream.Position == expectedStream.Position, "Stream position differs.");
                    RuntimeOracle.Check(actualStream.CanWrite == expectedStream.CanWrite, "Stream writability differs.");
                    RuntimeOracle.Check(actualStream.TryGetBuffer(out _) == expectedStream.TryGetBuffer(out _), "Stream buffer exposure differs.");
                }
            }
            """;

        return AssertScenario(contracts, assertions);
    }
}