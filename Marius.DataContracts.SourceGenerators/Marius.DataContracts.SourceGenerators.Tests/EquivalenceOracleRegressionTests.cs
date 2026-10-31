using System.Threading.Tasks;
using Xunit;

namespace Marius.DataContracts.SourceGenerators.Tests;

public class EquivalenceOracleRegressionTests
{
    private static async Task AssertOracle(string contracts, string expression, bool expected)
    {
        var result = await new SerializationTestCompiler().CompileAndRunAsync(contracts, $$"""
            var method = typeof(SerializationTestRunner).GetMethod("{{(expression.StartsWith("XElement") ? "AreXmlSemanticallyEquivalent" : "ObjectsAreEqual")}}", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            bool actual = (bool)method.Invoke(null, new object[] { {{expression}} });
            return new SerializationTestResultData { Success = true, DeserializedEqual = actual == {{(expected ? "true" : "false")}} };
            """);
        Assert.True(result.Success, result.ErrorMessage);
        Assert.True(result.DeserializedEqual, $"Oracle returned the wrong result for {expression}");
    }

    [Fact]
    public Task DetectsPublicFieldState() => AssertOracle(
        "[DataContract] public class State { [DataMember] public int Value; }",
        "new State { Value = 1 }, new State { Value = 2 }", false);

    [Fact]
    public Task DetectsPrivateDataMemberState() => AssertOracle(
        "[DataContract] public class State { [DataMember] private int value; public State(int n) { value = n; } }",
        "new State(1), new State(2)", false);

    [Fact]
    public Task DetectsHiddenBaseState() => AssertOracle(
        """
        [DataContract] public class Base { [DataMember] public int Value { get; set; } }
        [DataContract] public class State : Base { [DataMember] public new int Value { get; set; } public State(int n) { ((Base)this).Value = n; Value = 5; } }
        """, "new State(1), new State(2)", false);

    [Fact]
    public Task IgnoresNonContractPropertiesAndUserEquality() => AssertOracle(
        """
        [DataContract] public class State : IEquatable<State> {
            [DataMember] public int Value { get; set; }
            public int Ignored { get; set; }
            public bool Equals(State other) => true;
        }
        """, "new State { Value = 1 }, new State { Value = 2 }", false);

    [Fact]
    public Task DetectsLostAliases() => AssertOracle(
        """
        [DataContract] public class Node { [DataMember] public int Value { get; set; } }
        [DataContract] public class State {
            [DataMember] public Node A { get; set; }
            [DataMember] public Node B { get; set; }
            public State(bool shared) { A = new Node { Value = 1 }; B = shared ? A : new Node { Value = 1 }; }
        }
        """, "new State(true), new State(false)", false);

    [Fact]
    public Task DetectsDictionaryValueState() => AssertOracle(
        "[DataContract] public class Marker {}",
        "new Dictionary<string, int> { [\"key\"] = 1 }, new Dictionary<string, int> { [\"key\"] = 2 }", false);

    [Fact]
    public Task DetectsDateTimeKindState() => AssertOracle(
        "[DataContract] public class Marker {}",
        "new DateTime(638000000000000000, DateTimeKind.Utc), new DateTime(638000000000000000, DateTimeKind.Unspecified)", false);

    [Theory]
    [InlineData("<r> value </r>", "<r>value</r>", false)]
    [InlineData("<r>before<c/>after</r>", "<r>changed<c/>after</r>", false)]
    [InlineData("<r xmlns:i='http://www.w3.org/2001/XMLSchema-instance' xmlns:a='urn:x' i:type='a:Model'/>", "<r xmlns:i='http://www.w3.org/2001/XMLSchema-instance' xmlns:b='urn:x' i:type='b:Model'/>", true)]
    [InlineData("<r xmlns:i='http://www.w3.org/2001/XMLSchema-instance' xmlns:a='urn:x' i:type='a:Model'/>", "<r xmlns:i='http://www.w3.org/2001/XMLSchema-instance' xmlns:a='urn:y' i:type='a:Model'/>", false)]
    public Task ComparesXmlTextAndExpandedTypeNames(string left, string right, bool expected) =>
        AssertOracle("[DataContract] public class Marker {}", $"XElement.Parse(\"{left}\", LoadOptions.PreserveWhitespace), XElement.Parse(\"{right}\", LoadOptions.PreserveWhitespace)", expected);

    [Fact]
    public async Task HandlesCyclesAndPreservesConfiguredIdentity()
    {
        var result = await new SerializationTestCompiler().CompileAndRunAsync(
            "[DataContract] public class Node { [DataMember] public Node Next { get; set; } [DataMember] public List<Node> Items { get; set; } }",
            """
            var node = new Node(); node.Next = node; node.Items = new List<Node> { node, node };
            return SerializationTestRunner.RunTest(node, typeof(Node), new System.Runtime.Serialization.DataContractSerializerSettings { PreserveObjectReferences = true });
            """);
        Assert.True(result.Success, result.ErrorMessage);
        Assert.True(result.XmlEquivalent);
        Assert.True(result.DeserializedEqual, result.DeserializationDetails);
    }

    [Fact]
    public async Task UsesFrameworkRoundtripAsStateOracle()
    {
        var result = await new SerializationTestCompiler().CompileAndRunAsync(
            "[DataContract] public class State { [DataMember] public IEnumerable<int> Items { get; set; } public int Ignored { get; set; } }",
            "return SerializationTestRunner.RunTest(new State { Items = new List<int> { 1, 2 }, Ignored = 99 }, typeof(State));");
        Assert.True(result.Success, result.ErrorMessage);
        Assert.True(result.XmlEquivalent);
        Assert.True(result.DeserializedEqual, result.DeserializationDetails);
    }
}
