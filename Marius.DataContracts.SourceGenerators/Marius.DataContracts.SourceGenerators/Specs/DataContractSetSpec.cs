namespace Marius.DataContracts.SourceGenerators.Specs;

/// <summary>
/// Container for all data contract specifications. Immutable and equatable.
/// </summary>
internal sealed class DataContractSetSpec : IEquatable<DataContractSetSpec>
{
    public required EquatableArray<DataContractSpec> Contracts { get; init; }
    public required EquatableArray<PrivateAccessorSpec> PrivateAccessors { get; init; }
    public required EquatableArray<DiagnosticInfo> Diagnostics { get; init; }

    public DataContractSpec? GetContract(int id)
    {
        if (id < 0 || id >= Contracts.Length)
            return null;
        return Contracts[id];
    }

    public bool Equals(DataContractSetSpec? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return Contracts.Equals(other.Contracts) &&
               PrivateAccessors.Equals(other.PrivateAccessors) &&
               Diagnostics.Equals(other.Diagnostics);
    }

    public override bool Equals(object? obj) => Equals(obj as DataContractSetSpec);

    public override int GetHashCode()
    {
        unchecked
        {
            var hashCode = Contracts.GetHashCode();
            hashCode = (hashCode * 397) ^ PrivateAccessors.GetHashCode();
            hashCode = (hashCode * 397) ^ Diagnostics.GetHashCode();
            return hashCode;
        }
    }

    public static bool operator ==(DataContractSetSpec? left, DataContractSetSpec? right) => Equals(left, right);
    public static bool operator !=(DataContractSetSpec? left, DataContractSetSpec? right) => !Equals(left, right);
}

