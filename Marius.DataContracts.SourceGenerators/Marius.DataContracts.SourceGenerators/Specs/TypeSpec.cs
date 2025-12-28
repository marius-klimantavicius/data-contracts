namespace Marius.DataContracts.SourceGenerators.Specs;

/// <summary>
/// Immutable, equatable representation of a type that is free from Roslyn symbols.
/// </summary>
internal sealed class TypeSpec : IEquatable<TypeSpec>
{
    public required string FullyQualifiedName { get; init; }
    public required string Name { get; init; }
    public required string Namespace { get; init; }
    public required bool IsValueType { get; init; }
    public required bool IsNullableValueType { get; init; }
    public required bool IsGenericType { get; init; }
    public required bool IsOpenGenericType { get; init; }
    public required bool IsArray { get; init; }
    public required bool IsAbstract { get; init; }
    public required bool IsTypeSerializable { get; init; } // only for value types
    public required SpecialTypeKind SpecialType { get; init; }
    public required TypeKindSpec TypeKind { get; init; }
    public TypeSpec? ElementType { get; init; }
    
    /// <summary>
    /// Type arguments for generic types.
    /// </summary>
    public EquatableArray<TypeSpec> TypeArguments { get; init; } = EquatableArray<TypeSpec>.Empty;

    public bool Equals(TypeSpec? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return FullyQualifiedName == other.FullyQualifiedName &&
               IsValueType == other.IsValueType &&
               IsNullableValueType == other.IsNullableValueType &&
               IsGenericType == other.IsGenericType &&
               IsOpenGenericType == other.IsOpenGenericType &&
               IsArray == other.IsArray &&
               IsAbstract == other.IsAbstract &&
               IsTypeSerializable == other.IsTypeSerializable &&
               SpecialType == other.SpecialType &&
               TypeKind == other.TypeKind &&
               Equals(ElementType, other.ElementType) &&
               TypeArguments.Equals(other.TypeArguments);
    }

    public override bool Equals(object? obj) => Equals(obj as TypeSpec);

    public override int GetHashCode()
    {
        unchecked
        {
            var hashCode = FullyQualifiedName.GetHashCode();
            hashCode = (hashCode * 397) ^ IsValueType.GetHashCode();
            hashCode = (hashCode * 397) ^ IsNullableValueType.GetHashCode();
            hashCode = (hashCode * 397) ^ IsGenericType.GetHashCode();
            hashCode = (hashCode * 397) ^ IsOpenGenericType.GetHashCode();
            hashCode = (hashCode * 397) ^ IsArray.GetHashCode();
            hashCode = (hashCode * 397) ^ IsAbstract.GetHashCode();
            hashCode = (hashCode * 397) ^ IsTypeSerializable.GetHashCode();
            hashCode = (hashCode * 397) ^ (int)SpecialType;
            hashCode = (hashCode * 397) ^ (int)TypeKind;
            hashCode = (hashCode * 397) ^ (ElementType?.GetHashCode() ?? 0);
            hashCode = (hashCode * 397) ^ TypeArguments.GetHashCode();
            return hashCode;
        }
    }

    public static bool operator ==(TypeSpec? left, TypeSpec? right) => Equals(left, right);
    public static bool operator !=(TypeSpec? left, TypeSpec? right) => !Equals(left, right);
    
    /// <summary>
    /// Returns "ref " if value type, empty string otherwise.
    /// </summary>
    public string MaybeRef() => IsValueType ? "ref " : "";
    
    /// <summary>
    /// Returns true if this type can be instantiated (is not an abstract class or interface).
    /// </summary>
    public bool CanBeInstantiated => !IsAbstract && TypeKind != TypeKindSpec.Interface;
}

internal enum SpecialTypeKind
{
    None = 0,
    Object,
    Boolean,
    Char,
    SByte,
    Byte,
    Int16,
    UInt16,
    Int32,
    UInt32,
    Int64,
    UInt64,
    Single,
    Double,
    Decimal,
    String,
    DateTime,
}

internal enum TypeKindSpec
{
    Unknown,
    Array,
    Class,
    Struct,
    Interface,
    Enum,
    Delegate,
}

