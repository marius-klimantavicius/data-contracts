// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using System.Xml;
using System.Xml.Serialization;

namespace Marius.DataContracts.Runtime;

public class XmlObjectSerializerWriteContext : XmlObjectSerializerContext
{
    private ObjectReferenceStack _byValObjectsInScope;
    private XmlSerializableWriter? _xmlSerializableWriter;
    private const int depthToCheckCyclicReference = 512;
    private bool _isGetOnlyCollection;
    protected bool _preserveObjectReferences;

    public static XmlObjectSerializerWriteContext CreateContext(DataContractSerializer serializer, DataContract rootTypeDataContract)
    {
        return serializer.PreserveObjectReferences
            ? new XmlObjectSerializerWriteContextComplex(serializer, rootTypeDataContract)
            : new XmlObjectSerializerWriteContext(serializer, rootTypeDataContract);
    }

    protected XmlObjectSerializerWriteContext(DataContractSerializer serializer, DataContract rootTypeDataContract)
        : base(serializer, rootTypeDataContract)
    {
        SerializeReadOnlyTypes = serializer.SerializeReadOnlyTypes;
        // Known types restricts the set of types that can be deserialized
        UnsafeTypeForwardingEnabled = true;
    }

    public XmlObjectSerializerWriteContext(XmlObjectSerializer serializer, int maxItemsInObjectGraph, StreamingContext streamingContext, bool ignoreExtensionDataObject)
        : base(serializer, maxItemsInObjectGraph, streamingContext, ignoreExtensionDataObject)
    {
        // Known types restricts the set of types that can be deserialized
        UnsafeTypeForwardingEnabled = true;
    }

    private ObjectToIdCache? _serializedObjects;
    protected ObjectToIdCache SerializedObjects => _serializedObjects ??= new ObjectToIdCache();

    public override bool IsGetOnlyCollection
    {
        get => _isGetOnlyCollection;
        set => _isGetOnlyCollection = value;
    }

    public bool SerializeReadOnlyTypes { get; }

    public bool UnsafeTypeForwardingEnabled { get; }

    public void StoreIsGetOnlyCollection()
    {
        _isGetOnlyCollection = true;
    }

    public void ResetIsGetOnlyCollection()
    {
        _isGetOnlyCollection = false;
    }

    public void InternalSerializeReference(XmlWriterDelegator xmlWriter, object obj, bool isDeclaredType, bool writeXsiType, RuntimeTypeHandle declaredTypeHandle)
    {
        if (!OnHandleReference(xmlWriter, obj, true /*canContainCyclicReference*/))
            InternalSerialize(xmlWriter, obj, isDeclaredType, writeXsiType, declaredTypeHandle);
        OnEndHandleReference(xmlWriter, obj, true /*canContainCyclicReference*/);
    }

    public virtual void InternalSerialize(XmlWriterDelegator xmlWriter, object obj, bool isDeclaredType, bool writeXsiType, RuntimeTypeHandle declaredTypeHandle)
    {
        if (writeXsiType)
        {
            var declaredType = Globals.TypeOfObject;
            SerializeWithXsiType(xmlWriter, obj, obj.GetType().TypeHandle, null /*type*/, declaredType.TypeHandle, declaredType);
        }
        else if (isDeclaredType)
        {
            var contract = GetDataContract(declaredTypeHandle, null);
            SerializeWithoutXsiType(contract, xmlWriter, obj, declaredTypeHandle);
        }
        else
        {
            var objTypeHandle = obj.GetType().TypeHandle;
            if (declaredTypeHandle.Value == objTypeHandle.Value)
            {
                var dataContract = GetDataContract(declaredTypeHandle, null /*type*/);
                SerializeWithoutXsiType(dataContract, xmlWriter, obj, declaredTypeHandle);
            }
            else
            {
                SerializeWithXsiType(xmlWriter, obj, objTypeHandle, null /*type*/, declaredTypeHandle, Type.GetTypeFromHandle(declaredTypeHandle)!);
            }
        }
    }

    public void SerializeWithoutXsiType(DataContract dataContract, XmlWriterDelegator xmlWriter, object obj, RuntimeTypeHandle declaredTypeHandle)
    {
        if (OnHandleIsReference(xmlWriter, dataContract, obj))
            return;

        if (dataContract.KnownDataContracts.Count > 0)
        {
            ScopedKnownTypes.Push(dataContract.KnownDataContracts);
            WriteDataContractValue(dataContract, xmlWriter, obj, declaredTypeHandle);
            ScopedKnownTypes.Pop();
        }
        else
        {
            WriteDataContractValue(dataContract, xmlWriter, obj, declaredTypeHandle);
        }
    }

