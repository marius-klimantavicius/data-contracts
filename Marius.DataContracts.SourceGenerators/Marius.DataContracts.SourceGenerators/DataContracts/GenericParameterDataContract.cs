using Microsoft.CodeAnalysis;

namespace Marius.DataContracts.SourceGenerators.DataContracts;

internal class GenericParameterDataContract : DataContract
{
    public GenericParameterDataContract(DataContractContext context, ITypeSymbol type)
        : base(new GenericParameterDataContractModel(context, type))
    {
    }

    private sealed class GenericParameterDataContractModel : DataContractModel
    {
        public GenericParameterDataContractModel(DataContractContext context, ITypeSymbol type)
            : base(context, type)
        {
        }
    }
}