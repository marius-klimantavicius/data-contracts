using Marius.DataContracts.SourceGenerators.DataContracts;

namespace Marius.DataContracts.SourceGenerators.Specs;

/// <summary>
/// Immutable, equatable representation of a CollectionDataContract that is free from Roslyn symbols.
/// </summary>
internal sealed class CollectionDataContractSpec : DataContractSpec, IEquatable<CollectionDataContractSpec>
{
    public required CollectionKind CollectionKind { get; init; }
    public required string ItemName { get; init; }
    public required TypeSpec ItemType { get; init; }
    public required string CollectionItemName { get; init; }
    public required TypeSpec? CollectionElementType { get; init; }
    public string? KeyName { get; init; }
    public string? ValueName { get; init; }
    public bool IsDictionary => KeyName != null;
    
    /// <summary>
    /// Reference to item contract by ID.
    /// </summary>
    public required int ItemContractId { get; init; }
    
    /// <summary>
    /// Reference to shared type contract by ID. -1 if no shared type contract.
    /// </summary>
    public required int SharedTypeContractId { get; init; }
    
    public string? ChildElementNamespace { get; init; }
    
    /// <summary>
    /// Add method name if it's accessible, null if not.
    /// </summary>
    public required bool HasAddMethod { get; init; }

    public required EquatableArray<KnownDataContractSpec> KnownDataContracts { get; init; }

    public bool Equals(CollectionDataContractSpec? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        if (!base.Equals(other)) return false;
        return CollectionKind == other.CollectionKind &&
               ItemType.Equals(other.ItemType) &&
               ItemName == other.ItemName &&
               KeyName == other.KeyName &&
               ValueName == other.ValueName &&
               ItemContractId == other.ItemContractId &&
               SharedTypeContractId == other.SharedTypeContractId &&
               ChildElementNamespace == other.ChildElementNamespace &&
               HasAddMethod == other.HasAddMethod &&
               KnownDataContracts.Equals(other.KnownDataContracts);
    }

    public override bool Equals(DataContractSpec? other) => Equals(other as CollectionDataContractSpec);
    public override bool Equals(object? obj) => Equals(obj as CollectionDataContractSpec);

    public override int GetHashCode()
    {
        unchecked
        {
            var hashCode = base.GetHashCode();
            hashCode = (hashCode * 397) ^ (int)CollectionKind;
            hashCode = (hashCode * 397) ^ ItemType.GetHashCode();
            hashCode = (hashCode * 397) ^ ItemName.GetHashCode();
            hashCode = (hashCode * 397) ^ (KeyName?.GetHashCode() ?? 0);
            hashCode = (hashCode * 397) ^ (ValueName?.GetHashCode() ?? 0);
            hashCode = (hashCode * 397) ^ ItemContractId;
            hashCode = (hashCode * 397) ^ SharedTypeContractId;
            hashCode = (hashCode * 397) ^ (ChildElementNamespace?.GetHashCode() ?? 0);
            hashCode = (hashCode * 397) ^ HasAddMethod.GetHashCode();
            hashCode = (hashCode * 397) ^ KnownDataContracts.GetHashCode();
            return hashCode;
        }
    }
}

