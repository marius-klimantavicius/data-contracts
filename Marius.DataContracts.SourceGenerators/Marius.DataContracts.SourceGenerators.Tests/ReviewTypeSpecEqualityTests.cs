using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace Marius.DataContracts.SourceGenerators.Tests;

public class ReviewTypeSpecEqualityTests
{
    [Fact]
    public void IndependentlyCreatedSpecs_AreValueEqualAndDeduplicate()
    {
        // Exercise the actual internal value object without widening its API.
        var specType = typeof(DataContractGenerator).Assembly.GetType("Marius.DataContracts.SourceGenerators.Specs.TypeSpec", throwOnError: true)!;

        var left = Spec("global::Review.Leaf");
        var right = Spec("global::Review.Leaf");
        Assert.NotSame(left, right);
        Assert.True(left.Equals(right), "Equivalent leaf TypeSpecs with null ConstructedFrom must compare equal");
        Assert.True(right.Equals(left));
        Assert.Equal(left.GetHashCode(), right.GetHashCode());
        Assert.Single(new HashSet<object> { left, right });
        Assert.False(left.Equals(Spec("global::Review.Other")));

        var genericLeft = Spec("global::Review.Box<int>", Spec("global::Review.Box<T>"));
        var genericRight = Spec("global::Review.Box<int>", Spec("global::Review.Box<T>"));
        Assert.True(genericLeft.Equals(genericRight), "ConstructedFrom must compare recursively by value");
        Assert.Equal(genericLeft.GetHashCode(), genericRight.GetHashCode());
        Assert.False(genericLeft.Equals(Spec("global::Review.Box<int>")));
        Assert.False(Spec("global::Review.Box<int>").Equals(genericLeft));
        Assert.False(genericLeft.Equals(Spec("global::Review.Box<int>", Spec("global::Review.Other<T>"))));
        return;

        object Spec(string name, object? constructedFrom = null)
        {
            var result = Activator.CreateInstance(specType, nonPublic: true)!;
            specType.GetProperty("FullyQualifiedName")!.SetValue(result, name);
            specType.GetProperty("Name")!.SetValue(result, name);
            specType.GetProperty("Namespace")!.SetValue(result, "Review");
            specType.GetProperty("ConstructedFrom")!.SetValue(result, constructedFrom);
            return result;
        }
    }

    [Fact]
    public void UnrelatedCompilationEdit_ReusesOutputButContractEditRegenerates()
    {
        const string source = """
            using System.Runtime.Serialization;
            [DataContract] public class Root { [DataMember] public int Value; }
            """;
        var compilation = ReviewGeneratorDriver.Compilation(source);
        var driver = ReviewGeneratorDriver.Driver(track: true).RunGenerators(compilation);
        var firstSources = driver.GetRunResult().GeneratedTrees.Select(tree => tree.ToString()).ToArray();
        compilation = compilation.AddSyntaxTrees(CSharpSyntaxTree.ParseText("internal class Unrelated { int Answer => 42; }"));
        driver = driver.RunGenerators(compilation);
        
        var reused = driver.GetRunResult().Results.Single();
        Assert.Null(reused.Exception);
        Assert.Equal(firstSources, driver.GetRunResult().GeneratedTrees.Select(tree => tree.ToString()));
        
        var reasons = reused.TrackedOutputSteps.Values.SelectMany(steps => steps).SelectMany(step => step.Outputs).Select(output => output.Reason).ToArray();
        Assert.NotEmpty(reasons);
        Assert.All(reasons, reason => Assert.True(reason is IncrementalStepRunReason.Cached or IncrementalStepRunReason.Unchanged, "Unrelated edit must reuse equal specs; actual output reason=" + reason));

        compilation = compilation.ReplaceSyntaxTree(compilation.SyntaxTrees.First(), CSharpSyntaxTree.ParseText(source.Replace("int Value", "long Value")));
        driver = driver.RunGenerators(compilation);
        
        var changed = driver.GetRunResult().Results.Single();
        Assert.Null(changed.Exception);
        Assert.Contains(changed.TrackedOutputSteps.Values.SelectMany(steps => steps).SelectMany(step => step.Outputs), output => output.Reason is IncrementalStepRunReason.Modified or IncrementalStepRunReason.New);
        Assert.NotEqual(string.Join("\n", firstSources), string.Join("\n", driver.GetRunResult().GeneratedTrees.Select(tree => tree.ToString())));
        Assert.Contains("default(long)", string.Join("\n", driver.GetRunResult().GeneratedTrees.Select(tree => tree.ToString())));
    }
}
