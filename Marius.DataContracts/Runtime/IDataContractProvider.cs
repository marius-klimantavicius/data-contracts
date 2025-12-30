using System.Diagnostics.CodeAnalysis;

namespace Marius.DataContracts.Runtime;

public interface IDataContractProvider
{
    DataContract? GetPrimitiveDataContract(string name, string ns);
    DataContract? GetPrimitiveDataContract(Type type);
    
    bool TryGetDataContract(Type type, [NotNullWhen(true)] out DataContract? dataContract);

    public DataContract GetDataContract(Type type)
    {
        if (TryGetDataContract(type, out var contract))
            return contract;
        
        throw new InvalidOperationException($"DataContract for type {type.FullName} not found.");
    }

    public DataContract GetDataContract(RuntimeTypeHandle typeHandle, Type? type)
    {
        type ??= Type.GetTypeFromHandle(typeHandle)!;
        return GetDataContract(type);
    }

    public DataContract GetDataContractSkipValidation(RuntimeTypeHandle typeHandle, Type? type)
    {
        type ??= Type.GetTypeFromHandle(typeHandle)!;
        return GetDataContract(type);
    }
}