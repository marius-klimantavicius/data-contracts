using Microsoft.CodeAnalysis.CSharp;

namespace Marius.DataContracts.SourceGenerators.Generators;

internal static class CSharpIdentifier
{
    // Use only for C# identifiers. XML and UnsafeAccessor metadata names retain
    // their original spelling without the C# verbatim-identifier prefix.
    public static string Escape(string name)
    {
        if (SyntaxFacts.GetKeywordKind(name) != SyntaxKind.None || SyntaxFacts.GetContextualKeywordKind(name) != SyntaxKind.None)
            return "@" + name;

        return name;
    }
}
