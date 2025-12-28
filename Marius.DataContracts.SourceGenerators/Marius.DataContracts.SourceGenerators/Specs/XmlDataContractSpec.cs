namespace Marius.DataContracts.SourceGenerators.Specs;

/// <summary>
/// Immutable, equatable representation of an XmlDataContract that is free from Roslyn symbols.
/// </summary>
internal sealed class XmlDataContractSpec : DataContractSpec, IEquatable<XmlDataContractSpec>
{
    public required bool IsAny { get; init; }
    public string? SchemaProviderMethod { get; init; }
    public string? SchemaProviderMethodAccessorName { get; init; }
    public bool? SchemaProviderMethodIsXmlSchemaType { get; init; }
    public string? ConstructorAccessorName { get; init; }
    public required bool IsXElement { get; init; }
    public required bool IsXmlElementOrXmlNodeArray { get; init; }
    public required EquatableArray<KnownDataContractSpec> KnownDataContracts { get; init; }
    public XmlRootAttributeSpec? XmlRootAttribute { get; init; }

    public bool Equals(XmlDataContractSpec? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        if (!base.Equals(other)) return false;

        return HasRoot == other.HasRoot &&
            IsAny == other.IsAny &&
            SchemaProviderMethod == other.SchemaProviderMethod &&
            SchemaProviderMethodAccessorName == other.SchemaProviderMethodAccessorName &&
            SchemaProviderMethodIsXmlSchemaType == other.SchemaProviderMethodIsXmlSchemaType &&
            ConstructorAccessorName == other.ConstructorAccessorName &&
            IsXElement == other.IsXElement &&
            IsXmlElementOrXmlNodeArray == other.IsXmlElementOrXmlNodeArray &&
            KnownDataContracts.Equals(other.KnownDataContracts) &&
            Equals(XmlRootAttribute, other.XmlRootAttribute)
            ;
    }

    public override bool Equals(DataContractSpec? other) => Equals(other as XmlDataContractSpec);
    public override bool Equals(object? obj) => Equals(obj as XmlDataContractSpec);

    public override int GetHashCode()
    {
        unchecked
        {
            var hashCode = base.GetHashCode();
            hashCode = (hashCode * 397) ^ HasRoot.GetHashCode();
            hashCode = (hashCode * 397) ^ IsAny.GetHashCode();
            hashCode = (hashCode * 397) ^ (ConstructorAccessorName?.GetHashCode() ?? 0);
            hashCode = (hashCode * 397) ^ (SchemaProviderMethod?.GetHashCode() ?? 0);
            hashCode = (hashCode * 397) ^ (SchemaProviderMethodAccessorName?.GetHashCode() ?? 0);
            hashCode = (hashCode * 397) ^ (SchemaProviderMethodIsXmlSchemaType?.GetHashCode() ?? 0);
            hashCode = (hashCode * 397) ^ IsXElement.GetHashCode();
            hashCode = (hashCode * 397) ^ IsXmlElementOrXmlNodeArray.GetHashCode();
            hashCode = (hashCode * 397) ^ KnownDataContracts.GetHashCode();
            hashCode = (hashCode * 397) ^ (XmlRootAttribute?.GetHashCode() ?? 0);
            return hashCode;
        }
    }
}