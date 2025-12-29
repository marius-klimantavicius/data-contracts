using System.Collections.Frozen;

namespace Marius.DataContracts.Runtime;

public class DataContractProvider : IDataContractProvider
{
    private readonly DataContract?[] _dataContracts;
    private readonly FrozenDictionary<Type, DataContract> _typeDataContracts;

    public DataContractProvider(DataContract[] dataContracts, FrozenDictionary<Type, DataContract> typeDataContracts)
    {
        _dataContracts = dataContracts;
        _typeDataContracts = typeDataContracts;
    }

    public DataContract? GetPrimitiveDataContract(string name, string ns)
    {
        for (var i = 0; i < _dataContracts.Length; i++)
        {
            var item = _dataContracts[i];
            if (item == null)
                continue;
            
            if (item.XmlName.Name == name && item.XmlName.Namespace == ns)
                return item;
        }

        return null;
    }

    public DataContract? GetPrimitiveDataContract(Type type)
    {
        return _typeDataContracts.GetValueOrDefault(type);
    }

    public DataContract GetDataContract(Type type)
    {
        if (_typeDataContracts.TryGetValue(type, out var value))
            return value;

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