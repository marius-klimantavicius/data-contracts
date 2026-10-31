using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Xml;
using Marius.DataContracts.Runtime;
using Xunit;

namespace Marius.DataContracts.SourceGenerators.Tests;

public class RuntimeParityRegressionTests
{
    // These helpers throw on each failed assertion. No assertion depends on the
    // reflection-based object comparer or the harness's default result booleans.
    private const string Oracle = """
        public static class RuntimeOracle
        {
            public static void Check(bool condition, string message)
            {
                if (!condition) throw new Exception(message);
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
                        .Select(a => new XAttribute(a.Name,a.Name == XName.Get("type", "http://www.w3.org/2001/XMLSchema-instance") ? ExpandQName(element, a.Value) : a.Value)),
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
    [InlineData(false)]
    [InlineData(true)]
    public Task UnlistedGeneratedDerived_IsRejectedLikeDotNet(bool read) => AssertScenario(
        """
        [DataContract] public class Base { [DataMember] public int Value { get; set; } }
        [DataContract] public class Derived : Base { [DataMember] public int Other { get; set; } }
        [DataContract] public class Root { [DataMember] public Base Value { get; set; } = null!; }
        """,
        $$"""
        var value = new Root { Value = new Derived { Value = 11, Other = 22 } };
        if ({{(read ? "true" : "false")}})
        {
            var xml = RuntimeOracle.Write(false, typeof(Root), value,new System.Runtime.Serialization.DataContractSerializerSettings { KnownTypes = new[] { typeof(Derived) } });
            RuntimeOracle.ThrowsSerialization(() => RuntimeOracle.Read(false, typeof(Root), xml), ".NET unlisted derived read");
            RuntimeOracle.ThrowsSerialization(() => RuntimeOracle.Read(true, typeof(Root), xml), "Custom unlisted derived read");
        }
        else
        {
            RuntimeOracle.ThrowsSerialization(() => RuntimeOracle.Write(false, typeof(Root), value), ".NET unlisted derived write");
            RuntimeOracle.ThrowsSerialization(() => RuntimeOracle.Write(true, typeof(Root), value), "Custom unlisted derived write");
        }
        """);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task UnrelatedGeneratedQName_DoesNotShadowScopedKnownType(bool read) => AssertScenario(
        """
        [DataContract(Name = "Shared", Namespace = "urn:runtime-parity")]
        public class A { [DataMember] public int Value { get; set; } }
        [DataContract(Name = "Shared", Namespace = "urn:runtime-parity")]
        public class B { [DataMember] public string Value { get; set; } = ""; }
        [DataContract, KnownType(typeof(B))]
        public class Root { [DataMember] public object Value { get; set; } = null!; }
        """,
        $$"""
        var value = new Root { Value = new B { Value = "known B" } };
        var expected = RuntimeOracle.Write(false, typeof(Root), value);
        var xml = {{(read ? "expected" : "RuntimeOracle.Write(true, typeof(Root), value)")}};
        RuntimeOracle.SameXml(expected, xml);
        var net = (Root)RuntimeOracle.Read(false, typeof(Root), xml);
        var custom = (Root)RuntimeOracle.Read(true, typeof(Root), xml);
        RuntimeOracle.Check(net.Value is B nb && nb.Value == "known B", ".NET must materialize B");
        RuntimeOracle.Check(custom.Value is B cb && cb.Value == "known B", "Custom must materialize scoped B, not unrelated A");
        """);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task SeparateKnownTypeScopes_CanReuseQName(bool read) => AssertScenario(
        """
        [DataContract(Name = "Shared", Namespace = "urn:runtime-parity")]
        public class A { [DataMember] public int Value { get; set; } }
        [DataContract(Name = "Shared", Namespace = "urn:runtime-parity")]
        public class B { [DataMember] public string Value { get; set; } = ""; }
        [DataContract, KnownType(typeof(A))]
        public class First { [DataMember] public object Value { get; set; } = null!; }
        [DataContract, KnownType(typeof(B))]
        public class Second { [DataMember] public object Value { get; set; } = null!; }
        [DataContract] public class Root
        {
            [DataMember(Order = 0)] public First First { get; set; } = null!;
            [DataMember(Order = 1)] public Second Second { get; set; } = null!;
        }
        """,
        $$"""
        var value = new Root { First = new First { Value = new A { Value = 17 } }, Second = new Second { Value = new B { Value = "separate B" } } };
        var expected = RuntimeOracle.Write(false, typeof(Root), value);
        var xml = {{(read ? "expected" : "RuntimeOracle.Write(true, typeof(Root), value)")}};
        RuntimeOracle.SameXml(expected, xml);
        foreach (var custom in new[] { false, true })
        {
            var result = (Root)RuntimeOracle.Read(custom, typeof(Root), xml);
            RuntimeOracle.Check(result.First.Value is A a && a.Value == 17, "First scope must resolve A");
            RuntimeOracle.Check(result.Second.Value is B b && b.Value == "separate B", "Second scope must resolve B");
        }
        """);

    [Fact]
    public Task PrimitiveLookup_DoesNotExposeAllAvailableContracts() => AssertScenario(
        """
        [DataContract] public enum Choice { [EnumMember] One }
        [DataContract] public class Root
        {
            [DataMember] public Choice Choice { get; set; }
            [DataMember] public List<int> Values { get; set; } = new();
        }
        """,
        """
        var provider = DataContractContext.DataContractProvider;
        foreach (var type in new[] { typeof(Root), typeof(Choice), typeof(List<int>) })
        {
            RuntimeOracle.Check(provider.TryGetDataContract(type, out var contract), "Generated contract must remain available: " + type);
            RuntimeOracle.Check(provider.GetPrimitiveDataContract(type) == null, "Type primitive lookup must exclude " + type);
            RuntimeOracle.Check(provider.GetPrimitiveDataContract(contract!.XmlName.Name, contract.XmlName.Namespace) == null,
                "QName primitive lookup must exclude " + type);
        }
        foreach (var type in new[] { typeof(object), typeof(int), typeof(string), typeof(byte[]), typeof(Uri), typeof(Guid), typeof(TimeSpan) })
        {
            var primitive = provider.GetPrimitiveDataContract(type);
            RuntimeOracle.Check(primitive is PrimitiveDataContract && primitive.IsBuiltInDataContract, "Missing primitive contract: " + type);
            RuntimeOracle.Check(provider.GetPrimitiveDataContract(primitive!.XmlName.Name, primitive.XmlName.Namespace) == primitive,
                "Primitive QName lookup mismatch: " + type);
        }
        RuntimeOracle.Check(!provider.GetPrimitiveDataContract(typeof(object))!.IsPrimitive,
            "Object is a primitive contract category even though IsPrimitive is false");
        """);

    [Fact]
    public Task SerializerKnownTypes_IncludeListedContractAndTransitiveKnownTypes() => AssertScenario(
        """
        [DataContract] public class Base { }
        [DataContract, KnownType(typeof(Leaf))] public class Derived : Base { [DataMember] public int Value { get; set; } }
        [DataContract] public class Leaf : Derived { [DataMember] public string Text { get; set; } = ""; }
        [DataContract] public class Root { [DataMember] public Base Value { get; set; } = null!; }
        """,
        """
        var provider = DataContractContext.DataContractProvider;
        var known = XmlObjectSerializerContext.GetDataContractsForKnownTypes(provider, new[] { typeof(Derived) });
        foreach (var type in new[] { typeof(Derived), typeof(Leaf) })
        {
            RuntimeOracle.Check(provider.TryGetDataContract(type, out var contract), "Missing generated contract");
            RuntimeOracle.Check(known!.TryGetValue(contract!.XmlName, out var actual) && actual.UnderlyingType == type,
                "Serializer known-type map must include " + type.Name);
        }
        var settings = new System.Runtime.Serialization.DataContractSerializerSettings { KnownTypes = new[] { typeof(Derived) } };
        foreach (var child in new Derived[] { new Derived { Value = 7 }, new Leaf { Value = 8, Text = "leaf" } })
        {
            var value = new Root { Value = child };
            var netXml = RuntimeOracle.Write(false, typeof(Root), value, settings);
            var customXml = RuntimeOracle.Write(true, typeof(Root), value, settings);
            RuntimeOracle.SameXml(netXml, customXml);
            foreach (var custom in new[] { false, true })
            {
                var result = (Root)RuntimeOracle.Read(custom, typeof(Root), custom ? netXml : customXml, settings);
                RuntimeOracle.Check(result.Value.GetType() == child.GetType() && ((Derived)result.Value).Value == child.Value,
                    "Serializer KnownTypes must support listed and transitive types");
                if (child is Leaf) RuntimeOracle.Check(((Leaf)result.Value).Text == "leaf", "Leaf value lost");
            }
        }
        """);

    [Fact]
    public Task BuiltInObjectValues_DoNotRequireKnownTypes() => AssertScenario(
        """
        [DataContract] public class Root { [DataMember] public object Value { get; set; } = null!; }
        """,
        """
        foreach (var child in new object[] { new object(), 42, "text", new byte[] { 1, 2, 3 }, new Uri("https://example.test/"), Guid.Parse("01020304-0506-0708-090a-0b0c0d0e0f10"), TimeSpan.FromMinutes(3) })
        {
            var value = new Root { Value = child };
            var netXml = RuntimeOracle.Write(false, typeof(Root), value);
            var customXml = RuntimeOracle.Write(true, typeof(Root), value);
            RuntimeOracle.SameXml(netXml, customXml);
            foreach (var custom in new[] { false, true })
            {
                var result = ((Root)RuntimeOracle.Read(custom, typeof(Root), custom ? netXml : customXml)).Value;
                RuntimeOracle.Check(result.GetType() == child.GetType(), "Built-in runtime type lost: " + child.GetType());
                RuntimeOracle.Check(child is byte[] bytes ? ((byte[])result).SequenceEqual(bytes)
                    : child.GetType() == typeof(object) || child.Equals(result), "Built-in value lost: " + child.GetType());
            }
        }
        """);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task ConflictingSerializerKnownTypes_AreRejectedLikeDotNet(bool read) => AssertScenario(
        """
        [DataContract(Name = "Shared", Namespace = "urn:runtime-parity")]
        public class A { [DataMember] public int Value { get; set; } }
        [DataContract(Name = "Shared", Namespace = "urn:runtime-parity")]
        public class B { [DataMember] public string Value { get; set; } = ""; }
        [DataContract] public class Root { [DataMember] public object Value { get; set; } = null!; }
        """,
        $$"""
        var value = new Root { Value = new A { Value = 42 } };
        var valid = new System.Runtime.Serialization.DataContractSerializerSettings { KnownTypes = new[] { typeof(A) } };
        var xml = RuntimeOracle.Write(false, typeof(Root), value, valid);
        var conflicting = new System.Runtime.Serialization.DataContractSerializerSettings { KnownTypes = new[] { typeof(A), typeof(B) } };
        Exception? Failure(bool custom)
        {
            try
            {
                if ({{(read ? "true" : "false")}}) RuntimeOracle.Read(custom, typeof(Root), xml, conflicting);
                else RuntimeOracle.Write(custom, typeof(Root), value, conflicting);
            }
            catch (Exception ex) { return ex; }
            return null;
        }
        var expected = Failure(false);
        RuntimeOracle.Check(expected is InvalidOperationException, ".NET duplicate known type failure: " + expected?.GetType());
        var actual = Failure(true);
        RuntimeOracle.Check(actual?.GetType() == expected!.GetType(),
            "Custom conflicting serializer known types must throw " + expected.GetType().Name + ", actual: " + (actual?.GetType().Name ?? "success"));
        """);

    [Fact]
    public void BuiltInXmlContract_IsKnownWithoutBeingAPrimitiveContract()
    {
        var dictionary = new XmlDictionary();
        var contract = new XmlDataContract<XmlElement>
        {
            Id = 0,
            UnderlyingType = typeof(XmlElement),
            OriginalUnderlyingType = typeof(XmlElement),
            XmlName = new XmlQualifiedName("XmlElement", "http://schemas.datacontract.org/2004/07/System.Xml"),
            Name = dictionary.Add("XmlElement"),
            Namespace = dictionary.Add("http://schemas.datacontract.org/2004/07/System.Xml"),
            TopLevelElementName = null,
            TopLevelElementNamespace = null,
            IsBuiltInDataContract = true,
            IsPrimitive = false,
            IsReference = false,
            IsISerializable = false,
            HasRoot = false,
            CanContainReferences = false,
            Create = () => throw new InvalidOperationException("This test only resolves metadata"),
            Read = (_, _) => throw new InvalidOperationException("This test only resolves metadata"),
        };
        var provider = new DataContractProvider([contract],
            new Dictionary<Type, DataContract> { [typeof(XmlElement)] = contract }.ToFrozenDictionary());
        Assert.True(provider.TryGetDataContract(typeof(XmlElement), out var available));
        Assert.Same(contract, available);
        Assert.Null(provider.GetPrimitiveDataContract(typeof(XmlElement)));
        Assert.Null(provider.GetPrimitiveDataContract(contract.XmlName.Name, contract.XmlName.Namespace));
        var serializer = new DataContractSerializer(provider, typeof(XmlElement));
        var context = new XmlObjectSerializerContext(serializer, contract);
        Assert.Equal(typeof(XmlElement), context.ResolveNameFromKnownTypes(contract.XmlName));
    }

    [Theory]
    [InlineData("list", 5, false)]
    [InlineData("list", 5, true)]
    [InlineData("collection", 5, false)]
    [InlineData("collection", 5, true)]
    [InlineData("nongeneric", 5, false)]
    [InlineData("nongeneric", 5, true)]
    [InlineData("enumerable", 5, false)]
    [InlineData("enumerable", 5, true)]
    [InlineData("array", 5, false)]
    [InlineData("array", 5, true)]
    [InlineData("dictionary", 8, false)]
    [InlineData("dictionary", 8, true)]
    public Task CollectionQuota_BoundariesMatchDotNet(string shape, int minimumQuota, bool read) => AssertScenario(
        """
        public class IntEnumerable : IEnumerable<int>
        {
            private readonly List<int> items = new();
            public void Add(int value) => items.Add(value);
            public IEnumerator<int> GetEnumerator() => items.GetEnumerator();
            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }
        [DataContract] public class ListRoot { [DataMember] public List<int> Values { get; set; } = new(); }
        [DataContract] public class CollectionRoot { [DataMember] public HashSet<int> Values { get; set; } = new(); }
        [DataContract] public class NonGenericRoot { [DataMember] public ArrayList Values { get; set; } = new(); }
        [DataContract] public class EnumerableRoot { [DataMember] public IntEnumerable Values { get; set; } = new(); }
        [DataContract] public class ArrayRoot { [DataMember] public int[] Values { get; set; } = Array.Empty<int>(); }
        [DataContract] public class DictionaryRoot { [DataMember] public Dictionary<string, int> Values { get; set; } = new(); }
        """,
        $$"""
        object value = "{{shape}}" switch
        {
            "list" => new ListRoot { Values = new List<int> { 1, 2, 3 } },
            "collection" => new CollectionRoot { Values = new HashSet<int> { 1, 2, 3 } },
            "nongeneric" => new NonGenericRoot { Values = new ArrayList { 1, 2, 3 } },
            "enumerable" => new EnumerableRoot { Values = new IntEnumerable { 1, 2, 3 } },
            "array" => new ArrayRoot { Values = new[] { 1, 2, 3 } },
            "dictionary" => new DictionaryRoot { Values = new Dictionary<string, int> { ["one"] = 1, ["two"] = 2 } },
            _ => throw new Exception("Unknown test shape"),
        };
        void Validate(object result)
        {
            IEnumerable<int> items = result switch
            {
                ListRoot r => r.Values,
                CollectionRoot r => r.Values,
                NonGenericRoot r => r.Values.Cast<int>(),
                EnumerableRoot r => r.Values,
                ArrayRoot r => r.Values,
                DictionaryRoot r => r.Values.Values,
                _ => throw new Exception("Unexpected deserialized type"),
            };
            RuntimeOracle.Check(items.SequenceEqual(value is DictionaryRoot ? new[] { 1, 2 } : new[] { 1, 2, 3 }), "Collection contents differ");
            if (result is DictionaryRoot dictionary)
                RuntimeOracle.Check(dictionary.Values.Count == 2 && dictionary.Values["one"] == 1 && dictionary.Values["two"] == 2, "Dictionary keys or values lost");
        }
        foreach (var preserve in new[] { false, true })
        {
            var xml = RuntimeOracle.Write(false, value.GetType(), value, new System.Runtime.Serialization.DataContractSerializerSettings { PreserveObjectReferences = preserve });
            foreach (var quota in new[] { {{minimumQuota}}, {{minimumQuota - 1}}, 1, 0, {{minimumQuota + 1}}, int.MaxValue })
            {
                var settings = new System.Runtime.Serialization.DataContractSerializerSettings { MaxItemsInObjectGraph = quota, PreserveObjectReferences = preserve };
                if (quota < {{minimumQuota}})
                {
                    foreach (var custom in new[] { false, true })
                    {
                        if ({{(read ? "true" : "false")}})
                            RuntimeOracle.ThrowsSerialization(() => RuntimeOracle.Read(custom, value.GetType(), xml, settings), "Read quota " + quota + " custom=" + custom);
                        else
                            RuntimeOracle.ThrowsSerialization(() => RuntimeOracle.Write(custom, value.GetType(), value, settings), "Write quota " + quota + " custom=" + custom);
                    }
                }
                else if ({{(read ? "true" : "false")}})
                {
                    Validate(RuntimeOracle.Read(false, value.GetType(), xml, settings));
                    Validate(RuntimeOracle.Read(true, value.GetType(), xml, settings));
                }
                else
                {
                    var expected = RuntimeOracle.Write(false, value.GetType(), value, settings);
                    var actual = RuntimeOracle.Write(true, value.GetType(), value, settings);
                    RuntimeOracle.SameXml(expected, actual);
                    Validate(RuntimeOracle.Read(false, value.GetType(), actual, settings));
                    Validate(RuntimeOracle.Read(true, value.GetType(), expected, settings));
                }
            }
        }
        """);

    [Fact]
    public Task DictionaryEntries_WithPreserveObjectReferences_ReadOwnAttributes() => AssertScenario(
        """
        [DataContract] public class Item { [DataMember] public int Value { get; set; } }
        [DataContract] public class Root
        {
            [DataMember(Order = 0)] public Dictionary<string, Item> Values { get; set; } = new();
            [DataMember(Order = 1)] public Item Shared { get; set; } = null!;
        }
        """,
        """
        var shared = new Item { Value = 42 };
        var original = new Root { Values = new Dictionary<string, Item> { ["one"] = shared, ["two"] = shared }, Shared = shared };
        var settings = new System.Runtime.Serialization.DataContractSerializerSettings { PreserveObjectReferences = true };
        var xml = RuntimeOracle.Write(false, typeof(Root), original, settings);
        foreach (var custom in new[] { false, true })
        {
            var result = (Root)RuntimeOracle.Read(custom, typeof(Root), xml, settings);
            RuntimeOracle.Check(result.Values.Count == 2 && result.Values["one"].Value == 42 && result.Values["two"].Value == 42, "Dictionary entry values must survive reference-preserving reads");
            RuntimeOracle.Check(ReferenceEquals(result.Shared, result.Values["one"]) && ReferenceEquals(result.Shared, result.Values["two"]), "Dictionary entries and following member must share the same deserialized object");
        }
        """);
}