using Microsoft.CodeAnalysis;

namespace Marius.DataContracts.SourceGenerators.DataContracts;

internal class SpecialTypeDataContract : DataContract
{
    public const string LocalName = "anyType";

    private readonly DataContractContext _context;
    private readonly ITypeSymbol _type;

    public override bool IsBuiltInDataContract => true;

    public SpecialTypeDataContract(DataContractContext context, ITypeSymbol type)
        : base(new SpecialTypeDataContractModel(context, type, LocalName, DataContractContext.SchemaNamespace))
    {
        _context = context;
        _type = type;
    }

    private sealed class SpecialTypeDataContractModel : DataContractModel
    {
        public SpecialTypeDataContractModel(DataContractContext context, ITypeSymbol type, string name, string ns)
            : base(context, type)
        {
            SetDataContractName(name, ns);
        }
    }
}