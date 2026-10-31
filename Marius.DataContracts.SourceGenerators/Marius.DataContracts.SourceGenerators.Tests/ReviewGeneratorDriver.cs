using System;
using System.Linq;
using Basic.Reference.Assemblies;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace Marius.DataContracts.SourceGenerators.Tests;

internal static class ReviewGeneratorDriver
{
    public static CSharpCompilation Compilation(string source) => CSharpCompilation.Create(
        "ReviewRegressionAssembly",
        [CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Latest))],
#if NET9_0
        Net90.References.All.Add
#else
        Net100.References.All.Add
#endif
        (MetadataReference.CreateFromFile(typeof(Runtime.DataContractSerializer).Assembly.Location)),
        new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true));

    public static GeneratorDriver Driver(bool track = false) => CSharpGeneratorDriver.Create(
        [new DataContractGenerator().AsSourceGenerator()],
        driverOptions: new GeneratorDriverOptions(IncrementalGeneratorOutputKind.None, trackIncrementalGeneratorSteps: track));

    public static (Compilation Output, GeneratorDriver Driver) Generate(string source)
    {
        var driver = Driver().RunGeneratorsAndUpdateCompilation(Compilation(source), out var output, out var diagnostics);
        var exception = driver.GetRunResult().Results.Single().Exception;
        Assert.True(exception is null, exception?.ToString());
        Assert.Empty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
        return (output, driver);
    }

    public static void AssertCompiles(Compilation output) => Assert.True(
        !output.GetDiagnostics().Any(d => d.Severity == DiagnosticSeverity.Error),
        string.Join(Environment.NewLine, output.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error)));
}
