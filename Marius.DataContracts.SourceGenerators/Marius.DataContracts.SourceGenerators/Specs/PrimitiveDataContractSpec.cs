namespace Marius.DataContracts.SourceGenerators.Specs;

/// <summary>
/// Immutable, equatable representation of a PrimitiveDataContract that is free from Roslyn symbols.
/// </summary>
internal sealed class PrimitiveDataContractSpec : DataContractSpec, IEquatable<PrimitiveDataContractSpec>
{
    public required string PrimitiveContractName { get; init; }
    public required string WriteMethodName { get; init; }
    public required string ReadMethodName { get; init; }
    public required TypeSpec? InterfaceType { get; init; }

    public bool Equals(PrimitiveDataContractSpec? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        if (!base.Equals(other)) return false;

        return PrimitiveContractName == other.PrimitiveContractName &&
            WriteMethodName == other.WriteMethodName &&
            ReadMethodName == other.ReadMethodName;
    }

    public override bool Equals(DataContractSpec? other) => Equals(other as PrimitiveDataContractSpec);
    public override bool Equals(object? obj) => Equals(obj as PrimitiveDataContractSpec);

    public override int GetHashCode()
    {
        unchecked
        {
            var hashCode = base.GetHashCode();
            hashCode = (hashCode * 397) ^ PrimitiveContractName.GetHashCode();
            hashCode = (hashCode * 397) ^ WriteMethodName.GetHashCode();
            hashCode = (hashCode * 397) ^ ReadMethodName.GetHashCode();
            return hashCode;
        }
    }
}