using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;

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

    public bool TryGetDataContract(Type type, [NotNullWhen(true)] out DataContract? dataContract)
    {
        return _typeDataContracts.TryGetValue(type, out dataContract);
    }
}