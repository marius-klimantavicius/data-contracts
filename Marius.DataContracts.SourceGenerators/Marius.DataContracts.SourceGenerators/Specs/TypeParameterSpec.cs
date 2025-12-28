namespace Marius.DataContracts.SourceGenerators.Specs;

internal sealed class TypeParameterSpec : IEquatable<TypeParameterSpec>
{
    public required string Name { get; init; }
    public required int Ordinal { get; init; }
    public required bool IsMethodTypeParameter { get; init; }
    public EquatableArray<TypeConstraintSpec> Constraints { get; init; } = EquatableArray<TypeConstraintSpec>.Empty;
    public bool HasNotNullConstraint { get; init; }
    public bool HasUnmanagedConstraint { get; init; }
    public bool HasValueTypeConstraint { get; init; }
    public bool HasReferenceTypeConstraint { get; init; }
    public bool HasReferenceTypeConstraintNullable { get; init; }
    public bool HasConstructorConstraint { get; init; }

    public bool Equals(TypeParameterSpec? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return Name == other.Name &&
            Ordinal == other.Ordinal &&
            IsMethodTypeParameter == other.IsMethodTypeParameter &&
            Constraints.Equals(other.Constraints) &&
            HasNotNullConstraint == other.HasNotNullConstraint &&
            HasUnmanagedConstraint == other.HasUnmanagedConstraint &&
            HasValueTypeConstraint == other.HasValueTypeConstraint &&
            HasReferenceTypeConstraint == other.HasReferenceTypeConstraint &&
            HasReferenceTypeConstraintNullable == other.HasReferenceTypeConstraintNullable &&
            HasConstructorConstraint == other.HasConstructorConstraint;
    }

    public override bool Equals(object? obj) => Equals(obj as TypeParameterSpec);

    public override int GetHashCode()
    {
        unchecked
        {
            var hashCode = Name.GetHashCode();
            hashCode = (hashCode * 397) ^ Ordinal;
            hashCode = (hashCode * 397) ^ IsMethodTypeParameter.GetHashCode();
            hashCode = (hashCode * 397) ^ Constraints.GetHashCode();
            hashCode = (hashCode * 397) ^ HasNotNullConstraint.GetHashCode();
            hashCode = (hashCode * 397) ^ HasUnmanagedConstraint.GetHashCode();
            hashCode = (hashCode * 397) ^ HasValueTypeConstraint.GetHashCode();
            hashCode = (hashCode * 397) ^ HasReferenceTypeConstraint.GetHashCode();
            hashCode = (hashCode * 397) ^ HasReferenceTypeConstraintNullable.GetHashCode();
            hashCode = (hashCode * 397) ^ HasConstructorConstraint.GetHashCode();
            return hashCode;
        }
    }

    public static bool operator ==(TypeParameterSpec? left, TypeParameterSpec? right) => Equals(left, right);
    public static bool operator !=(TypeParameterSpec? left, TypeParameterSpec? right) => !Equals(left, right);
}