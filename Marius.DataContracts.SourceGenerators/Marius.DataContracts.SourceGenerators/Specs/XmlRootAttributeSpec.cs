namespace Marius.DataContracts.SourceGenerators.Specs;

/// <summary>
/// Immutable, equatable representation of an XmlRootAttribute that is free from Roslyn symbols.
/// </summary>
internal sealed class XmlRootAttributeSpec : IEquatable<XmlRootAttributeSpec>
{
    public string? ElementName { get; init; }
    public string? Namespace { get; init; }
    public bool IsNullable { get; init; }
    public string? DataType { get; init; }

    public bool Equals(XmlRootAttributeSpec? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;

        return ElementName == other.ElementName &&
               Namespace == other.Namespace &&
               IsNullable == other.IsNullable &&
               DataType == other.DataType;
    }

    public override bool Equals(object? obj) => Equals(obj as XmlRootAttributeSpec);

    public override int GetHashCode()
    {
        unchecked
        {
            var hashCode = ElementName?.GetHashCode() ?? 0;
            hashCode = (hashCode * 397) ^ (Namespace?.GetHashCode() ?? 0);
            hashCode = (hashCode * 397) ^ IsNullable.GetHashCode();
            hashCode = (hashCode * 397) ^ (DataType?.GetHashCode() ?? 0);
            return hashCode;
        }
    }

    public static bool operator ==(XmlRootAttributeSpec? left, XmlRootAttributeSpec? right) => Equals(left, right);
    public static bool operator !=(XmlRootAttributeSpec? left, XmlRootAttributeSpec? right) => !Equals(left, right);
}

