using Microsoft.CodeAnalysis;

namespace Marius.DataContracts.SourceGenerators;

internal readonly struct DiagnosticInfo : IEquatable<DiagnosticInfo>
{
    public DiagnosticDescriptor Descriptor { get; private init; }
    public object?[] MessageArgs { get; private init; }
    public Location? Location { get; private init; }
 
    public static DiagnosticInfo Create(DiagnosticDescriptor descriptor, Location? location, object?[]? messageArgs)
    {
        var trimmedLocation = location is null ? null : GetTrimmedLocation(location);
 
        return new DiagnosticInfo
        {
            Descriptor = descriptor,
            Location = trimmedLocation,
            MessageArgs = messageArgs ?? Array.Empty<object?>(),
        };
 
        // Creates a copy of the Location instance that does not capture a reference to Compilation.
        static Location GetTrimmedLocation(Location location)
            => Location.Create(location.SourceTree?.FilePath ?? "", location.SourceSpan, location.GetLineSpan().Span);
    }
 
    public Diagnostic CreateDiagnostic()
        => Diagnostic.Create(Descriptor, Location, MessageArgs);
 
    public override bool Equals(object? obj) => obj is DiagnosticInfo info && Equals(info);
 
    public bool Equals(DiagnosticInfo other)
    {
        return Descriptor.Equals(other.Descriptor) &&
            MessageArgs.SequenceEqual(other.MessageArgs) &&
            Location == other.Location;
    }
 
    public override int GetHashCode()
    {
        var helper = new HashCode();
        helper.Add(Descriptor.GetHashCode());
        foreach (var messageArg in MessageArgs) 
            helper.Add( messageArg?.GetHashCode() ?? 0);

        helper.Add(Location?.GetHashCode() ?? 0);
        return helper.ToHashCode();
    }
}