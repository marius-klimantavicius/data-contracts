namespace Marius.DataContracts.SourceGenerators.Specs;

/// <summary>
/// Immutable, equatable representation of a member (property or field) that is free from Roslyn symbols.
/// </summary>
internal sealed class MemberSpec : IEquatable<MemberSpec>
{
    public required string Name { get; init; }
    public required TypeSpec DeclaringType { get; init; }
    public required TypeSpec MemberType { get; init; }
    public required MemberKindSpec Kind { get; init; }
    public required bool IsAccessible { get; init; }
    public required bool HasAccessibleGetter { get; init; }
    public required bool HasAccessibleSetter { get; init; }
    public required bool IsSetterInitOnly { get; init; }
    
    /// <summary>
    /// Accessor method name for private access (if needed).
    /// </summary>
    public string? GetterAccessorName { get; init; }
    public string? SetterAccessorName { get; init; }

    public bool Equals(MemberSpec? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return Name == other.Name &&
               DeclaringType.Equals(other.DeclaringType) &&
               MemberType.Equals(other.MemberType) &&
               Kind == other.Kind &&
               IsAccessible == other.IsAccessible &&
               HasAccessibleGetter == other.HasAccessibleGetter &&
               HasAccessibleSetter == other.HasAccessibleSetter &&
               IsSetterInitOnly == other.IsSetterInitOnly &&
               GetterAccessorName == other.GetterAccessorName &&
               SetterAccessorName == other.SetterAccessorName;
    }

    public override bool Equals(object? obj) => Equals(obj as MemberSpec);

    public override int GetHashCode()
    {
        unchecked
        {
            var hashCode = Name.GetHashCode();
            hashCode = (hashCode * 397) ^ DeclaringType.GetHashCode();
            hashCode = (hashCode * 397) ^ MemberType.GetHashCode();
            hashCode = (hashCode * 397) ^ (int)Kind;
            hashCode = (hashCode * 397) ^ IsAccessible.GetHashCode();
            hashCode = (hashCode * 397) ^ HasAccessibleGetter.GetHashCode();
            hashCode = (hashCode * 397) ^ HasAccessibleSetter.GetHashCode();
            hashCode = (hashCode * 397) ^ IsSetterInitOnly.GetHashCode();
            hashCode = (hashCode * 397) ^ (GetterAccessorName?.GetHashCode() ?? 0);
            hashCode = (hashCode * 397) ^ (SetterAccessorName?.GetHashCode() ?? 0);
            return hashCode;
        }
    }

    public static bool operator ==(MemberSpec? left, MemberSpec? right) => Equals(left, right);
    public static bool operator !=(MemberSpec? left, MemberSpec? right) => !Equals(left, right);
}

internal enum MemberKindSpec
{
    Property,
    Field,
}

