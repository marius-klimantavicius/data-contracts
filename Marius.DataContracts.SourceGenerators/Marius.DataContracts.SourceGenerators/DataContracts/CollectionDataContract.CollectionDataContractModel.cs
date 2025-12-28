using System.Collections.Immutable;
using System.Diagnostics;
using System.Runtime.Serialization;
using Microsoft.CodeAnalysis;

namespace Marius.DataContracts.SourceGenerators.DataContracts;

public partial class CollectionDataContract
{
    public const string KeyLocalName = "Key";
    public const string ValueLocalName = "Value";
    public const string GetCurrentMethodName = "get_Current";

    private sealed class CollectionDataContractModel : DataContractModel
    {
        private DataContract? _itemContract;

        private bool _isKnownTypeAttributeChecked;
        private ImmutableArray<DataContract> _knownDataContracts = ImmutableArray<DataContract>.Empty;

        internal DataContract? SharedTypeContract { get; set; }

        internal CollectionKind Kind { get; private set; }

        internal ITypeSymbol ItemType { get; private set; } = null!;
        internal string ItemName { get; set; } = null!;
        internal bool ItemNameSetExplicit { get; set; }
        internal string CollectionItemName { get; private set; } = null!;
        internal bool IsItemTypeNullable { get; set; }

        internal string? KeyName { get; set; }
        internal string? ValueName { get; set; }
        internal bool IsDictionary => KeyName != null;

        internal IMethodSymbol? GetEnumeratorMethod { get; }
        internal IMethodSymbol? AddMethod { get; }
        internal IMethodSymbol? Constructor { get; }

        internal string? SerializationExceptionMessage { get; }
        internal string? DeserializationExceptionMessage { get; }
        internal string? InvalidCollectionInSharedContractMessage { get; }

        internal bool IsConstructorCheckRequired { get; }

        internal DataContract ItemContract
        {
            get
            {
                if (_itemContract == null)
                {
                    if (IsDictionary)
                    {
                        if (KeyName == ValueName)
                            DataContractContext.ThrowInvalidDataContractException(SR.Format(SR.DupKeyValueName, DataContractContext.GetClrTypeFullName(UnderlyingType), KeyName));

                        Debug.Assert(KeyName != null);
                        Debug.Assert(ValueName != null);
                        _itemContract = Context.GetOrAddDataContract(ItemType, () => ClassDataContract.CreateClassDataContractForKeyValue(Context, ItemType, Namespace, new string[] { KeyName, ValueName }));
                    }
                    else
                    {
                        _itemContract = Context.GetDataContract(ItemType);
                    }
                }

                return _itemContract;
            }

            set => _itemContract = value;
        }

        public override ImmutableArray<DataContract> KnownDataContracts
        {
            get
            {
                if (!_isKnownTypeAttributeChecked)
                {
                    if (!_isKnownTypeAttributeChecked)
                    {
                        _knownDataContracts = Context.ImportKnownTypeAttributes(UnderlyingType);
                        _isKnownTypeAttributeChecked = true;
                    }
                }

                return _knownDataContracts;
            }

            set => _knownDataContracts = value;
        }

        public CollectionDataContractModel(DataContractContext context, IArrayTypeSymbol type)
            : base(context, type)
        {
            if (SymbolEqualityComparer.Default.Equals(context.KnownSymbols.ArrayType, type))
                type = context.KnownSymbols.ObjectArrayType;

            if (type.Rank > 1)
                throw new NotSupportedException(SR.SupportForMultidimensionalArraysNotPresent);

            XmlName = context.GetXmlName(type);
            Init(CollectionKind.Array, type.ElementType, null);
        }

        public CollectionDataContractModel(DataContractContext context, ITypeSymbol type, CollectionKind kind)
            : base(context, type)
        {
            XmlName = context.GetXmlName(type);
            Init(kind, (type as IArrayTypeSymbol)?.ElementType, null);
        }

