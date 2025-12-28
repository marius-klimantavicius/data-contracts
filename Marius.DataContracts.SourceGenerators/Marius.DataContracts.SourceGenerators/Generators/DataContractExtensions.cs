using Microsoft.CodeAnalysis;

namespace Marius.DataContracts.SourceGenerators;

internal static class DataContractExtensions
{
    public static bool IsAccessible(this ISymbol symbol)
    {
        return symbol.DeclaredAccessibility == Accessibility.Public
            || symbol.DeclaredAccessibility == Accessibility.Internal
            || symbol.DeclaredAccessibility == Accessibility.ProtectedOrInternal;
    }

    public static string MaybeRef(this ITypeSymbol type)
    {
        if (type.IsValueType)
            return "ref ";

        return "";
    }
}