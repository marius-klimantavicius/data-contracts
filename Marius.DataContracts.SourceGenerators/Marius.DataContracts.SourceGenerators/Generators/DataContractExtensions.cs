using Microsoft.CodeAnalysis;

namespace Marius.DataContracts.SourceGenerators.Generators;

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
    
    public static List<ITypeSymbol>? GetAllTypeArgumentsInScope(this INamedTypeSymbol type)
    {
        if (!type.IsGenericType)
        {
            return null;
        }
 
        List<ITypeSymbol>? args = null;
        TraverseContainingTypes(type);
        return args;
 
        void TraverseContainingTypes(INamedTypeSymbol current)
        {
            if (current.ContainingType is INamedTypeSymbol parent)
            {
                TraverseContainingTypes(parent);
            }
 
            if (!current.TypeArguments.IsEmpty)
            {
                (args ??= new()).AddRange(current.TypeArguments);
            }
        }
    }
}