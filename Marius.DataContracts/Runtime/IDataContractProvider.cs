namespace Marius.DataContracts.Runtime;

public interface IDataContractProvider
{
    DataContract? GetPrimitiveDataContract(string name, string ns);
    DataContract? GetPrimitiveDataContract(Type type);
    DataContract GetDataContract(Type type);
    DataContract GetDataContract(RuntimeTypeHandle typeHandle, Type? type);
    DataContract GetDataContractSkipValidation(RuntimeTypeHandle typeHandle, Type? type);
}