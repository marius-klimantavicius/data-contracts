using Microsoft.CodeAnalysis;

namespace Marius.DataContracts.SourceGenerators.DataContracts;

internal class DataMember
{
    private DataContract? _memberTypeContract;
    private PrimitiveDataContract? _memberPrimitiveContract;
    private ITypeSymbol? _memberType;

    public DataContractContext Context { get; }

    public string Name { get; set; } = null!;
    public bool IsRequired { get; set; }
    public bool IsNullable { get; set; }
    public bool EmitDefaultValue { get; set; }
    public long Order { get; set; }
    public bool IsGetOnlyCollection { get; set; }

    public ISymbol MemberInfo { get; }

    public DataMember? ConflictingMember { get; set; }
    public bool HasConflictingNameAndType { get; set; }

    public ITypeSymbol MemberType
    {
        get
        {
            if (_memberType == null)
            {
                if (MemberInfo is IFieldSymbol field)
                    _memberType = field.Type;
                else if (MemberInfo is IPropertySymbol property)
                    _memberType = property.Type;
                else
                    _memberType = (ITypeSymbol)MemberInfo;
            }

            return _memberType;
        }
    }

    internal PrimitiveDataContract? MemberPrimitiveContract
    {
        get
        {
            if (_memberPrimitiveContract is NullPrimitiveDataContract)
                _memberPrimitiveContract = Context.GetBuiltInDataContract(MemberType) as PrimitiveDataContract;

            return _memberPrimitiveContract;
        }
    }

    internal DataContract MemberTypeContract
    {
        get
        {
            if (_memberTypeContract == null)
            {
                if (IsGetOnlyCollection)
                    _memberTypeContract = Context.GetGetOnlyCollectionDataContract(MemberType);
                else
                    _memberTypeContract = Context.GetDataContract(MemberType);
            }

            return _memberTypeContract;
        }
    }

    public DataMember(DataContractContext context, ISymbol member)
    {
        EmitDefaultValue = true;
        Context = context;
        MemberInfo = member;
        _memberPrimitiveContract = new NullPrimitiveDataContract(context);
    }
}