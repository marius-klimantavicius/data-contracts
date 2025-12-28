namespace Marius.DataContracts.SourceGenerators.Specs;

internal sealed class KnownDataContractSpec : IEquatable<KnownDataContractSpec>
{
    public required int ContractId { get; init; }

    public bool Equals(KnownDataContractSpec? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;

        return ContractId == other.ContractId;
    }

    public override bool Equals(object? obj)
    {
        return ReferenceEquals(this, obj) || obj is KnownDataContractSpec other && Equals(other);
    }

    public override int GetHashCode()
    {
        return ContractId;
    }

    public static bool operator ==(KnownDataContractSpec? left, KnownDataContractSpec? right)
    {
        return Equals(left, right);
    }

    public static bool operator !=(KnownDataContractSpec? left, KnownDataContractSpec? right)
    {
        return !Equals(left, right);
    }
}