    public virtual void SerializeWithXsiTypeAtTopLevel(DataContract dataContract, XmlWriterDelegator xmlWriter, object obj, RuntimeTypeHandle originalDeclaredTypeHandle, Type graphType)
    {
        Debug.Assert(rootTypeDataContract != null);

        var verifyKnownType = false;
        var declaredType = rootTypeDataContract.OriginalUnderlyingType;

        if (declaredType.IsInterface && CollectionDataContract.IsCollectionInterface(declaredType))
        {
            WriteResolvedTypeInfo(xmlWriter, graphType, declaredType);
        }
        else if (!declaredType.IsArray) //Array covariance is not supported in XSD. If declared type is array do not write xsi:type. Instead, write xsi:type for each item
        {
            verifyKnownType = WriteTypeInfo(xmlWriter, dataContract, rootTypeDataContract);
        }

        SerializeAndVerifyType(dataContract, xmlWriter, obj, verifyKnownType, originalDeclaredTypeHandle, declaredType);
    }

    protected virtual void SerializeWithXsiType(XmlWriterDelegator xmlWriter, object obj, RuntimeTypeHandle objectTypeHandle, Type? objectType, RuntimeTypeHandle declaredTypeHandle, Type declaredType)
    {
        var verifyKnownType = false;
        DataContract dataContract;
        if (declaredType.IsInterface && CollectionDataContract.IsCollectionInterface(declaredType))
        {
            dataContract = GetDataContractSkipValidation(objectTypeHandle, objectType);
            if (OnHandleIsReference(xmlWriter, dataContract, obj))
                return;

            dataContract = GetDataContract(declaredTypeHandle, declaredType);
            WriteClrTypeInfo(xmlWriter, dataContract);
        }
        else if (declaredType.IsArray) //Array covariance is not supported in XSD. If declared type is array do not write xsi:type. Instead, write xsi:type for each item
        {
            // A call to OnHandleIsReference is not necessary here -- arrays cannot be IsReference
            dataContract = GetDataContract(objectTypeHandle, objectType);
            WriteClrTypeInfo(xmlWriter, dataContract);
            dataContract = GetDataContract(declaredTypeHandle, declaredType);
        }
        else
        {
            dataContract = GetDataContract(objectTypeHandle, objectType);
            if (OnHandleIsReference(xmlWriter, dataContract, obj))
                return;

            if (!WriteClrTypeInfo(xmlWriter, dataContract))
            {
                var declaredTypeContract = GetDataContract(declaredTypeHandle, declaredType);
                verifyKnownType = WriteTypeInfo(xmlWriter, dataContract, declaredTypeContract);
            }
        }

        SerializeAndVerifyType(dataContract, xmlWriter, obj, verifyKnownType, declaredTypeHandle, declaredType);
    }

    public bool OnHandleIsReference(XmlWriterDelegator xmlWriter, DataContract contract, object obj)
    {
        if (_preserveObjectReferences || !contract.IsReference || _isGetOnlyCollection)
        {
            return false;
        }

        var isNew = true;
        var objectId = SerializedObjects.GetId(obj, ref isNew);
        _byValObjectsInScope.EnsureSetAsIsReference(obj);
        if (isNew)
        {
            xmlWriter.WriteAttributeString(Globals.SerPrefix, DictionaryGlobals.IdLocalName,
                DictionaryGlobals.SerializationNamespace, string.Create(CultureInfo.InvariantCulture, $"i{objectId}"));
            return false;
        }

        xmlWriter.WriteAttributeString(Globals.SerPrefix, DictionaryGlobals.RefLocalName, DictionaryGlobals.SerializationNamespace, string.Create(CultureInfo.InvariantCulture, $"i{objectId}"));
        return true;
    }

