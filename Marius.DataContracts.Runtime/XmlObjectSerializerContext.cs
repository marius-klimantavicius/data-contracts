// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Frozen;
using System.Runtime.Serialization;
using System.Xml;

using DataContractDictionary = System.Collections.Frozen.FrozenDictionary<System.Xml.XmlQualifiedName, Marius.DataContracts.Runtime.DataContract>;

namespace Marius.DataContracts.Runtime;

public class XmlObjectSerializerContext
{
    private readonly XmlObjectSerializer _serializer;
    private readonly DataContract? _rootTypeDataContract;
    private ScopedKnownTypes _scopedKnownTypes;
    private DataContractDictionary? _serializerKnownDataContracts;
    private bool _isSerializerKnownDataContractsSetExplicit;
    private IList<Type>? _serializerKnownTypeList;
    private int _itemCount;
    private readonly int _maxItemsInObjectGraph;
    private readonly StreamingContext _streamingContext;

    protected ScopedKnownTypes ScopedKnownTypes => _scopedKnownTypes;
    protected IDataContractProvider DataContractProvider => _serializer.DataContractProvider;
    protected DataContract? rootTypeDataContract => _rootTypeDataContract;

    public XmlObjectSerializerContext(XmlObjectSerializer serializer, int maxItemsInObjectGraph, StreamingContext streamingContext, bool ignoreExtensionDataObject)
    {
        _serializer = serializer;
        _itemCount = 1;
        _maxItemsInObjectGraph = maxItemsInObjectGraph;
        _streamingContext = streamingContext;
        IgnoreExtensionDataObject = ignoreExtensionDataObject;
    }

    public XmlObjectSerializerContext(DataContractSerializer serializer, DataContract rootTypeDataContract)
        : this(
            serializer,
            serializer.MaxItemsInObjectGraph,
#pragma warning disable SYSLIB0050 // StreamingContext ctor is obsolete
            new StreamingContext(StreamingContextStates.All),
#pragma warning restore SYSLIB0050
            serializer.IgnoreExtensionDataObject
        )
    {
        _rootTypeDataContract = rootTypeDataContract;
        _serializerKnownTypeList = serializer._knownTypeList;
    }

    public virtual bool IsGetOnlyCollection
    {
        get => false;
        set { }
    }

    public StreamingContext GetStreamingContext()
    {
        return _streamingContext;
    }

    public void IncrementItemCount(int count)
    {
        if (count > _maxItemsInObjectGraph - _itemCount)
            throw XmlObjectSerializer.CreateSerializationException(SR.Format(SR.ExceededMaxItemsQuota, _maxItemsInObjectGraph));

        _itemCount += count;
    }

    public int RemainingItemCount => _maxItemsInObjectGraph - _itemCount;

    public bool IgnoreExtensionDataObject { get; }

    public DataContract GetDataContract(Type type)
    {
        return GetDataContract(type.TypeHandle, type);
    }

    public virtual DataContract GetDataContract(RuntimeTypeHandle typeHandle, Type? type)
    {
        return DataContractProvider.GetDataContract(typeHandle, type);
    }

    public virtual DataContract GetDataContractSkipValidation(RuntimeTypeHandle typeHandle, Type? type)
    {
        return DataContractProvider.GetDataContractSkipValidation(typeHandle, type);
    }

    public virtual void CheckIfTypeSerializable(Type memberType, bool isMemberTypeSerializable)
    {
        if (!isMemberTypeSerializable)
            throw new InvalidDataContractException(SR.Format(SR.TypeNotSerializable, memberType));
    }

    public virtual Type GetSurrogatedType(Type type)
    {
        return type;
    }

    public virtual DataContractDictionary? SerializerKnownDataContracts
    {
        get
        {
            // This field must be initialized during construction by serializers using data contracts.
            if (!_isSerializerKnownDataContractsSetExplicit)
            {
                _serializerKnownDataContracts = _serializer.KnownDataContracts;
                _isSerializerKnownDataContractsSetExplicit = true;
            }

            return _serializerKnownDataContracts;
        }
    }

