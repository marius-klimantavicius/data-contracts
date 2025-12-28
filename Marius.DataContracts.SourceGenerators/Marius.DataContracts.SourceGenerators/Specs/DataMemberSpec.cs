namespace Marius.DataContracts.SourceGenerators.Specs;

/// <summary>
/// Immutable, equatable representation of a DataMember that is free from Roslyn symbols.
/// </summary>
internal sealed class DataMemberSpec : IEquatable<DataMemberSpec>
{
    public required string Name { get; init; }
    public required bool IsRequired { get; init; }
    public required bool IsNullable { get; init; }
    public required bool EmitDefaultValue { get; init; }
    public required long Order { get; init; }
    public required bool IsGetOnlyCollection { get; init; }
    public required MemberSpec MemberInfo { get; init; }
    public required TypeSpec MemberType { get; init; }
    public required int MemberTypeContractId { get; init; }
    public required DataMemberSpec? ConflictingMember { get; init; }
    public string? PrimitiveReadMethodName { get; init; }
    public string? PrimitiveWriteMethodName { get; init; }

    public bool Equals(DataMemberSpec? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return Name == other.Name &&
               IsRequired == other.IsRequired &&
               IsNullable == other.IsNullable &&
               EmitDefaultValue == other.EmitDefaultValue &&
               Order == other.Order &&
               IsGetOnlyCollection == other.IsGetOnlyCollection &&
               MemberInfo.Equals(other.MemberInfo) &&
               MemberType.Equals(other.MemberType) &&
               MemberTypeContractId == other.MemberTypeContractId &&
               ConflictingMember == other.ConflictingMember &&
               PrimitiveReadMethodName == other.PrimitiveReadMethodName &&
               PrimitiveWriteMethodName == other.PrimitiveWriteMethodName;
    }

    public override bool Equals(object? obj) => Equals(obj as DataMemberSpec);

    public override int GetHashCode()
    {
        unchecked
        {
            var hashCode = Name.GetHashCode();
            hashCode = (hashCode * 397) ^ IsRequired.GetHashCode();
            hashCode = (hashCode * 397) ^ IsNullable.GetHashCode();
            hashCode = (hashCode * 397) ^ EmitDefaultValue.GetHashCode();
            hashCode = (hashCode * 397) ^ Order.GetHashCode();
            hashCode = (hashCode * 397) ^ IsGetOnlyCollection.GetHashCode();
            hashCode = (hashCode * 397) ^ MemberInfo.GetHashCode();
            hashCode = (hashCode * 397) ^ MemberType.GetHashCode();
            hashCode = (hashCode * 397) ^ MemberTypeContractId;
            hashCode = (hashCode * 397) ^ (ConflictingMember?.GetHashCode() ?? 0);
            hashCode = (hashCode * 397) ^ (PrimitiveReadMethodName?.GetHashCode() ?? 0);
            hashCode = (hashCode * 397) ^ (PrimitiveWriteMethodName?.GetHashCode() ?? 0);
            return hashCode;
        }
    }

    public static bool operator ==(DataMemberSpec? left, DataMemberSpec? right) => Equals(left, right);
    public static bool operator !=(DataMemberSpec? left, DataMemberSpec? right) => !Equals(left, right);
}

