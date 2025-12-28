namespace Marius.DataContracts.SourceGenerators.Specs;

/// <summary>
/// Immutable, equatable specification for a private accessor method.
/// </summary>
internal sealed class PrivateAccessorSpec : IEquatable<PrivateAccessorSpec>
{
    public required string Name { get; init; }
    public required PrivateAccessorKind Kind { get; init; }
    public required string TargetName { get; init; }
    public required TypeSpec ContainingType { get; init; }
    public TypeSpec? ReturnType { get; init; }
    public required EquatableArray<ParameterSpec> Parameters { get; init; }
    public required bool IsRegularConstructor { get; init; }

    public bool Equals(PrivateAccessorSpec? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return Name == other.Name &&
               Kind == other.Kind &&
               TargetName == other.TargetName &&
               ContainingType.Equals(other.ContainingType) &&
               Equals(ReturnType, other.ReturnType) &&
               Parameters.Equals(other.Parameters) &&
               IsRegularConstructor == other.IsRegularConstructor;
    }

    public override bool Equals(object? obj) => Equals(obj as PrivateAccessorSpec);

    public override int GetHashCode()
    {
        unchecked
        {
            var hashCode = Name.GetHashCode();
            hashCode = (hashCode * 397) ^ (int)Kind;
            hashCode = (hashCode * 397) ^ TargetName.GetHashCode();
            hashCode = (hashCode * 397) ^ ContainingType.GetHashCode();
            hashCode = (hashCode * 397) ^ (ReturnType?.GetHashCode() ?? 0);
            hashCode = (hashCode * 397) ^ Parameters.GetHashCode();
            hashCode = (hashCode * 397) ^ IsRegularConstructor.GetHashCode();
            return hashCode;
        }
    }

    public static bool operator ==(PrivateAccessorSpec? left, PrivateAccessorSpec? right) => Equals(left, right);
    public static bool operator !=(PrivateAccessorSpec? left, PrivateAccessorSpec? right) => !Equals(left, right);
}

internal enum PrivateAccessorKind
{
    Constructor,
    Method,
    Field,
}

/// <summary>
/// Immutable, equatable specification for a method parameter.
/// </summary>
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

