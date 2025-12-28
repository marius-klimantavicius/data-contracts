namespace Marius.DataContracts.SourceGenerators.Specs;

internal sealed class TypeConstraintSpec : IEquatable<TypeConstraintSpec>
{
    public required TypeConstraintKind Kind { get; init; }
    public string? ConstraintTypeFullName { get; init; }
    public bool IsGenericType { get; init; }
    public EquatableArray<string> TypeArguments { get; init; } = EquatableArray<string>.Empty;

    public bool Equals(TypeConstraintSpec? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return Kind == other.Kind &&
            ConstraintTypeFullName == other.ConstraintTypeFullName &&
            IsGenericType == other.IsGenericType &&
            TypeArguments.Equals(other.TypeArguments);
    }

    public override bool Equals(object? obj) => Equals(obj as TypeConstraintSpec);

    public override int GetHashCode()
    {
        unchecked
        {
            var hashCode = (int)Kind;
            hashCode = (hashCode * 397) ^ (ConstraintTypeFullName?.GetHashCode() ?? 0);
            hashCode = (hashCode * 397) ^ IsGenericType.GetHashCode();
            hashCode = (hashCode * 397) ^ TypeArguments.GetHashCode();
            return hashCode;
        }
    }

    public static bool operator ==(TypeConstraintSpec? left, TypeConstraintSpec? right) => Equals(left, right);
    public static bool operator !=(TypeConstraintSpec? left, TypeConstraintSpec? right) => !Equals(left, right);
}

internal enum TypeConstraintKind
{
    Type,
    TypeParameter,
}
