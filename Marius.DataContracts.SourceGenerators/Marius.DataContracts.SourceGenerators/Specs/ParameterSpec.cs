namespace Marius.DataContracts.SourceGenerators.Specs;

internal sealed class ParameterSpec : IEquatable<ParameterSpec>
{
    public required string Name { get; init; }
    public required TypeSpec Type { get; init; }
    public required ParameterRefKind RefKind { get; init; }

    public bool Equals(ParameterSpec? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;

        return Name == other.Name &&
            Type.Equals(other.Type) &&
            RefKind == other.RefKind;
    }

    public override bool Equals(object? obj) => Equals(obj as ParameterSpec);

    public override int GetHashCode()
    {
        unchecked
        {
            var hashCode = Name.GetHashCode();
            hashCode = (hashCode * 397) ^ Type.GetHashCode();
            hashCode = (hashCode * 397) ^ (int)RefKind;
            return hashCode;
        }
    }

    public static bool operator ==(ParameterSpec? left, ParameterSpec? right) => Equals(left, right);
    public static bool operator !=(ParameterSpec? left, ParameterSpec? right) => !Equals(left, right);
}

internal enum ParameterRefKind
{
    None,
    Ref,
    Out,
    In,
}