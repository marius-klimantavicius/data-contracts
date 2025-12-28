namespace Marius.DataContracts.SourceGenerators.Specs;

/// <summary>
/// Immutable, equatable representation of a ClassDataContract that is free from Roslyn symbols.
/// </summary>
internal sealed class ClassDataContractSpec : DataContractSpec, IEquatable<ClassDataContractSpec>
{
    public required EquatableArray<string> ContractNamespaces { get; init; }
    public required EquatableArray<string> MemberNames { get; init; }
    public required EquatableArray<string> MemberNamespaces { get; init; }
    public required EquatableArray<DataMemberSpec> Members { get; init; }
    public required EquatableArray<KnownDataContractSpec> KnownDataContracts { get; init; }
    public required EquatableArray<string?> ChildElementNamespaces { get; init; }

    public string? SerializationExceptionMessage { get; init; }
    public string? DeserializationExceptionMessage { get; init; }

    public bool IsReadOnlyContract => DeserializationExceptionMessage != null;
    public required bool IsNonAttributedType { get; init; }
    public required bool HasExtensionData { get; init; }
    public required bool IsDbNull { get; init; }

    /// <summary>
    /// Reference to base class contract by ID. -1 if no base contract.
    /// </summary>
    public required int BaseClassContractId { get; init; }

    /// <summary>
    /// Whether this type has a parameterless constructor.
    /// </summary>
    public required bool HasParameterlessConstructor { get; init; }

    /// <summary>
    /// If the constructor is inaccessible, the accessor method name.
    /// </summary>
    public string? ConstructorAccessorName { get; init; }

    /// <summary>
    /// Whether this type implements IObjectReference for factory method pattern.
    /// </summary>
    public required bool HasFactoryMethod { get; init; }

    /// <summary>
    /// Whether this type implements IDeserializationCallback.
    /// </summary>
    public required bool HasDeserializationCallback { get; init; }

    /// <summary>
    /// ISerializable constructor accessor name (if needed).
    /// </summary>
    public string? ISerializableConstructorAccessorName { get; init; }


    public bool Equals(ClassDataContractSpec? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        if (!base.Equals(other)) return false;

        return ContractNamespaces.Equals(other.ContractNamespaces) &&
            MemberNames.Equals(other.MemberNames) &&
            MemberNamespaces.Equals(other.MemberNamespaces) &&
            Members.Equals(other.Members) &&
            KnownDataContracts.Equals(other.KnownDataContracts) &&
            SerializationExceptionMessage == other.SerializationExceptionMessage &&
            DeserializationExceptionMessage == other.DeserializationExceptionMessage &&
            IsNonAttributedType == other.IsNonAttributedType &&
            HasExtensionData == other.HasExtensionData &&
            IsDbNull == other.IsDbNull &&
            BaseClassContractId == other.BaseClassContractId &&
            HasParameterlessConstructor == other.HasParameterlessConstructor &&
            ConstructorAccessorName == other.ConstructorAccessorName &&
            HasFactoryMethod == other.HasFactoryMethod &&
            HasDeserializationCallback == other.HasDeserializationCallback &&
            ISerializableConstructorAccessorName == other.ISerializableConstructorAccessorName;
    }

    public override bool Equals(DataContractSpec? other) => Equals(other as ClassDataContractSpec);
    public override bool Equals(object? obj) => Equals(obj as ClassDataContractSpec);

    public override int GetHashCode()
    {
        unchecked
        {
            var hashCode = base.GetHashCode();
            hashCode = (hashCode * 397) ^ ContractNamespaces.GetHashCode();
            hashCode = (hashCode * 397) ^ MemberNames.GetHashCode();
            hashCode = (hashCode * 397) ^ MemberNamespaces.GetHashCode();
            hashCode = (hashCode * 397) ^ Members.GetHashCode();
            hashCode = (hashCode * 397) ^ KnownDataContracts.GetHashCode();
            hashCode = (hashCode * 397) ^ (SerializationExceptionMessage?.GetHashCode() ?? 0);
            hashCode = (hashCode * 397) ^ (DeserializationExceptionMessage?.GetHashCode() ?? 0);
            hashCode = (hashCode * 397) ^ IsNonAttributedType.GetHashCode();
            hashCode = (hashCode * 397) ^ HasExtensionData.GetHashCode();
            hashCode = (hashCode * 397) ^ IsDbNull.GetHashCode();
            hashCode = (hashCode * 397) ^ BaseClassContractId;
            hashCode = (hashCode * 397) ^ HasParameterlessConstructor.GetHashCode();
            hashCode = (hashCode * 397) ^ (ConstructorAccessorName?.GetHashCode() ?? 0);
            hashCode = (hashCode * 397) ^ HasFactoryMethod.GetHashCode();
            hashCode = (hashCode * 397) ^ HasDeserializationCallback.GetHashCode();
            hashCode = (hashCode * 397) ^ (ISerializableConstructorAccessorName?.GetHashCode() ?? 0);
            return hashCode;
        }
    }
}