        public CollectionDataContractModel(DataContractContext context, ITypeSymbol type, CollectionKind kind, ITypeSymbol itemType, IMethodSymbol getEnumeratorMethod, string? serializationExceptionMessage, string? deserializationExceptionMessage)
            : base(context, type)
        {
            if (getEnumeratorMethod == null)
                throw new InvalidDataContractException(SR.Format(SR.CollectionMustHaveGetEnumeratorMethod, DataContractContext.GetClrTypeFullName(type)));
            if (itemType == null)
                throw new InvalidDataContractException(SR.Format(SR.CollectionMustHaveItemType, DataContractContext.GetClrTypeFullName(type)));

            XmlName = context.GetCollectionXmlName(type, itemType, out var collectionContractAttribute);

            Init(kind, itemType, collectionContractAttribute);
            GetEnumeratorMethod = getEnumeratorMethod;
            SerializationExceptionMessage = serializationExceptionMessage;
            DeserializationExceptionMessage = deserializationExceptionMessage;
        }

        public CollectionDataContractModel(DataContractContext context, ITypeSymbol type, CollectionKind kind, ITypeSymbol itemType, IMethodSymbol getEnumeratorMethod, IMethodSymbol? addMethod, IMethodSymbol? constructor)
            : this(context, type, kind, itemType, getEnumeratorMethod, default(string), null)
        {
            if (addMethod == null && type.TypeKind != TypeKind.Interface)
                throw new InvalidDataContractException(SR.Format(SR.CollectionMustHaveAddMethod, DataContractContext.GetClrTypeFullName(type)));

            AddMethod = addMethod;
            Constructor = constructor;
        }

        public CollectionDataContractModel(DataContractContext context, ITypeSymbol type, CollectionKind kind, ITypeSymbol itemType, IMethodSymbol getEnumeratorMethod, IMethodSymbol? addMethod, IMethodSymbol? constructor, bool isConstructorCheckRequired)
            : this(context, type, kind, itemType, getEnumeratorMethod, addMethod, constructor)
        {
            IsConstructorCheckRequired = isConstructorCheckRequired;
        }

        public CollectionDataContractModel(DataContractContext context, ITypeSymbol type, string invalidCollectionInSharedContractMessage)
            : base(context, type)
        {
            Init(CollectionKind.Collection, null /*itemType*/, null);
            InvalidCollectionInSharedContractMessage = invalidCollectionInSharedContractMessage;
        }

        private void Init(CollectionKind kind, ITypeSymbol? itemType, CollectionDataContractAttribute? collectionContractAttribute)
        {
            Kind = kind;
            if (itemType != null)
            {
                ItemType = itemType;
                IsItemTypeNullable = Context.IsTypeNullable(itemType);

                var isDictionary = kind == CollectionKind.Dictionary || kind == CollectionKind.GenericDictionary;
                var itemName = default(string?);
                var keyName = default(string?);
                var valueName = default(string?);
                if (collectionContractAttribute != null)
                {
                    if (collectionContractAttribute.IsItemNameSetExplicitly)
                    {
                        if (string.IsNullOrEmpty(collectionContractAttribute.ItemName))
                            throw new InvalidDataContractException(SR.Format(SR.InvalidCollectionContractItemName, DataContractContext.GetClrTypeFullName(UnderlyingType)));

                        itemName = DataContractContext.EncodeLocalName(collectionContractAttribute.ItemName);
                        ItemNameSetExplicit = true;
                    }

                    if (collectionContractAttribute.IsKeyNameSetExplicitly)
                    {
                        if (string.IsNullOrEmpty(collectionContractAttribute.KeyName))
                            throw new InvalidDataContractException(SR.Format(SR.InvalidCollectionContractKeyName, DataContractContext.GetClrTypeFullName(UnderlyingType)));
                        if (!isDictionary)
                            throw new InvalidDataContractException(SR.Format(SR.InvalidCollectionContractKeyNoDictionary, DataContractContext.GetClrTypeFullName(UnderlyingType), collectionContractAttribute.KeyName));

                        keyName = DataContractContext.EncodeLocalName(collectionContractAttribute.KeyName);
                    }

                    if (collectionContractAttribute.IsValueNameSetExplicitly)
                    {
                        if (string.IsNullOrEmpty(collectionContractAttribute.ValueName))
                            throw new InvalidDataContractException(SR.Format(SR.InvalidCollectionContractValueName, DataContractContext.GetClrTypeFullName(UnderlyingType)));
                        if (!isDictionary)
                            throw new InvalidDataContractException(SR.Format(SR.InvalidCollectionContractValueNoDictionary, DataContractContext.GetClrTypeFullName(UnderlyingType), collectionContractAttribute.ValueName));

                        valueName = DataContractContext.EncodeLocalName(collectionContractAttribute.ValueName);
                    }
                }

                Name = XmlName.Name;
                Namespace = XmlName.Namespace;
                ItemName = itemName ?? Context.GetXmlName(DataContractContext.UnwrapNullableType(itemType)).Name;
                CollectionItemName = ItemName;
                if (isDictionary)
                {
                    KeyName = keyName ?? KeyLocalName;
                    ValueName = valueName ?? ValueLocalName;
                }
            }

            if (collectionContractAttribute != null)
                IsReference = collectionContractAttribute.IsReference;
        }