    protected void SerializeAndVerifyType(DataContract dataContract, XmlWriterDelegator xmlWriter, object obj, bool verifyKnownType, RuntimeTypeHandle declaredTypeHandle, Type declaredType)
    {
        var knownTypesAddedInCurrentScope = false;
        if (dataContract.KnownDataContracts.Count > 0)
        {
            ScopedKnownTypes.Push(dataContract.KnownDataContracts);
            knownTypesAddedInCurrentScope = true;
        }

        if (verifyKnownType)
        {
            if (!IsKnownType(dataContract, declaredType))
            {
                var knownContract = ResolveDataContractFromKnownTypes(dataContract.XmlName.Name, dataContract.XmlName.Namespace, null /*memberTypeContract*/, declaredType);
                if (knownContract == null || knownContract.UnderlyingType != dataContract.UnderlyingType)
                    throw XmlObjectSerializer.CreateSerializationException(SR.Format(SR.DcTypeNotFoundOnSerialize, DataContract.GetClrTypeFullName(dataContract.UnderlyingType), dataContract.XmlName.Name, dataContract.XmlName.Namespace));
            }
        }

        WriteDataContractValue(dataContract, xmlWriter, obj, declaredTypeHandle);

        if (knownTypesAddedInCurrentScope)
            ScopedKnownTypes.Pop();
    }

    public virtual bool WriteClrTypeInfo(XmlWriterDelegator xmlWriter, DataContract dataContract)
    {
        return false;
    }

    public virtual bool WriteClrTypeInfo(XmlWriterDelegator xmlWriter, string clrTypeName, string clrAssemblyName)
    {
        return false;
    }

    public virtual bool WriteClrTypeInfo(XmlWriterDelegator xmlWriter, Type dataContractType, string? clrTypeName, string? clrAssemblyName)
    {
        return false;
    }

    public virtual bool WriteClrTypeInfo(XmlWriterDelegator xmlWriter, Type dataContractType, SerializationInfo serInfo)
    {
        return false;
    }

    public virtual void WriteAnyType(XmlWriterDelegator xmlWriter, object value)
    {
        xmlWriter.WriteAnyType(value);
    }

    public virtual void WriteString(XmlWriterDelegator xmlWriter, string value)
    {
        xmlWriter.WriteString(value);
    }

    public virtual void WriteString(XmlWriterDelegator xmlWriter, string? value, XmlDictionaryString name, XmlDictionaryString? ns)
    {
        if (value == null)
        {
            WriteNull(xmlWriter, typeof(string), true /*isMemberTypeSerializable*/, name, ns);
        }
        else
        {
            xmlWriter.WriteStartElementPrimitive(name, ns);
            xmlWriter.WriteString(value);
            xmlWriter.WriteEndElementPrimitive();
        }
    }

    public virtual void WriteBase64(XmlWriterDelegator xmlWriter, byte[] value)
    {
        xmlWriter.WriteBase64(value);
    }

    public virtual void WriteBase64(XmlWriterDelegator xmlWriter, byte[]? value, XmlDictionaryString name, XmlDictionaryString ns)
    {
        if (value == null)
        {
            WriteNull(xmlWriter, typeof(byte[]), true /*isMemberTypeSerializable*/, name, ns);
        }
        else
        {
            xmlWriter.WriteStartElementPrimitive(name, ns);
            xmlWriter.WriteBase64(value);
            xmlWriter.WriteEndElementPrimitive();
        }
    }

    public virtual void WriteUri(XmlWriterDelegator xmlWriter, Uri value)
    {
        xmlWriter.WriteUri(value);
    }

    public virtual void WriteUri(XmlWriterDelegator xmlWriter, Uri? value, XmlDictionaryString name, XmlDictionaryString ns)
    {
        if (value == null)
        {
            WriteNull(xmlWriter, typeof(Uri), true /*isMemberTypeSerializable*/, name, ns);
        }
        else
        {
            xmlWriter.WriteStartElementPrimitive(name, ns);
            xmlWriter.WriteUri(value);
            xmlWriter.WriteEndElementPrimitive();
        }
    }

    public virtual void WriteQName(XmlWriterDelegator xmlWriter, XmlQualifiedName value)
    {
        xmlWriter.WriteQName(value);
    }

    public virtual void WriteQName(XmlWriterDelegator xmlWriter, XmlQualifiedName? value, XmlDictionaryString name, XmlDictionaryString? ns)
    {
        if (value == null)
            WriteNull(xmlWriter, typeof(XmlQualifiedName), true /*isMemberTypeSerializable*/, name, ns);
        else
        {
            if (!string.IsNullOrEmpty(ns?.Value))
                xmlWriter.WriteStartElement(Globals.ElementPrefix, name, ns);
            else
                xmlWriter.WriteStartElement(name, ns);
            xmlWriter.WriteQName(value);
            xmlWriter.WriteEndElement();
        }
    }

