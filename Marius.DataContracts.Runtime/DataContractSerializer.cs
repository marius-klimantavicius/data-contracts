// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.Serialization;
using System.Text;
using System.Xml;
using DataContractDictionary = System.Collections.Frozen.FrozenDictionary<System.Xml.XmlQualifiedName, Marius.DataContracts.Runtime.DataContract>;

namespace Marius.DataContracts.Runtime;

public sealed class DataContractSerializer : XmlObjectSerializer
{
    private Type _rootType;
    private DataContract? _rootContract; // post-surrogate
    private bool _needsContractNsAtRoot;
    private XmlDictionaryString? _rootName;
    private XmlDictionaryString? _rootNamespace;
    private ReadOnlyCollection<Type>? _knownTypeCollection;
    internal IList<Type>? _knownTypeList;
    internal DataContractDictionary? _knownDataContracts;
    private IDataContractProvider _dataContractProvider;

    internal static UTF8Encoding UTF8NoBom { get; } = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: false);
    internal static UTF8Encoding ValidatingUTF8 { get; } = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    internal static UnicodeEncoding UTF16NoBom { get; } = new UnicodeEncoding(bigEndian: false, byteOrderMark: false, throwOnInvalidBytes: false);
    internal static UnicodeEncoding BEUTF16NoBom { get; } = new UnicodeEncoding(bigEndian: true, byteOrderMark: false, throwOnInvalidBytes: false);
    internal static UnicodeEncoding ValidatingUTF16 { get; } = new UnicodeEncoding(bigEndian: false, byteOrderMark: false, throwOnInvalidBytes: true);
    internal static UnicodeEncoding ValidatingBEUTF16 { get; } = new UnicodeEncoding(bigEndian: true, byteOrderMark: false, throwOnInvalidBytes: true);

    internal static Base64Encoding Base64Encoding { get; } = new Base64Encoding();

    public override IDataContractProvider DataContractProvider => _dataContractProvider;

    private static bool IsReflectionBackupAllowed()
    {
        return true;
    }

    public DataContractSerializer(IDataContractProvider dataContractProvider, Type type, IEnumerable<Type>? knownTypes = null)
        : this(dataContractProvider, type, knownTypes, int.MaxValue, false, false)
    {
    }

    public DataContractSerializer(IDataContractProvider dataContractProvider, Type type, string rootName, string rootNamespace, IEnumerable<Type>? knownTypes = null)
        : this(dataContractProvider, type, rootName, rootNamespace, knownTypes, false, false)
    {
    }

    internal DataContractSerializer(IDataContractProvider dataContractProvider, Type type, string rootName, string rootNamespace, IEnumerable<Type>? knownTypes, bool ignoreExtensionDataObject, bool preserveObjectReferences)
    {
        var dictionary = new XmlDictionary(2);
        Initialize(dataContractProvider, type, dictionary.Add(rootName), dictionary.Add(rootNamespace), knownTypes, int.MaxValue, ignoreExtensionDataObject, preserveObjectReferences, false);
    }

    public DataContractSerializer(IDataContractProvider dataContractProvider, Type type, XmlDictionaryString rootName, XmlDictionaryString rootNamespace, IEnumerable<Type>? knownTypes = null)
    {
        Initialize(dataContractProvider, type, rootName, rootNamespace, knownTypes, int.MaxValue, false, false, false);
    }

    internal DataContractSerializer(IDataContractProvider dataContractProvider, Type type, IEnumerable<Type>? knownTypes, int maxItemsInObjectGraph, bool ignoreExtensionDataObject, bool preserveObjectReferences)
    {
        Initialize(dataContractProvider, type, knownTypes, maxItemsInObjectGraph, ignoreExtensionDataObject, preserveObjectReferences, false);
    }

    public DataContractSerializer(IDataContractProvider dataContractProvider, Type type, DataContractSerializerSettings? settings)
    {
        settings ??= new DataContractSerializerSettings();
        Initialize(dataContractProvider, type, settings.RootName, settings.RootNamespace, settings.KnownTypes, settings.MaxItemsInObjectGraph, settings.IgnoreExtensionDataObject, settings.PreserveObjectReferences, settings.SerializeReadOnlyTypes);
    }

    [MemberNotNull(nameof(_dataContractProvider))]
    [MemberNotNull(nameof(_rootType))]
    private void Initialize(
        IDataContractProvider dataContractProvider,
        Type type,
        IEnumerable<Type>? knownTypes,
        int maxItemsInObjectGraph,
        bool ignoreExtensionDataObject,
        bool preserveObjectReferences,
        bool serializeReadOnlyTypes)
    {
        ArgumentNullException.ThrowIfNull(dataContractProvider);
        ArgumentNullException.ThrowIfNull(type);

        _dataContractProvider = dataContractProvider;
        _rootType = type;

        if (knownTypes != null)
        {
            _knownTypeList = new List<Type>();
            foreach (var knownType in knownTypes) 
                _knownTypeList.Add(knownType);
        }

        ArgumentOutOfRangeException.ThrowIfNegative(maxItemsInObjectGraph);
        MaxItemsInObjectGraph = maxItemsInObjectGraph;

        IgnoreExtensionDataObject = ignoreExtensionDataObject;
        PreserveObjectReferences = preserveObjectReferences;
        SerializeReadOnlyTypes = serializeReadOnlyTypes;
    }

    [MemberNotNull(nameof(_dataContractProvider))]
    [MemberNotNull(nameof(_rootType))]
    private void Initialize(
        IDataContractProvider dataContractProvider,
        Type type,
        XmlDictionaryString? rootName,
        XmlDictionaryString? rootNamespace,
        IEnumerable<Type>? knownTypes,
        int maxItemsInObjectGraph,
        bool ignoreExtensionDataObject,
        bool preserveObjectReferences,
        bool serializeReadOnlyTypes)
    {
        Initialize(dataContractProvider, type, knownTypes, maxItemsInObjectGraph, ignoreExtensionDataObject, preserveObjectReferences, serializeReadOnlyTypes);

        // validate root name and namespace are both non-null
        _rootName = rootName;
        _rootNamespace = rootNamespace;
    }

    public ReadOnlyCollection<Type> KnownTypes
    {
        get
        {
            return _knownTypeCollection ??= _knownTypeList != null ? new ReadOnlyCollection<Type>(_knownTypeList) : ReadOnlyCollection<Type>.Empty;
        }
    }

    internal override DataContractDictionary? KnownDataContracts
    {
        get
        {
            if (_knownDataContracts == null && _knownTypeList != null)
            {
                // This assignment may be performed concurrently and thus is a race condition.
                // It's safe, however, because at worse a new (and identical) dictionary of
                // data contracts will be created and re-assigned to this field.  Introduction
                // of a lock here could lead to deadlocks.
                _knownDataContracts = XmlObjectSerializerContext.GetDataContractsForKnownTypes(_dataContractProvider, _knownTypeList);
            }

            return _knownDataContracts;
        }
    }

    public int MaxItemsInObjectGraph { get; private set; }

    public bool PreserveObjectReferences { get; private set; }

    public bool IgnoreExtensionDataObject { get; private set; }

    public bool SerializeReadOnlyTypes { get; private set; }

    private DataContract RootContract
    {
        get
        {
            if (_rootContract == null)
            {
                _rootContract = DataContractProvider.GetDataContract(_rootType);
                _needsContractNsAtRoot = CheckIfNeedsContractNsAtRoot(_rootName, _rootNamespace, _rootContract);
            }

            return _rootContract;
        }
    }

    internal override void InternalWriteObject(XmlWriterDelegator writer, object? graph)
    {
        InternalWriteStartObject(writer, graph);
        InternalWriteObjectContent(writer, graph);
        InternalWriteEndObject(writer);
    }

    public override void WriteObject(XmlWriter writer, object? graph)
    {
        WriteObjectHandleExceptions(new XmlWriterDelegator(writer), graph);
    }

    public override void WriteStartObject(XmlWriter writer, object? graph)
    {
        WriteStartObjectHandleExceptions(new XmlWriterDelegator(writer), graph);
    }

    public override void WriteObjectContent(XmlWriter writer, object? graph)
    {
        WriteObjectContentHandleExceptions(new XmlWriterDelegator(writer), graph);
    }

    public override void WriteEndObject(XmlWriter writer)
    {
        WriteEndObjectHandleExceptions(new XmlWriterDelegator(writer));
    }

    public override void WriteStartObject(XmlDictionaryWriter writer, object? graph)
    {
        WriteStartObjectHandleExceptions(new XmlWriterDelegator(writer), graph);
    }

    public override void WriteObjectContent(XmlDictionaryWriter writer, object? graph)
    {
        WriteObjectContentHandleExceptions(new XmlWriterDelegator(writer), graph);
    }

    public override void WriteEndObject(XmlDictionaryWriter writer)
    {
        WriteEndObjectHandleExceptions(new XmlWriterDelegator(writer));
    }

    public override void WriteObject(XmlDictionaryWriter writer, object? graph)
    {
        WriteObjectHandleExceptions(new XmlWriterDelegator(writer), graph);
    }

    public override object? ReadObject(XmlReader reader)
    {
        return ReadObjectHandleExceptions(new XmlReaderDelegator(reader), true /*verifyObjectName*/);
    }

    public override object? ReadObject(XmlReader reader, bool verifyObjectName)
    {
        return ReadObjectHandleExceptions(new XmlReaderDelegator(reader), verifyObjectName);
    }

    public override bool IsStartObject(XmlReader reader)
    {
        return IsStartObjectHandleExceptions(new XmlReaderDelegator(reader));
    }

    public override object? ReadObject(XmlDictionaryReader reader, bool verifyObjectName)
    {
        return ReadObjectHandleExceptions(new XmlReaderDelegator(reader), verifyObjectName);
    }

    public override bool IsStartObject(XmlDictionaryReader reader)
    {
        return IsStartObjectHandleExceptions(new XmlReaderDelegator(reader));
    }

    internal override void InternalWriteStartObject(XmlWriterDelegator writer, object? graph)
    {
        WriteRootElement(writer, RootContract, _rootName, _rootNamespace, _needsContractNsAtRoot);
    }

    internal override void InternalWriteObjectContent(XmlWriterDelegator writer, object? graph)
    {
        if (MaxItemsInObjectGraph == 0)
            throw CreateSerializationException(SR.Format(SR.ExceededMaxItemsQuota, MaxItemsInObjectGraph));

        var contract = RootContract;
        var declaredType = contract.UnderlyingType;
        var graphType = graph == null ? declaredType : graph.GetType();


        if (graph == null)
        {
            if (IsRootXmlAny(_rootName, contract))
                throw CreateSerializationException(SR.Format(SR.IsAnyCannotBeNull, declaredType));

            WriteNull(writer);
        }
        else
        {
            if (declaredType == graphType)
            {
                if (contract.CanContainReferences)
                {
                    var context = XmlObjectSerializerWriteContext.CreateContext(this, contract);
                    context.HandleGraphAtTopLevel(writer, graph, contract);
                    context.SerializeWithoutXsiType(contract, writer, graph, declaredType.TypeHandle);
                }
                else
                {
                    contract.WriteXmlValue(writer, graph, null);
                }
            }
            else
            {
                if (IsRootXmlAny(_rootName, contract))
                    throw CreateSerializationException(SR.Format(SR.IsAnyCannotBeSerializedAsDerivedType, graphType, contract.UnderlyingType));

                contract = GetDataContract(contract, declaredType, graphType);

                var context = XmlObjectSerializerWriteContext.CreateContext(this, RootContract);
                if (contract.CanContainReferences) 
                    context.HandleGraphAtTopLevel(writer, graph, contract);

                context.OnHandleIsReference(writer, contract, graph);
                context.SerializeWithXsiTypeAtTopLevel(contract, writer, graph, declaredType.TypeHandle, graphType);
            }
        }
    }

    internal DataContract GetDataContract(DataContract declaredTypeContract, Type declaredType, Type objectType)
    {
        if (declaredType.IsInterface && CollectionDataContract.IsCollectionInterface(declaredType))
            return declaredTypeContract;

        if (declaredType.IsArray) //Array covariance is not supported in XSD
            return declaredTypeContract;

        return DataContractProvider.GetDataContract(objectType);
    }

    internal override void InternalWriteEndObject(XmlWriterDelegator writer)
    {
        if (!IsRootXmlAny(_rootName, RootContract)) 
            writer.WriteEndElement();
    }

    internal override object? InternalReadObject(XmlReaderDelegator xmlReader, bool verifyObjectName)
    {
        if (MaxItemsInObjectGraph == 0)
            throw CreateSerializationException(SR.Format(SR.ExceededMaxItemsQuota, MaxItemsInObjectGraph));

        if (verifyObjectName)
        {
            if (!InternalIsStartObject(xmlReader))
            {
                XmlDictionaryString? expectedName;
                XmlDictionaryString? expectedNs;
                if (_rootName == null)
                {
                    expectedName = RootContract.TopLevelElementName;
                    expectedNs = RootContract.TopLevelElementNamespace;
                }
                else
                {
                    expectedName = _rootName;
                    expectedNs = _rootNamespace;
                }

                throw CreateSerializationExceptionWithReaderDetails(SR.Format(SR.ExpectingElement, expectedNs, expectedName), xmlReader);
            }
        }
        else if (!IsStartElement(xmlReader))
        {
            throw CreateSerializationExceptionWithReaderDetails(SR.Format(SR.ExpectingElementAtDeserialize, XmlNodeType.Element), xmlReader);
        }

        var contract = RootContract;
        if (contract.IsPrimitive && ReferenceEquals(contract.UnderlyingType, _rootType) /*handle Nullable<T> differently*/)
            return contract.ReadXmlValue(xmlReader, null);

        if (IsRootXmlAny(_rootName, contract))
            return XmlObjectSerializerReadContext.ReadRootIXmlSerializable(xmlReader, (contract as XmlDataContract)!, false /*isMemberType*/);

        var context = XmlObjectSerializerReadContext.CreateContext(this, contract);
        return context.InternalDeserialize(xmlReader, _rootType, contract, null, null);
    }

    internal override bool InternalIsStartObject(XmlReaderDelegator reader)
    {
        return IsRootElement(reader, RootContract, _rootName, _rootNamespace);
    }

    internal override Type GetSerializeType(object? graph)
    {
        return graph == null ? _rootType : graph.GetType();
    }

    internal override Type GetDeserializeType()
    {
        return _rootType;
    }
}