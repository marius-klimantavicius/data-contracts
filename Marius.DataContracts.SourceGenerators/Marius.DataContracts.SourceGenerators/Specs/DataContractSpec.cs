namespace Marius.DataContracts.SourceGenerators.Specs;

/// <summary>
/// Immutable, equatable base class for data contract specifications that are free from Roslyn symbols.
/// </summary>
internal abstract class DataContractSpec : IEquatable<DataContractSpec>
{
    public required int Id { get; init; }
    public required string GeneratedName { get; init; }
    public required string TypeInfoPropertyName { get; init; }
    public required TypeSpec UnderlyingType { get; init; }
    public required TypeSpec OriginalUnderlyingType { get; init; }
    public required string Name { get; init; }
    public required string Namespace { get; init; }
    public required string XmlName { get; init; }
    public required string XmlNamespace { get; init; }
    public required bool IsPrimitive { get; init; }
    public required bool IsReference { get; init; }
    public required bool IsValueType { get; init; }
    public required bool IsISerializable { get; init; }
    public required bool HasRoot { get; init; }
    public required int BaseContractId { get; init; }
    public required bool CanContainReferences { get; init; }
    public required bool IsBuiltInDataContract { get; init; }
    public required string? TopLevelElementName { get; init; }
    public required string? TopLevelElementNamespace { get; init; }
    public required DataContractKindSpec Kind { get; init; }

    public virtual bool Equals(DataContractSpec? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;

        return Id == other.Id &&
            GeneratedName == other.GeneratedName &&
            UnderlyingType.Equals(other.UnderlyingType) &&
            OriginalUnderlyingType.Equals(other.OriginalUnderlyingType) &&
            Name == other.Name &&
            Namespace == other.Namespace &&
            XmlName == other.XmlName &&
            XmlNamespace == other.XmlNamespace &&
            IsPrimitive == other.IsPrimitive &&
            IsReference == other.IsReference &&
            IsValueType == other.IsValueType &&
            IsISerializable == other.IsISerializable &&
            HasRoot == other.HasRoot &&
            BaseContractId == other.BaseContractId &&
            CanContainReferences == other.CanContainReferences &&
            IsBuiltInDataContract == other.IsBuiltInDataContract &&
            TopLevelElementName == other.TopLevelElementName &&
            TopLevelElementNamespace == other.TopLevelElementNamespace &&
            Kind == other.Kind
            ;
    }

    public override bool Equals(object? obj) => Equals(obj as DataContractSpec);

    public override int GetHashCode()
    {
        unchecked
        {
            var hashCode = Id;
            hashCode = (hashCode * 397) ^ GeneratedName.GetHashCode();
            hashCode = (hashCode * 397) ^ UnderlyingType.GetHashCode();
            hashCode = (hashCode * 397) ^ OriginalUnderlyingType.GetHashCode();
            hashCode = (hashCode * 397) ^ Name.GetHashCode();
            hashCode = (hashCode * 397) ^ Namespace.GetHashCode();
            hashCode = (hashCode * 397) ^ XmlName.GetHashCode();
            hashCode = (hashCode * 397) ^ XmlNamespace.GetHashCode();
            hashCode = (hashCode * 397) ^ IsPrimitive.GetHashCode();
            hashCode = (hashCode * 397) ^ IsReference.GetHashCode();
            hashCode = (hashCode * 397) ^ IsValueType.GetHashCode();
            hashCode = (hashCode * 397) ^ IsISerializable.GetHashCode();
            hashCode = (hashCode * 397) ^ BaseContractId.GetHashCode();
            hashCode = (hashCode * 397) ^ CanContainReferences.GetHashCode();
            hashCode = (hashCode * 397) ^ IsBuiltInDataContract.GetHashCode();
            hashCode = (hashCode * 397) ^ (TopLevelElementName?.GetHashCode() ?? 0);
            hashCode = (hashCode * 397) ^ (TopLevelElementNamespace?.GetHashCode() ?? 0);
            hashCode = (hashCode * 397) ^ (int)Kind;
            return hashCode;
        }
    }

    public static bool operator ==(DataContractSpec? left, DataContractSpec? right) => Equals(left, right);
    public static bool operator !=(DataContractSpec? left, DataContractSpec? right) => !Equals(left, right);
}

internal enum DataContractKindSpec
{
    Class,
    Collection,
    Enum,
    Primitive,
    Xml,
}