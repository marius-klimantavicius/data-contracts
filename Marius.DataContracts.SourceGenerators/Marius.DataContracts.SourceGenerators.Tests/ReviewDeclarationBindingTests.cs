using System.Threading.Tasks;
using Xunit;

namespace Marius.DataContracts.SourceGenerators.Tests;

public class ReviewDeclarationBindingTests
{
    [Theory]
    [InlineData("{ get; set; }")]
    [InlineData(";")]
    public Task HiddenMembers_ReadDotNetXmlIntoBothDeclarations(string member) => ReviewRegression.AssertAsync(
        $$"""
        [DataContract] public class Base { [DataMember] public int Value {{member}} }
        [DataContract] public class Derived : Base { [DataMember] public new int Value {{member}} }
        """,
        """
        var original = new Derived { Value = 22 };
        ((Base)original).Value = 11;
        using var stream = new MemoryStream();
        new System.Runtime.Serialization.DataContractSerializer(typeof(Derived)).WriteObject(stream, original);
        stream.Position = 0;
        var value = (Derived)DataContractContext.GetSerializer<Derived>().ReadObject(stream)!;
        ReviewOracle.Check(((Base)value).Value == 11, "Base.Value from .NET XML must be 11; actual=" + ((Base)value).Value);
        ReviewOracle.Check(value.Value == 22, "Derived.Value from .NET XML must be 22");
        return new SerializationTestResultData { Success = true };
        """);

    [Theory]
    [InlineData("{ get; set; }")]
    [InlineData(";")]
    public Task HiddenMembers_PreserveBothDeclarations(string member) => ReviewRegression.AssertAsync(
        $$"""
        [DataContract] public class Base { [DataMember] public int Value {{member}} }
        [DataContract] public class Derived : Base { [DataMember] public new int Value {{member}} }
        """,
        """
        var original = new Derived { Value = 22 };
        ((Base)original).Value = 11;
        return ReviewOracle.Parity(original, value => {
            ReviewOracle.Check(((Base)value).Value == 11, "Base.Value must be 11");
            ReviewOracle.Check(value.Value == 22, "Derived.Value must be 22");
        });
        """);

    [Fact]
    public Task InheritedVirtualMember_PreservesDotNetDispatch() => ReviewRegression.AssertAsync(
        """
        [DataContract] public class Base {
            [DataMember] public virtual int Value { get; set; }
        }
        [DataContract] public class Derived : Base {
            public override int Value { get => base.Value + 100; set => base.Value = value - 100; }
            public int StoredValue => base.Value;
        }
        """,
        """
        var original = new Derived { Value = 122 };
        return ReviewOracle.Parity(original, value => {
            ReviewOracle.Check(value.Value == 122 && ((Base)value).Value == 122, "Virtual getter must dispatch");
            ReviewOracle.Check(value.StoredValue == 22, "Virtual setter must dispatch");
        });
        """);
}
