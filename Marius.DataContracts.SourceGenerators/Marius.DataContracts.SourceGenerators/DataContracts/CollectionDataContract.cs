using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;

namespace Marius.DataContracts.SourceGenerators.DataContracts;

public enum CollectionKind : byte
{
    None,
    GenericDictionary,
    Dictionary,
    GenericList,
    GenericCollection,
    List,
    GenericEnumerable,
    Collection,
    Enumerable,
    Array,
}

public partial class CollectionDataContract : DataContract
{
    private DataContract? _itemContract;
    private string _collectionItemName;
    private string? _childElementNamespace;
    
    private new CollectionDataContractModel Model => Unsafe.As<CollectionDataContractModel>(base.Model);

    public DataContract? SharedTypeContract => Model.SharedTypeContract;
    public string ItemName => Model.ItemName;
    public CollectionKind Kind => Model.Kind;
    public ITypeSymbol ItemType => Model.ItemType;
    public IMethodSymbol? AddMethod => Model.AddMethod;

    public string? KeyName => Model.KeyName;
    public string? ValueName => Model.ValueName;
    public bool IsDictionary => KeyName != null;

    internal string? ChildElementNamespace
    {
        get
        {
            if (_childElementNamespace == null)
            {
                lock (this)
                {
                    if (_childElementNamespace == null && !IsDictionary)
                    {
                        var tempChildElementNamespace = ClassDataContract.GetChildNamespaceToDeclare(this, ItemType);
                        Interlocked.MemoryBarrier();
                        _childElementNamespace = tempChildElementNamespace;
                    }
                }
            }

            return _childElementNamespace;
        }
    }    
    public DataContract ItemContract
    {
        get => _itemContract ?? Model.ItemContract;
        set
        {
            _itemContract = value;
            Model.ItemContract = value;
        }
    }

    public string CollectionItemName => _collectionItemName;
    
    internal CollectionDataContract(DataContractContext context, IArrayTypeSymbol type)
        : base(new CollectionDataContractModel(context, type))
    {
        InitCollectionDataContract(this);
    }

    internal CollectionDataContract(DataContractContext context, ITypeSymbol type, CollectionKind kind)
        : base(new CollectionDataContractModel(context, type, kind))
    {
        InitCollectionDataContract(this);
    }

    private CollectionDataContract(DataContractContext context, ITypeSymbol type, CollectionKind kind, ITypeSymbol itemType, IMethodSymbol getEnumeratorMethod, string? serializationExceptionMessage, string? deserializationExceptionMessage)
        : base(new CollectionDataContractModel(context, type, kind, itemType, getEnumeratorMethod, serializationExceptionMessage, deserializationExceptionMessage))
    {
        InitCollectionDataContract(GetSharedTypeContract(type));
    }

    private CollectionDataContract(DataContractContext context, ITypeSymbol type, CollectionKind kind, ITypeSymbol itemType, IMethodSymbol getEnumeratorMethod, IMethodSymbol? addMethod, IMethodSymbol? constructor)
        : base(new CollectionDataContractModel(context, type, kind, itemType, getEnumeratorMethod, addMethod, constructor))
    {
        InitCollectionDataContract(GetSharedTypeContract(type));
    }

    private CollectionDataContract(DataContractContext context, ITypeSymbol type, CollectionKind kind, ITypeSymbol itemType, IMethodSymbol getEnumeratorMethod, IMethodSymbol? addMethod, IMethodSymbol? constructor, bool isConstructorCheckRequired)
        : base(new CollectionDataContractModel(context, type, kind, itemType, getEnumeratorMethod, addMethod, constructor, isConstructorCheckRequired))
    {
        InitCollectionDataContract(GetSharedTypeContract(type));
    }

    private CollectionDataContract(DataContractContext context, ITypeSymbol type, string invalidCollectionInSharedContractMessage)
        : base(new CollectionDataContractModel(context, type, invalidCollectionInSharedContractMessage))
    {
        InitCollectionDataContract(GetSharedTypeContract(type));
    }

    internal ITypeSymbol GetCollectionElementType()
    {
        return Model.GetCollectionElementType();
    }

    private DataContract? GetSharedTypeContract(ITypeSymbol type)
    {
        if (DataContractContext.HasCollectionDataContractAttribute(type))
            return this;

        if (Context.KnownSymbols.IsSerializable(type) || DataContractContext.HasDataContractAttribute(type))
            return new ClassDataContract(Context, type);

        return null;
    }
    
    [MemberNotNull(nameof(_collectionItemName))]
    private void InitCollectionDataContract(DataContract? sharedTypeContract)
    {
        _collectionItemName = Model.CollectionItemName;
        if (Model.Kind == CollectionKind.Dictionary || Model.Kind == CollectionKind.GenericDictionary) 
            _itemContract = Model.ItemContract;

        Model.SharedTypeContract = sharedTypeContract;
    }

}