        internal ITypeSymbol GetCollectionElementType()
        {
            Debug.Assert(Kind != CollectionKind.Array, "GetCollectionElementType should not be called on Arrays");
            Debug.Assert(GetEnumeratorMethod != null, "GetEnumeratorMethod should be non-null for non-Arrays");

            var enumeratorType = default(ITypeSymbol);
            if (Kind == CollectionKind.GenericDictionary)
            {
                var keyValueTypes = ((INamedTypeSymbol)ItemType).TypeArguments;
                enumeratorType = Context.KnownSymbols.IEnumeratorOfType!.Construct(Context.KnownSymbols.KeyValueOfType!.Construct(keyValueTypes.ToArray()));
            }
            else if (Kind == CollectionKind.Dictionary)
            {
                enumeratorType = Context.KnownSymbols.IEnumeratorOfType!.Construct(Context.KnownSymbols.KeyValueOfType!.Construct(Context.KnownSymbols.ObjectType, Context.KnownSymbols.ObjectType));
            }
            else
            {
                enumeratorType = GetEnumeratorMethod.ReturnType;
            }

            var getCurrentMethod = enumeratorType.GetMembers(GetCurrentMethodName).OfType<IMethodSymbol>().FirstOrDefault(s => !s.IsStatic && s.DeclaredAccessibility == Accessibility.Public && s.Parameters.Length == 0);
            if (getCurrentMethod == null)
            {
                if (enumeratorType.TypeKind == TypeKind.Interface)
                {
                    getCurrentMethod = Context.KnownSymbols.GetEnumeratorCurrentGetMethod();
                }
                else
                {
                    var ienumeratorInterface = Context.KnownSymbols.IEnumeratorType;
                    if (Kind == CollectionKind.GenericDictionary || Kind == CollectionKind.GenericCollection || Kind == CollectionKind.GenericEnumerable)
                    {
                        var interfaceTypes = enumeratorType.AllInterfaces;
                        foreach (var interfaceType in interfaceTypes)
                        {
                            if (interfaceType.IsGenericType
                                && SymbolEqualityComparer.Default.Equals(Context.KnownSymbols.IEnumeratorOfType, interfaceType.ConstructedFrom)
                                && SymbolEqualityComparer.Default.Equals(interfaceType.TypeArguments[0], ItemType))
                            {
                                ienumeratorInterface = interfaceType;
                                break;
                            }
                        }
                    }

                    getCurrentMethod = GetTargetMethodWithName(GetCurrentMethodName, enumeratorType, ienumeratorInterface)!;
                }
            }

            var elementType = getCurrentMethod.ReturnType;
            return elementType;
        }
    }
}