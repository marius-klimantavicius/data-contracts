namespace Marius.DataContracts.SourceGenerators.Specs;

/// <summary>
/// Immutable, equatable representation of an EnumDataContract that is free from Roslyn symbols.
/// </summary>
internal sealed class EnumDataContractSpec : DataContractSpec, IEquatable<EnumDataContractSpec>
{
    public required EquatableArray<DataMemberSpec> Members { get; init; }
    public required EquatableArray<long> Values { get; init; }
    public required bool IsFlags { get; init; }
    public required bool IsULong { get; init; }
    public required EquatableArray<string> ChildElementNames { get; init; }
    public required string BaseContractXmlName { get; init; }
    public required string BaseContractXmlNamespace { get; init; }

    public bool Equals(EnumDataContractSpec? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        if (!base.Equals(other)) return false;
        return Members.Equals(other.Members) &&
               Values.Equals(other.Values) &&
               IsFlags == other.IsFlags &&
               IsULong == other.IsULong &&
               ChildElementNames.Equals(other.ChildElementNames) &&
               BaseContractId == other.BaseContractId &&
               BaseContractXmlName == other.BaseContractXmlName &&
               BaseContractXmlNamespace == other.BaseContractXmlNamespace;
    }

    public override bool Equals(DataContractSpec? other) => Equals(other as EnumDataContractSpec);
    public override bool Equals(object? obj) => Equals(obj as EnumDataContractSpec);

    public override int GetHashCode()
    {
        unchecked
        {
            var hashCode = base.GetHashCode();
            hashCode = (hashCode * 397) ^ Members.GetHashCode();
            hashCode = (hashCode * 397) ^ Values.GetHashCode();
            hashCode = (hashCode * 397) ^ IsFlags.GetHashCode();
            hashCode = (hashCode * 397) ^ IsULong.GetHashCode();
            hashCode = (hashCode * 397) ^ ChildElementNames.GetHashCode();
            hashCode = (hashCode * 397) ^ BaseContractId;
            hashCode = (hashCode * 397) ^ BaseContractXmlName.GetHashCode();
            hashCode = (hashCode * 397) ^ BaseContractXmlNamespace.GetHashCode();
            return hashCode;
        }
    }
}