    public void HandleGraphAtTopLevel(XmlWriterDelegator writer, object obj, DataContract contract)
    {
        writer.WriteXmlnsAttribute(Globals.XsiPrefix, DictionaryGlobals.SchemaInstanceNamespace);
        if (contract.IsISerializable)
            writer.WriteXmlnsAttribute(Globals.XsdPrefix, DictionaryGlobals.SchemaNamespace);

        OnHandleReference(writer, obj, true /*canContainReferences*/);
    }

    public virtual bool OnHandleReference(XmlWriterDelegator xmlWriter, object obj, bool canContainCyclicReference)
    {
        if (xmlWriter.depth < depthToCheckCyclicReference)
            return false;

        if (canContainCyclicReference)
        {
            if (_byValObjectsInScope.Contains(obj))
                throw XmlObjectSerializer.CreateSerializationException(SR.Format(SR.CannotSerializeObjectWithCycles, DataContract.GetClrTypeFullName(obj.GetType())));

            _byValObjectsInScope.Push(obj);
        }

        return false;
    }

    public virtual void OnEndHandleReference(XmlWriterDelegator xmlWriter, object obj, bool canContainCyclicReference)
    {
        if (xmlWriter.depth < depthToCheckCyclicReference)
            return;

        if (canContainCyclicReference)
        {
            _byValObjectsInScope.Pop(obj);
        }
    }

    public void WriteNull(XmlWriterDelegator xmlWriter, Type memberType, bool isMemberTypeSerializable)
    {
        CheckIfTypeSerializable(memberType, isMemberTypeSerializable);
        WriteNull(xmlWriter);
    }

    public void WriteNull(XmlWriterDelegator xmlWriter, Type memberType, bool isMemberTypeSerializable, XmlDictionaryString name, XmlDictionaryString? ns)
    {
        xmlWriter.WriteStartElement(name, ns);
        WriteNull(xmlWriter, memberType, isMemberTypeSerializable);
        xmlWriter.WriteEndElement();
    }

    public void IncrementArrayCount(XmlWriterDelegator xmlWriter, Array array)
    {
        IncrementCollectionCount(xmlWriter, array.GetLength(0));
    }

    public void IncrementCollectionCount(XmlWriterDelegator xmlWriter, ICollection collection)
    {
        IncrementCollectionCount(xmlWriter, collection.Count);
    }

    public void IncrementCollectionCountGeneric<T>(XmlWriterDelegator xmlWriter, ICollection<T> collection)
    {
        IncrementCollectionCount(xmlWriter, collection.Count);
    }

    private void IncrementCollectionCount(XmlWriterDelegator xmlWriter, int size)
    {
        IncrementItemCount(size);
        WriteArraySize(xmlWriter, size);
    }

    public virtual void WriteArraySize(XmlWriterDelegator xmlWriter, int size)
    {
    }

    public static bool IsMemberTypeSameAsMemberValue(object? obj, Type? memberType)
    {
        if (obj == null || memberType == null)
            return false;

        return obj.GetType().TypeHandle.Equals(memberType.TypeHandle);
    }

    public static T GetDefaultValue<T>()
    {
        return default(T)!;
    }

    public static T GetNullableValue<T>(T? value) where T : struct
    {
        // value.Value will throw if hasValue is false
        return value!.Value;
    }

    public static void ThrowRequiredMemberMustBeEmitted(string memberName, Type type)
    {
        throw new SerializationException(SR.Format(SR.RequiredMemberMustBeEmitted, memberName, type.FullName));
    }

    public static bool GetHasValue<T>(T? value) where T : struct
    {
        return value.HasValue;
    }

    public void WriteIXmlSerializable(XmlWriterDelegator xmlWriter, object obj)
    {
        _xmlSerializableWriter ??= new XmlSerializableWriter();
        WriteIXmlSerializable(xmlWriter, obj, _xmlSerializableWriter);
    }

    public static void WriteRootIXmlSerializable(XmlWriterDelegator xmlWriter, object obj)
    {
        WriteIXmlSerializable(xmlWriter, obj, new XmlSerializableWriter());
    }