    private DataContract? GetDataContractFromSerializerKnownTypes(XmlQualifiedName qname)
    {
        return SerializerKnownDataContracts?.GetValueOrDefault(qname);
    }

    public static DataContractDictionary? GetDataContractsForKnownTypes(IDataContractProvider dataContractProvider, IList<Type>? knownTypeList)
    {
        if (knownTypeList == null)
            return null;

        var dataContracts = new Dictionary<XmlQualifiedName, DataContract>();
        for (var i = 0; i < knownTypeList.Count; i++)
        {
            var knownType = knownTypeList[i];
            if (knownType == null)
                throw new ArgumentException(SR.Format(SR.NullKnownType, "knownTypes"));

            // Check if this is correct, how to handle duplicates
            var typeContract = dataContractProvider.GetDataContract(knownType);
            foreach (var item in typeContract.KnownDataContracts) 
                dataContracts.TryAdd(item.Key, item.Value);
        }

        return dataContracts.ToFrozenDictionary();
    }
    
    public bool IsKnownType(DataContract dataContract, DataContractDictionary? knownDataContracts, Type? declaredType)
    {
        var knownTypesAddedInCurrentScope = false;
        if (knownDataContracts?.Count > 0)
        {
            _scopedKnownTypes.Push(knownDataContracts);
            knownTypesAddedInCurrentScope = true;
        }

        var isKnownType = IsKnownType(dataContract, declaredType);

        if (knownTypesAddedInCurrentScope)
        {
            _scopedKnownTypes.Pop();
        }

        return isKnownType;
    }

    public bool IsKnownType(DataContract dataContract, Type? declaredType)
    {
        var knownContract = ResolveDataContractFromKnownTypes(dataContract.XmlName.Name, dataContract.XmlName.Namespace, null /*memberTypeContract*/, declaredType);
        return knownContract != null && knownContract.UnderlyingType == dataContract.UnderlyingType;
    }

    public Type? ResolveNameFromKnownTypes(XmlQualifiedName typeName)
    {
        var dataContract = ResolveDataContractFromKnownTypes(typeName);
        return dataContract?.OriginalUnderlyingType;
    }

    private DataContract? ResolveDataContractFromKnownTypes(XmlQualifiedName typeName) =>
        DataContractProvider.GetPrimitiveDataContract(typeName.Name, typeName.Namespace) ??
        _scopedKnownTypes.GetDataContract(typeName) ??
        GetDataContractFromSerializerKnownTypes(typeName);

    protected DataContract? ResolveDataContractFromKnownTypes(string typeName, string? typeNs, DataContract? memberTypeContract, Type? declaredType)
    {
        var qname = new XmlQualifiedName(typeName, typeNs);
        var dataContract = ResolveDataContractFromKnownTypes(qname);
        if (dataContract == null)
        {
            if (memberTypeContract != null
                && !memberTypeContract.UnderlyingType.IsInterface
                && memberTypeContract.XmlName == qname)
            {
                dataContract = memberTypeContract;
            }

            if (dataContract == null && _rootTypeDataContract != null)
            {
                if (_rootTypeDataContract.XmlName == qname)
                    dataContract = _rootTypeDataContract;
                else
                    dataContract = ResolveDataContractFromRootDataContract(qname);
            }
        }

        return dataContract;
    }

    protected virtual DataContract? ResolveDataContractFromRootDataContract(XmlQualifiedName typeQName)
    {
        var collectionContract = _rootTypeDataContract as CollectionDataContract;
        while (collectionContract != null)
        {
            var itemContract = GetDataContract(GetSurrogatedType(collectionContract.ItemType));
            if (itemContract.XmlName == typeQName)
                return itemContract;

            collectionContract = itemContract as CollectionDataContract;
        }

        return null;
    }
}