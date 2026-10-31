using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace Marius.DataContracts.SourceGenerators.Tests;

public class ReviewContextNameTests
{
    private const string Contracts = """
        using System.Runtime.Serialization;
        namespace One { [DataContract] public class Model { [DataMember] public int Value; } }
        namespace Two { [DataContract] public class Model { [DataMember] public int Value; } }
        public class Outer { [DataContract] public class Model { [DataMember] public int Value; } }
        [DataContract] public class Plain { [DataMember] public int Value; }
        [DataContract] public class GetObjectDataContract1 { }
        [DataContract] public class Lower { }
        [DataContract] public class lower { }
        [DataContract] public class Item { [DataMember] public int Value; }
        [DataContract] public class ItemArray { [DataMember] public int Value; }
        [DataContract] public class A { }
        [DataContract] public class AB { }
        [DataContract] public class BC { }
        [DataContract] public class C { }
        [DataContract] public class Pair<T, U> { [DataMember] public T First; [DataMember] public U Second; }
        [DataContract] public class DataContracts { }
        namespace Collision { [DataContract] public class DataContractContext { } }
        [DataContract] public class GetSerializer { }
        [DataContract] public class Root {
            [DataMember] public Item[] Array;
            [DataMember] public Pair<A, BC> Left;
            [DataMember] public Pair<AB, C> Right;
        }
        """;

    [Fact]
    public void ContextNames_AreValidUniqueAndStableAcrossDeclarationOrder()
    {
        var (first, firstDriver) = ReviewGeneratorDriver.Generate(Contracts);
        ReviewGeneratorDriver.AssertCompiles(first);

        var tree = CSharpSyntaxTree.ParseText(Contracts);
        var root = (Microsoft.CodeAnalysis.CSharp.Syntax.CompilationUnitSyntax)tree.GetRoot();
        var reordered = root.WithMembers(SyntaxFactory.List(root.Members.Reverse())).ToFullString();
        var (second, secondDriver) = ReviewGeneratorDriver.Generate(reordered);
        ReviewGeneratorDriver.AssertCompiles(second);

        var fields = Fields(first);
        Assert.Equal(fields, Fields(second));
        Assert.Equal(fields.Length, fields.Select(field => field.Split(':')[0]).Distinct().Count());
        Assert.Contains(fields, field => field.StartsWith("Plain:"));
        Assert.Contains(fields, field => field.StartsWith("Int32:"));
        Assert.All(fields, field => Assert.True(SyntaxFacts.IsValidIdentifier(field.Split(':')[0])));
        Assert.Equal(firstDriver.GetRunResult().GeneratedTrees.Select(t => t.ToString()), secondDriver.GetRunResult().GeneratedTrees.Select(t => t.ToString()));

        return;

        string[] Fields(Compilation output) => output.GetTypeByMetadataName("Marius.DataContracts.Runtime.DataContractContext")!
            .GetMembers().OfType<IFieldSymbol>()
            .Where(field => field.Type is INamedTypeSymbol { IsGenericType: true })
            .Select(field => field.Name + ":" + field.Type.ToDisplayString()).OrderBy(name => name).ToArray();
    }

    [Fact]
    public System.Threading.Tasks.Task SameSimpleNames_ResolveTheRightSerializer() => ReviewRegression.AssertAsync(
        Contracts,
        """
        ReviewOracle.Parity(new One.Model { Value = 11 }, value => ReviewOracle.Check(value.Value == 11, "One.Model"));
        ReviewOracle.Parity(new Two.Model { Value = 22 }, value => ReviewOracle.Check(value.Value == 22, "Two.Model"));
        ReviewOracle.Parity(new Outer.Model { Value = 33 }, value => ReviewOracle.Check(value.Value == 33, "Outer.Model"));
        return ReviewOracle.Parity(new Root { Array = new[] { new Item { Value = 44 } },
            Left = new Pair<A, BC> { First = new A(), Second = new BC() },
            Right = new Pair<AB, C> { First = new AB(), Second = new C() } }, value => {
                ReviewOracle.Check(value.Array.Length == 1 && value.Array[0].Value == 44, "Array contract");
                ReviewOracle.Check(value.Left.First is A && value.Right.First is AB, "Generic contracts");
            });
        """);
}