    private static void WriteIXmlSerializable(XmlWriterDelegator xmlWriter, object obj, XmlSerializableWriter xmlSerializableWriter)
    {
        xmlSerializableWriter.BeginWrite(xmlWriter.Writer, obj);
        switch (obj)
        {
            case IXmlSerializable xmlSerializable:
                xmlSerializable.WriteXml(xmlSerializableWriter);
                break;
            case XmlElement xmlElement:
                xmlElement.WriteTo(xmlSerializableWriter);
                break;
            case XmlNode[] xmlNodes:
            {
                foreach (var xmlNode in xmlNodes)
                    xmlNode.WriteTo(xmlSerializableWriter);
                break;
            }
            default:
                throw XmlObjectSerializer.CreateSerializationException(SR.Format(SR.UnknownXmlType, DataContract.GetClrTypeFullName(obj.GetType())));
        }

        xmlSerializableWriter.EndWrite();
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void GetObjectData(ISerializable obj, SerializationInfo serInfo, StreamingContext context)
    {
#pragma warning disable SYSLIB0050 // ISerializable.GetObjectData is obsolete
        obj.GetObjectData(serInfo, context);
#pragma warning restore SYSLIB0050
    }

    public void WriteISerializable(XmlWriterDelegator xmlWriter, ISerializable obj)
    {
        var objType = obj.GetType();
#pragma warning disable SYSLIB0050 // SerializationInfo ctor is obsolete
        var serInfo = new SerializationInfo(objType, XmlObjectSerializer.FormatterConverter);
#pragma warning restore SYSLIB0050
        GetObjectData(obj, serInfo, GetStreamingContext());

        WriteSerializationInfo(xmlWriter, objType, serInfo);
    }

    public void WriteSerializationInfo(XmlWriterDelegator xmlWriter, Type objType, SerializationInfo serInfo)
    {
        if (DataContract.GetClrTypeFullName(objType) != serInfo.FullTypeName)
        {
            DataContract.GetDefaultXmlName(serInfo.FullTypeName, out var typeName, out var typeNs);
            xmlWriter.WriteAttributeQualifiedName(Globals.SerPrefix, DictionaryGlobals.ISerializableFactoryTypeLocalName, DictionaryGlobals.SerializationNamespace, DataContract.GetClrTypeString(typeName), DataContract.GetClrTypeString(typeNs));
        }

        WriteClrTypeInfo(xmlWriter, objType, serInfo);
        IncrementItemCount(serInfo.MemberCount);
        foreach (var serEntry in serInfo)
        {
            var name = DataContract.GetClrTypeString(DataContract.EncodeLocalName(serEntry.Name));
            xmlWriter.WriteStartElement(name, DictionaryGlobals.EmptyString);
            var obj = serEntry.Value;
            if (obj == null)
                WriteNull(xmlWriter);
            else
                InternalSerializeReference(xmlWriter, obj, false /*isDeclaredType*/, false /*writeXsiType*/, Globals.TypeOfObject.TypeHandle);

            xmlWriter.WriteEndElement();
        }
    }

    protected virtual void WriteDataContractValue(DataContract dataContract, XmlWriterDelegator xmlWriter, object obj, RuntimeTypeHandle declaredTypeHandle)
    {
        dataContract.WriteXmlValue(xmlWriter, obj, this);
    }

    protected virtual void WriteNull(XmlWriterDelegator xmlWriter)
    {
        XmlObjectSerializer.WriteNull(xmlWriter);
    }

    private void WriteResolvedTypeInfo(XmlWriterDelegator writer, Type objectType, Type declaredType)
    {
        if (ResolveType(objectType, declaredType, out var typeName, out var typeNamespace))
            WriteTypeInfo(writer, typeName, typeNamespace);
    }

    private bool ResolveType(Type objectType, Type declaredType, [NotNullWhen(true)] out XmlDictionaryString? typeName, [NotNullWhen(true)] out XmlDictionaryString? typeNamespace)
    {
        if (!TryResolveType(objectType, declaredType, out typeName, out typeNamespace))
            throw XmlObjectSerializer.CreateSerializationException(SR.Format(SR.ResolveTypeReturnedFalse, DataContract.GetClrTypeFullName(DataContractProvider.GetType()), DataContract.GetClrTypeFullName(objectType)));

        if (typeName == null)
        {
            if (typeNamespace == null)
                return false;

            throw XmlObjectSerializer.CreateSerializationException(SR.Format(SR.ResolveTypeReturnedNull, DataContract.GetClrTypeFullName(DataContractProvider.GetType()), DataContract.GetClrTypeFullName(objectType)));
        }

        if (typeNamespace == null)
            throw XmlObjectSerializer.CreateSerializationException(SR.Format(SR.ResolveTypeReturnedNull, DataContract.GetClrTypeFullName(DataContractProvider.GetType()), DataContract.GetClrTypeFullName(objectType)));

        return true;
    }

    private bool TryResolveType(Type? type, Type? declaredType, out XmlDictionaryString? typeName, out XmlDictionaryString? typeNamespace)
    {
        if (type == null)
        {
            typeName = null;
            typeNamespace = null;
            return false;
        }

        if (declaredType != null && declaredType.IsInterface && CollectionDataContract.IsCollectionInterface(declaredType))
        {
            typeName = null;
            typeNamespace = null;
            return true;
        }

        var contract = GetDataContract(type);
        if (IsKnownType(contract, contract.KnownDataContracts, declaredType))
        {
            typeName = contract.Name;
            typeNamespace = contract.Namespace;
            return true;
        }

        typeName = null;
        typeNamespace = null;
        return false;
    }

    protected virtual bool WriteTypeInfo(XmlWriterDelegator writer, DataContract contract, DataContract declaredContract)
    {
        if (!XmlObjectSerializer.IsContractDeclared(contract, declaredContract))
            WriteResolvedTypeInfo(writer, contract.OriginalUnderlyingType, declaredContract.OriginalUnderlyingType);

        return false;
    }

    protected virtual void WriteTypeInfo(XmlWriterDelegator writer, string dataContractName, string? dataContractNamespace)
    {
        writer.WriteAttributeQualifiedName(Globals.XsiPrefix, DictionaryGlobals.XsiTypeLocalName, DictionaryGlobals.SchemaInstanceNamespace, dataContractName, dataContractNamespace);
    }

    protected virtual void WriteTypeInfo(XmlWriterDelegator writer, XmlDictionaryString dataContractName, XmlDictionaryString dataContractNamespace)
    {
        writer.WriteAttributeQualifiedName(Globals.XsiPrefix, DictionaryGlobals.XsiTypeLocalName, DictionaryGlobals.SchemaInstanceNamespace, dataContractName, dataContractNamespace);
    }

    public void WriteExtensionData(XmlWriterDelegator xmlWriter, ExtensionDataObject? extensionData, int memberIndex)
    {
        if (IgnoreExtensionDataObject || extensionData == null)
            return;

        var members = extensionData.Members;
        if (members != null)
        {
            for (var i = 0; i < members.Count; i++)
            {
                var member = members[i];
                if (member.MemberIndex == memberIndex)
                {
                    WriteExtensionDataMember(xmlWriter, member);
                }
            }
        }
    }

    private void WriteExtensionDataMember(XmlWriterDelegator xmlWriter, ExtensionDataMember member)
    {
        xmlWriter.WriteStartElement(member.Name, member.Namespace);
        var dataNode = member.Value;
        WriteExtensionDataValue(xmlWriter, dataNode);
        xmlWriter.WriteEndElement();
    }

    public virtual void WriteExtensionDataTypeInfo(XmlWriterDelegator xmlWriter, IDataNode dataNode)
    {
        if (dataNode.DataContractName != null)
            WriteTypeInfo(xmlWriter, dataNode.DataContractName, dataNode.DataContractNamespace);

        WriteClrTypeInfo(xmlWriter, dataNode.DataType, dataNode.ClrTypeName, dataNode.ClrAssemblyName);
    }

    public void WriteExtensionDataValue(XmlWriterDelegator xmlWriter, IDataNode? dataNode)
    {
        IncrementItemCount(1);
        if (dataNode == null)
        {
            WriteNull(xmlWriter);
            return;
        }

        if (dataNode.PreservesReferences
            && OnHandleReference(xmlWriter, dataNode.Value ?? dataNode, canContainCyclicReference: true))
            return;

        var dataType = dataNode.DataType;
        if (dataType == Globals.TypeOfClassDataNode)
            WriteExtensionClassData(xmlWriter, (ClassDataNode)dataNode);
        else if (dataType == Globals.TypeOfCollectionDataNode)
            WriteExtensionCollectionData(xmlWriter, (CollectionDataNode)dataNode);
        else if (dataType == Globals.TypeOfXmlDataNode)
            WriteExtensionXmlData(xmlWriter, (XmlDataNode)dataNode);
        else if (dataType == Globals.TypeOfISerializableDataNode)
            WriteExtensionISerializableData(xmlWriter, (ISerializableDataNode)dataNode);
        else
        {
            WriteExtensionDataTypeInfo(xmlWriter, dataNode);

            if (dataType == Globals.TypeOfObject)
            {
                // NOTE: serialize value in DataNode<object> since it may contain non-primitive
                // deserialized object (ex. empty class)
                var o = dataNode.Value;
                if (o != null)
                    InternalSerialize(xmlWriter, o, false /*isDeclaredType*/, false /*writeXsiType*/, o.GetType().TypeHandle);
            }
            else
                xmlWriter.WriteExtensionData(dataNode);
        }

        if (dataNode.PreservesReferences)
            OnEndHandleReference(xmlWriter, dataNode.Value ?? dataNode, true /*canContainCyclicReference*/);
    }

    public bool TryWriteDeserializedExtensionData(XmlWriterDelegator xmlWriter, IDataNode dataNode)
    {
        var o = dataNode.Value;
        if (o == null)
            return false;

        var declaredType = dataNode.DataContractName == null ? o.GetType() : Globals.TypeOfObject;
        InternalSerialize(xmlWriter, o, false /*isDeclaredType*/, false /*writeXsiType*/, declaredType.TypeHandle);
        return true;
    }

    private void WriteExtensionClassData(XmlWriterDelegator xmlWriter, ClassDataNode dataNode)
    {
        if (!TryWriteDeserializedExtensionData(xmlWriter, dataNode))
        {
            WriteExtensionDataTypeInfo(xmlWriter, dataNode);

            var members = dataNode.Members;
            if (members != null)
            {
                for (var i = 0; i < members.Count; i++)
                {
                    WriteExtensionDataMember(xmlWriter, members[i]);
                }
            }
        }
    }

    private void WriteExtensionCollectionData(XmlWriterDelegator xmlWriter, CollectionDataNode dataNode)
    {
        if (!TryWriteDeserializedExtensionData(xmlWriter, dataNode))
        {
            WriteExtensionDataTypeInfo(xmlWriter, dataNode);

            WriteArraySize(xmlWriter, dataNode.Size);

            var items = dataNode.Items;
            if (items != null)
            {
                for (var i = 0; i < items.Count; i++)
                {
                    xmlWriter.WriteStartElement(dataNode.ItemName!, dataNode.ItemNamespace);
                    WriteExtensionDataValue(xmlWriter, items[i]);
                    xmlWriter.WriteEndElement();
                }
            }
        }
    }

    private void WriteExtensionISerializableData(XmlWriterDelegator xmlWriter, ISerializableDataNode dataNode)
    {
        if (!TryWriteDeserializedExtensionData(xmlWriter, dataNode))
        {
            WriteExtensionDataTypeInfo(xmlWriter, dataNode);

            if (dataNode.FactoryTypeName != null)
                xmlWriter.WriteAttributeQualifiedName(Globals.SerPrefix, DictionaryGlobals.ISerializableFactoryTypeLocalName, DictionaryGlobals.SerializationNamespace, dataNode.FactoryTypeName, dataNode.FactoryTypeNamespace);

            var members = dataNode.Members;
            if (members != null)
            {
                for (var i = 0; i < members.Count; i++)
                {
                    var member = members[i];
                    xmlWriter.WriteStartElement(member.Name, string.Empty);
                    WriteExtensionDataValue(xmlWriter, member.Value);
                    xmlWriter.WriteEndElement();
                }
            }
        }
    }

    private void WriteExtensionXmlData(XmlWriterDelegator xmlWriter, XmlDataNode dataNode)
    {
        if (!TryWriteDeserializedExtensionData(xmlWriter, dataNode))
        {
            var xmlAttributes = dataNode.XmlAttributes;
            if (xmlAttributes != null)
            {
                foreach (var attribute in xmlAttributes)
                    attribute.WriteTo(xmlWriter.Writer);
            }

            WriteExtensionDataTypeInfo(xmlWriter, dataNode);

            var xmlChildNodes = dataNode.XmlChildNodes;
            if (xmlChildNodes != null)
            {
                foreach (var node in xmlChildNodes)
                    node.WriteTo(xmlWriter.Writer);
            }
        }
    }
}