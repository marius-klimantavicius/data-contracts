using Microsoft.CodeAnalysis;

namespace Marius.DataContracts.SourceGenerators;

internal static class RoslynExtensions
{
    /// <summary>
    /// Checks if the compilation contains the given location's source tree.
    /// </summary>
    public static bool ContainsLocation(this Compilation compilation, Location location)
        => location.SourceTree != null && compilation.ContainsSyntaxTree(location.SourceTree);

    /// <summary>
    /// Gets the first location of the symbol, or null if the symbol has no locations.
    /// </summary>
    public static Location? GetLocation(this ISymbol symbol)
        => symbol.Locations.Length > 0 ? symbol.Locations[0] : null;
}

