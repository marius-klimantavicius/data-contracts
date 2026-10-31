using System.Threading.Tasks;
using Xunit;

namespace Marius.DataContracts.SourceGenerators.Tests;

public class ReviewDefaultValueTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    public Task StructDefault_UsesBoxedDefaultEqualsNotOperatorsOrIEquatable(int x) => ReviewRegression.AssertAsync(
        """
        [DataContract] public struct Point : IEquatable<Point> {
            public Point() { X = 99; }
            [DataMember] public int X { get; set; }
            public bool Equals(Point other) => true;
            public override bool Equals(object? other) => other is Point point && X == point.X;
            public override int GetHashCode() => X;
            public static bool operator ==(Point a, Point b) => true;
            public static bool operator !=(Point a, Point b) => false;
        }
        [DataContract] public class Root { [DataMember(EmitDefaultValue = false)] public Point Point { get; set; } }
        """,
        $$"""
        return ReviewOracle.Parity(new Root { Point = new Point { X = {{x}} } }, value =>
            ReviewOracle.Check(value.Point.X == {{x}}, "Point.X must survive both interoperability directions"));
        """);

    [Fact]
    public Task PrimitiveAndNullableDefaults_MatchDotNet() => ReviewRegression.AssertAsync(
        """
        [DataContract] public class Root {
            [DataMember(EmitDefaultValue = false)] public double Nan { get; set; } = double.NaN;
            [DataMember(EmitDefaultValue = false)] public float Zero { get; set; }
            [DataMember(EmitDefaultValue = false)] public int? Present { get; set; }
            [DataMember(EmitDefaultValue = false)] public int? Missing { get; set; }
            [DataMember(EmitDefaultValue = false)] public decimal Decimal { get; set; }
        }
        """,
        """
        return ReviewOracle.Parity(new Root { Zero = -0.0f, Present = 0 }, value => {
            ReviewOracle.Check(double.IsNaN(value.Nan), "NaN must be emitted");
            ReviewOracle.Check(value.Zero == 0 && value.Decimal == 0, "Numeric zero must be omitted");
            ReviewOracle.Check(value.Present.HasValue && value.Present.Value == 0 && value.Missing is null, "Nullable defaults must distinguish null and zero");
        });
        """);

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    public Task OrdinaryStruct_EmitDefaultValueFalse(int x) => ReviewRegression.AssertAsync(
        """
        [DataContract] public struct Point { [DataMember] public int X { get; set; } }
        [DataContract] public class Root { [DataMember(EmitDefaultValue = false)] public Point Point { get; set; } }
        """,
        $$"""
        return ReviewOracle.Parity(new Root { Point = new Point { X = {{x}} } }, value => ReviewOracle.Check(value.Point.X == {{x}}, "Point.X must survive both interoperability directions"));
        """);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task ReferenceDefault_IgnoresOverloadedInequality(bool required) => ReviewRegression.AssertAsync(
        $$"""
        [DataContract] public class Special {
            [DataMember] public int Value { get; set; }
            public static bool operator ==(Special? a, Special? b) => true;
            public static bool operator !=(Special? a, Special? b) => false;
            public override bool Equals(object? other) => true;
            public override int GetHashCode() => 0;
        }
        [DataContract] public class Root {
            [DataMember(EmitDefaultValue = false, IsRequired = {{required.ToString().ToLowerInvariant()}})] public Special? Value { get; set; }
        }
        """,
        """
        return ReviewOracle.Parity(new Root { Value = new Special { Value = 5 } }, value =>
            ReviewOracle.Check(value.Value is not null && value.Value.Value == 5, "Non-null Special must be emitted regardless of its operators"));
        """);

    [Fact]
    public Task RequiredDefaultStruct_ThrowsSameFailureAsDotNet() => ReviewRegression.AssertAsync(
        """
        [DataContract] public struct Point { [DataMember] public int X { get; set; } }
        [DataContract] public class Root { [DataMember(EmitDefaultValue = false, IsRequired = true)] public Point Point { get; set; } }
        """,
        """
        Type Failure(bool generated) {
            try {
                using var stream = new MemoryStream();
                if (generated) DataContractContext.GetSerializer<Root>().WriteObject(stream, new Root());
                else new System.Runtime.Serialization.DataContractSerializer(typeof(Root)).WriteObject(stream, new Root());
                throw new InvalidOperationException("Expected serialization failure");
            } catch (System.Runtime.Serialization.SerializationException e) { return e.GetType(); }
        }
        ReviewOracle.Check(Failure(false) == typeof(System.Runtime.Serialization.SerializationException), ".NET failure must be SerializationException");
        ReviewOracle.Check(Failure(true) == Failure(false), "Generated failure must match .NET");
        return new SerializationTestResultData { Success = true };
        """);
}
