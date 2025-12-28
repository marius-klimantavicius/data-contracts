using System.Diagnostics;
using System.Reflection;
using System.Runtime.Serialization;
using System.Xml;

namespace Marius.DataContracts.SourceGenerators.DataContracts;

internal abstract class ReflectionClassWriter
{
    public void ReflectionWriteClass(XmlWriterDelegator xmlWriter, object obj, XmlObjectSerializerWriteContext context, ClassDataContract classContract, XmlDictionaryString[]? memberNames)
    {
        InvokeOnSerializing(obj, context, classContract);
        obj = ResolveAdapterType(obj);
        if (classContract.IsISerializable)
        {
            context.WriteISerializable(xmlWriter, (ISerializable)obj);
        }
        else
        {
            if (classContract.HasExtensionData)
            {
                context.WriteExtensionData(xmlWriter, ((IExtensibleDataObject)obj).ExtensionData, -1);
            }

            ReflectionWriteMembers(xmlWriter, obj, context, classContract, classContract, 0 /*childElementIndex*/, memberNames);
        }

        InvokeOnSerialized(obj, context, classContract);
    }

    public static void ReflectionWriteValue(XmlWriterDelegator xmlWriter, XmlObjectSerializerWriteContext context, Type type, object? value, bool writeXsiType, PrimitiveDataContract? primitiveContractForParamType)
    {
        Type memberType = type;
        object? memberValue = value;
        bool originValueIsNullableOfT = (memberType.IsGenericType && memberType.GetGenericTypeDefinition() == Globals.TypeOfNullable);
        if (memberType.IsValueType && !originValueIsNullableOfT)
        {
            Debug.Assert(memberValue != null);

            PrimitiveDataContract? primitiveContract = primitiveContractForParamType;
            if (primitiveContract != null && !writeXsiType)
            {
                primitiveContract.WriteXmlValue(xmlWriter, memberValue, context);
            }
            else
            {
                ReflectionInternalSerialize(xmlWriter, context, memberValue, memberValue.GetType().TypeHandle.Equals(memberType.TypeHandle), writeXsiType, memberType);
            }
        }
        else
        {
            if (originValueIsNullableOfT)
            {
                if (memberValue == null)
                {
                    memberType = Nullable.GetUnderlyingType(memberType)!;
                }
                else
                {
                    MethodInfo getValue = memberType.GetMethod("get_Value", Type.EmptyTypes)!;
                    memberValue = getValue.Invoke(memberValue, Array.Empty<object>())!;
                    memberType = memberValue.GetType();
                }
            }

            if (memberValue == null)
            {
                context.WriteNull(xmlWriter, memberType, DataContract.IsTypeSerializable(memberType));
            }
            else
            {
                PrimitiveDataContract? primitiveContract = originValueIsNullableOfT ? PrimitiveDataContract.GetPrimitiveDataContract(memberType) : primitiveContractForParamType;
                if (primitiveContract != null && primitiveContract.UnderlyingType != Globals.TypeOfObject && !writeXsiType)
                {
                    primitiveContract.WriteXmlValue(xmlWriter, memberValue, context);
                }
                else
                {
                    if (memberValue == null &&
                        (memberType == Globals.TypeOfObject
                            || (originValueIsNullableOfT && memberType.IsValueType)))
                    {
                        context.WriteNull(xmlWriter, memberType, DataContract.IsTypeSerializable(memberType));
                    }
                    else
                    {
                        ReflectionInternalSerialize(xmlWriter, context, memberValue!, memberValue!.GetType().TypeHandle.Equals(memberType.TypeHandle), writeXsiType, memberType, originValueIsNullableOfT);
                    }
                }
            }
        }
    }

    protected abstract int ReflectionWriteMembers(XmlWriterDelegator xmlWriter, object obj, XmlObjectSerializerWriteContext context, ClassDataContract classContract, ClassDataContract derivedMostClassContract, int childElementIndex, XmlDictionaryString[]? memberNames);

    protected static object? ReflectionGetMemberValue(object obj, DataMember dataMember)
    {
        return dataMember.Getter(obj);
    }

    protected static bool ReflectionTryWritePrimitive(XmlWriterDelegator xmlWriter, XmlObjectSerializerWriteContext context, object? value, XmlDictionaryString name, XmlDictionaryString? ns, PrimitiveDataContract? primitiveContract)
    {
        if (primitiveContract == null || primitiveContract.UnderlyingType == Globals.TypeOfObject)
            return false;

        primitiveContract.WriteXmlElement(xmlWriter, value, context, name, ns);

        return true;
    }

    private static void InvokeOnSerializing(object obj, XmlObjectSerializerWriteContext context, ClassDataContract classContract)
    {
        if (classContract.BaseClassContract != null)
            InvokeOnSerializing(obj, context, classContract.BaseClassContract);
        if (classContract.OnSerializing != null)
        {
            var contextArg = context.GetStreamingContext();
            classContract.OnSerializing.Invoke(obj, new object[] { contextArg });
        }
    }

    private static void InvokeOnSerialized(object obj, XmlObjectSerializerWriteContext context, ClassDataContract classContract)
    {
        if (classContract.BaseClassContract != null)
            InvokeOnSerialized(obj, context, classContract.BaseClassContract);
        if (classContract.OnSerialized != null)
        {
            var contextArg = context.GetStreamingContext();
            classContract.OnSerialized.Invoke(obj, new object[] { contextArg });
        }
    }

    private static object ResolveAdapterType(object obj)
    {
        Type type = obj.GetType();
        if (type == Globals.TypeOfDateTimeOffset)
        {
            obj = DateTimeOffsetAdapter.GetDateTimeOffsetAdapter((DateTimeOffset)obj);
        }
        else if (type == Globals.TypeOfMemoryStream)
        {
            obj = MemoryStreamAdapter.GetMemoryStreamAdapter((MemoryStream)obj);
        }

        return obj;
    }

    private static void ReflectionInternalSerialize(XmlWriterDelegator xmlWriter, XmlObjectSerializerWriteContext context, object obj, bool isDeclaredType, bool writeXsiType, Type memberType, bool isNullableOfT = false)
    {
        if (isNullableOfT)
        {
            context.InternalSerialize(xmlWriter, obj, isDeclaredType, writeXsiType, DataContract.GetId(memberType.TypeHandle), memberType.TypeHandle);
        }
        else
        {
            context.InternalSerializeReference(xmlWriter, obj, isDeclaredType, writeXsiType, DataContract.GetId(memberType.TypeHandle), memberType.TypeHandle);
        }
    }
}

internal sealed class ReflectionXmlFormatWriter
{
    private readonly ReflectionXmlClassWriter _reflectionClassWriter = new ReflectionXmlClassWriter();

    public void ReflectionWriteClass(XmlWriterDelegator xmlWriter, object obj, XmlObjectSerializerWriteContext context, ClassDataContract classContract)
    {
        _reflectionClassWriter.ReflectionWriteClass(xmlWriter, obj, context, classContract, null /*memberNames*/);
    }

    public static void ReflectionWriteCollection(XmlWriterDelegator xmlWriter, object obj, XmlObjectSerializerWriteContext context, CollectionDataContract collectionDataContract)
    {
        XmlDictionaryString ns = collectionDataContract.Namespace;
        XmlDictionaryString itemName = collectionDataContract.CollectionItemName;

        if (collectionDataContract.ChildElementNamespace != null)
        {
            xmlWriter.WriteNamespaceDecl(collectionDataContract.ChildElementNamespace);
        }

        if (collectionDataContract.Kind == CollectionKind.Array)
        {
            context.IncrementArrayCount(xmlWriter, (Array)obj);
            Type itemType = collectionDataContract.ItemType;
            if (!ReflectionTryWritePrimitiveArray(xmlWriter, obj, itemType, itemName, ns))
            {
                Array array = (Array)obj;
                PrimitiveDataContract? primitiveContract = PrimitiveDataContract.GetPrimitiveDataContract(itemType);
                for (int i = 0; i < array.Length; ++i)
                {
                    ReflectionXmlClassWriter.ReflectionWriteStartElement(xmlWriter, itemType, ns, ns.Value, itemName.Value);
                    ReflectionXmlClassWriter.ReflectionWriteValue(xmlWriter, context, itemType, array.GetValue(i), false, primitiveContract);
                    ReflectionXmlClassWriter.ReflectionWriteEndElement(xmlWriter);
                }
            }
        }
        else
        {
            collectionDataContract.IncrementCollectionCount(xmlWriter, obj, context);

            IEnumerator enumerator = collectionDataContract.GetEnumeratorForCollection(obj);
            PrimitiveDataContract? primitiveContractForType = PrimitiveDataContract.GetPrimitiveDataContract(collectionDataContract.UnderlyingType);

            if (primitiveContractForType != null && primitiveContractForType.UnderlyingType != Globals.TypeOfObject)
            {
                while (enumerator.MoveNext())
                {
                    object current = enumerator.Current;
                    context.IncrementItemCount(1);
                    primitiveContractForType.WriteXmlElement(xmlWriter, current, context, itemName, ns);
                }
            }
            else
            {
                Type elementType = collectionDataContract.GetCollectionElementType();
                bool isDictionary = collectionDataContract.Kind == CollectionKind.Dictionary || collectionDataContract.Kind == CollectionKind.GenericDictionary;
                while (enumerator.MoveNext())
                {
                    object current = enumerator.Current;
                    context.IncrementItemCount(1);
                    ReflectionXmlClassWriter.ReflectionWriteStartElement(xmlWriter, elementType, ns, ns.Value, itemName.Value);
                    if (isDictionary)
                    {
                        collectionDataContract.ItemContract.WriteXmlValue(xmlWriter, current, context);
                    }
                    else
                    {
                        ReflectionXmlClassWriter.ReflectionWriteValue(xmlWriter, context, elementType, current, false, primitiveContractForParamType: null);
                    }

                    ReflectionXmlClassWriter.ReflectionWriteEndElement(xmlWriter);
                }
            }
        }
    }

    private static bool ReflectionTryWritePrimitiveArray(XmlWriterDelegator xmlWriter, object obj, Type itemType, XmlDictionaryString collectionItemName, XmlDictionaryString itemNamespace)
    {
        PrimitiveDataContract? primitiveContract = PrimitiveDataContract.GetPrimitiveDataContract(itemType);
        if (primitiveContract == null)
            return false;

        switch (Type.GetTypeCode(itemType))
        {
            case TypeCode.Boolean:
                xmlWriter.WriteBooleanArray((bool[])obj, collectionItemName, itemNamespace);
                break;
            case TypeCode.DateTime:
                xmlWriter.WriteDateTimeArray((DateTime[])obj, collectionItemName, itemNamespace);
                break;
            case TypeCode.Decimal:
                xmlWriter.WriteDecimalArray((decimal[])obj, collectionItemName, itemNamespace);
                break;
            case TypeCode.Int32:
                xmlWriter.WriteInt32Array((int[])obj, collectionItemName, itemNamespace);
                break;
            case TypeCode.Int64:
                xmlWriter.WriteInt64Array((long[])obj, collectionItemName, itemNamespace);
                break;
            case TypeCode.Single:
                xmlWriter.WriteSingleArray((float[])obj, collectionItemName, itemNamespace);
                break;
            case TypeCode.Double:
                xmlWriter.WriteDoubleArray((double[])obj, collectionItemName, itemNamespace);
                break;
            default:
                return false;
        }

        return true;
    }
}

internal sealed class ReflectionXmlClassWriter : ReflectionClassWriter
{
    protected override int ReflectionWriteMembers(XmlWriterDelegator xmlWriter, object obj, XmlObjectSerializerWriteContext context, ClassDataContract classContract, ClassDataContract derivedMostClassContract, int childElementIndex, XmlDictionaryString[]? emptyStringArray)
    {
        int memberCount = (classContract.BaseClassContract == null) ? 0 : ReflectionWriteMembers(xmlWriter, obj, context, classContract.BaseClassContract, derivedMostClassContract, childElementIndex, emptyStringArray);

        childElementIndex += memberCount;

        XmlDictionaryString[] memberNames = classContract.MemberNames!;
        XmlDictionaryString ns = classContract.Namespace;
        context.IncrementItemCount(classContract.Members!.Count);
        for (int i = 0; i < classContract.Members.Count; i++, memberCount++)
        {
            DataMember member = classContract.Members[i];
            Type memberType = member.MemberType;
            if (member.IsGetOnlyCollection)
            {
                context.StoreIsGetOnlyCollection();
            }
            else
            {
                context.ResetIsGetOnlyCollection();
            }

            bool shouldWriteValue = true;
            object? memberValue = null;
            if (!member.EmitDefaultValue)
            {
                memberValue = ReflectionGetMemberValue(obj, member);
                object? defaultValue = XmlFormatGeneratorStatics.GetDefaultValue(memberType);
                if ((memberValue == null && defaultValue == null)
                    || (memberValue != null && memberValue.Equals(defaultValue)))
                {
                    shouldWriteValue = false;

                    if (member.IsRequired)
                    {
                        XmlObjectSerializerWriteContext.ThrowRequiredMemberMustBeEmitted(member.Name, classContract.UnderlyingType);
                    }
                }
            }

            if (shouldWriteValue)
            {
                bool writeXsiType = CheckIfMemberHasConflict(member, classContract, derivedMostClassContract);
                memberValue ??= ReflectionGetMemberValue(obj, member);
                PrimitiveDataContract? primitiveContract = member.MemberPrimitiveContract;

                if (writeXsiType || !ReflectionTryWritePrimitive(xmlWriter, context, memberValue, memberNames[i + childElementIndex] /*name*/, ns, primitiveContract))
                {
                    ReflectionWriteStartElement(xmlWriter, memberType, ns, ns.Value, member.Name);
                    if (classContract.ChildElementNamespaces![i + childElementIndex] != null)
                    {
                        var nsChildElement = classContract.ChildElementNamespaces[i + childElementIndex]!;
                        xmlWriter.WriteNamespaceDecl(nsChildElement);
                    }

                    ReflectionWriteValue(xmlWriter, context, memberType, memberValue, writeXsiType, primitiveContractForParamType: null);
                    ReflectionWriteEndElement(xmlWriter);
                }

                if (classContract.HasExtensionData)
                {
                    context.WriteExtensionData(xmlWriter, ((IExtensibleDataObject)obj).ExtensionData, memberCount);
                }
            }
        }

        return memberCount;
    }

    public static void ReflectionWriteStartElement(XmlWriterDelegator xmlWriter, Type type, XmlDictionaryString ns, string namespaceLocal, string nameLocal)
    {
        bool needsPrefix = NeedsPrefix(type, ns);

        if (needsPrefix)
        {
            xmlWriter.WriteStartElement(Globals.ElementPrefix, nameLocal, namespaceLocal);
        }
        else
        {
            xmlWriter.WriteStartElement(nameLocal, namespaceLocal);
        }
    }

    public static void ReflectionWriteEndElement(XmlWriterDelegator xmlWriter)
    {
        xmlWriter.WriteEndElement();
    }

    private static bool NeedsPrefix(Type type, XmlDictionaryString? ns)
    {
        return type == Globals.TypeOfXmlQualifiedName && (ns != null && ns.Value != null && ns.Value.Length > 0);
    }

    private static bool CheckIfMemberHasConflict(DataMember member, ClassDataContract classContract, ClassDataContract derivedMostClassContract)
    {
        // Check for conflict with base type members
        if (CheckIfConflictingMembersHaveDifferentTypes(member))
            return true;

        // Check for conflict with derived type members
        string? name = member.Name;
        string? ns = classContract.XmlName.Namespace;
        ClassDataContract? currentContract = derivedMostClassContract;
        while (currentContract != null && currentContract != classContract)
        {
            if (ns == currentContract.XmlName.Namespace)
            {
                List<DataMember> members = currentContract.Members!;
                for (int j = 0; j < members.Count; j++)
                {
                    if (name == members[j].Name)
                        return CheckIfConflictingMembersHaveDifferentTypes(members[j]);
                }
            }

            currentContract = currentContract.BaseClassContract;
        }

        return false;
    }

    private static bool CheckIfConflictingMembersHaveDifferentTypes(DataMember member)
    {
        while (member.ConflictingMember != null)
        {
            if (member.MemberType != member.ConflictingMember.MemberType)
                return true;

            member = member.ConflictingMember;
        }

        return false;
    }
}

internal class XmlObjectSerializerWriteContextComplex : XmlObjectSerializerWriteContext
{
    private readonly ISerializationSurrogateProvider? _serializationSurrogateProvider;

    internal XmlObjectSerializerWriteContextComplex(DataContractSerializer serializer, DataContract rootTypeDataContract, DataContractResolver? dataContractResolver)
        : base(serializer, rootTypeDataContract, dataContractResolver)
    {
        this.preserveObjectReferences = serializer.PreserveObjectReferences;
        _serializationSurrogateProvider = serializer.SerializationSurrogateProvider;
    }

    internal XmlObjectSerializerWriteContextComplex(XmlObjectSerializer serializer, int maxItemsInObjectGraph, StreamingContext streamingContext, bool ignoreExtensionDataObject)
        : base(serializer, maxItemsInObjectGraph, streamingContext, ignoreExtensionDataObject)
    {
    }

    internal override bool WriteClrTypeInfo(XmlWriterDelegator xmlWriter, DataContract dataContract)
    {
        return false;
    }

    internal override bool WriteClrTypeInfo(XmlWriterDelegator xmlWriter, Type dataContractType, string? clrTypeName, string? clrAssemblyName)
    {
        return false;
    }

    internal override void WriteAnyType(XmlWriterDelegator xmlWriter, object value)
    {
        if (!OnHandleReference(xmlWriter, value, false /*canContainCyclicReference*/))
            xmlWriter.WriteAnyType(value);
    }

    internal override void WriteString(XmlWriterDelegator xmlWriter, string value)
    {
        if (!OnHandleReference(xmlWriter, value, false /*canContainCyclicReference*/))
            xmlWriter.WriteString(value);
    }

    internal override void WriteString(XmlWriterDelegator xmlWriter, string? value, XmlDictionaryString name, XmlDictionaryString? ns)
    {
        if (value == null)
            WriteNull(xmlWriter, typeof(string), true /*isMemberTypeSerializable*/, name, ns);
        else
        {
            xmlWriter.WriteStartElementPrimitive(name, ns);
            if (!OnHandleReference(xmlWriter, value, false /*canContainCyclicReference*/))
                xmlWriter.WriteString(value);
            xmlWriter.WriteEndElementPrimitive();
        }
    }

    internal override void WriteBase64(XmlWriterDelegator xmlWriter, byte[] value)
    {
        if (!OnHandleReference(xmlWriter, value, false /*canContainCyclicReference*/))
            xmlWriter.WriteBase64(value);
    }

    internal override void WriteBase64(XmlWriterDelegator xmlWriter, byte[] value, XmlDictionaryString name, XmlDictionaryString ns)
    {
        if (value == null)
            WriteNull(xmlWriter, typeof(byte[]), true /*isMemberTypeSerializable*/, name, ns);
        else
        {
            xmlWriter.WriteStartElementPrimitive(name, ns);
            if (!OnHandleReference(xmlWriter, value, false /*canContainCyclicReference*/))
                xmlWriter.WriteBase64(value);
            xmlWriter.WriteEndElementPrimitive();
        }
    }

    internal override void WriteUri(XmlWriterDelegator xmlWriter, Uri value)
    {
        if (!OnHandleReference(xmlWriter, value, false /*canContainCyclicReference*/))
            xmlWriter.WriteUri(value);
    }

    internal override void WriteUri(XmlWriterDelegator xmlWriter, Uri value, XmlDictionaryString name, XmlDictionaryString ns)
    {
        if (value == null)
            WriteNull(xmlWriter, typeof(Uri), true /*isMemberTypeSerializable*/, name, ns);
        else
        {
            xmlWriter.WriteStartElementPrimitive(name, ns);
            if (!OnHandleReference(xmlWriter, value, false /*canContainCyclicReference*/))
                xmlWriter.WriteUri(value);
            xmlWriter.WriteEndElementPrimitive();
        }
    }

    internal override void WriteQName(XmlWriterDelegator xmlWriter, XmlQualifiedName value)
    {
        if (!OnHandleReference(xmlWriter, value, false /*canContainCyclicReference*/))
            xmlWriter.WriteQName(value);
    }

    internal override void WriteQName(XmlWriterDelegator xmlWriter, XmlQualifiedName? value, XmlDictionaryString name, XmlDictionaryString? ns)
    {
        if (value == null)
            WriteNull(xmlWriter, typeof(XmlQualifiedName), true /*isMemberTypeSerializable*/, name, ns);
        else
        {
            if (ns != null && ns.Value != null && ns.Value.Length > 0)
                xmlWriter.WriteStartElement(Globals.ElementPrefix, name, ns);
            else
                xmlWriter.WriteStartElement(name, ns);
            if (!OnHandleReference(xmlWriter, value, false /*canContainCyclicReference*/))
                xmlWriter.WriteQName(value);
            xmlWriter.WriteEndElement();
        }
    }

    internal override void InternalSerialize(XmlWriterDelegator xmlWriter, object obj, bool isDeclaredType, bool writeXsiType, int declaredTypeID, RuntimeTypeHandle declaredTypeHandle)
    {
        if (_serializationSurrogateProvider == null)
        {
            base.InternalSerialize(xmlWriter, obj, isDeclaredType, writeXsiType, declaredTypeID, declaredTypeHandle);
        }
        else
        {
            InternalSerializeWithSurrogate(xmlWriter, obj, isDeclaredType, writeXsiType, declaredTypeHandle);
        }
    }

    internal override bool OnHandleReference(XmlWriterDelegator xmlWriter, object obj, bool canContainCyclicReference)
    {
        if (preserveObjectReferences && !this.IsGetOnlyCollection)
        {
            bool isNew = true;
            int objectId = SerializedObjects.GetId(obj, ref isNew);
            if (isNew)
                xmlWriter.WriteAttributeInt(Globals.SerPrefix, DictionaryGlobals.IdLocalName, DictionaryGlobals.SerializationNamespace, objectId);
            else
            {
                xmlWriter.WriteAttributeInt(Globals.SerPrefix, DictionaryGlobals.RefLocalName, DictionaryGlobals.SerializationNamespace, objectId);
                xmlWriter.WriteAttributeBool(Globals.XsiPrefix, DictionaryGlobals.XsiNilLocalName, DictionaryGlobals.SchemaInstanceNamespace, true);
            }

            return !isNew;
        }

        return base.OnHandleReference(xmlWriter, obj, canContainCyclicReference);
    }

    internal override void OnEndHandleReference(XmlWriterDelegator xmlWriter, object obj, bool canContainCyclicReference)
    {
        if (preserveObjectReferences && !this.IsGetOnlyCollection)
            return;

        base.OnEndHandleReference(xmlWriter, obj, canContainCyclicReference);
    }

    internal override void CheckIfTypeSerializable(Type memberType, bool isMemberTypeSerializable)
    {
        if (_serializationSurrogateProvider != null)
        {
            while (memberType.IsArray)
                memberType = memberType.GetElementType()!;
            memberType = DataContractSurrogateCaller.GetDataContractType(_serializationSurrogateProvider, memberType);
            if (!DataContract.IsTypeSerializable(memberType))
                throw new InvalidDataContractException(SR.Format(SR.TypeNotSerializable, memberType));

            return;
        }

        base.CheckIfTypeSerializable(memberType, isMemberTypeSerializable);
    }

    internal override Type GetSurrogatedType(Type type)
    {
        if (_serializationSurrogateProvider == null)
        {
            return base.GetSurrogatedType(type);
        }
        else
        {
            type = DataContract.UnwrapNullableType(type);
            Type surrogateType = DataContractSerializer.GetSurrogatedType(_serializationSurrogateProvider, type);
            if (this.IsGetOnlyCollection && surrogateType != type)
            {
                throw new InvalidDataContractException(SR.Format(SR.SurrogatesWithGetOnlyCollectionsNotSupportedSerDeser,
                    DataContract.GetClrTypeFullName(type)));
            }
            else
            {
                return surrogateType;
            }
        }
    }

    private void InternalSerializeWithSurrogate(XmlWriterDelegator xmlWriter, object obj, bool isDeclaredType, bool writeXsiType, RuntimeTypeHandle declaredTypeHandle)
    {
        RuntimeTypeHandle objTypeHandle = isDeclaredType ? declaredTypeHandle : obj.GetType().TypeHandle;
        object oldObj = obj;
        int objOldId = 0;
        Type objType = Type.GetTypeFromHandle(objTypeHandle)!;
        Type declaredType = GetSurrogatedType(Type.GetTypeFromHandle(declaredTypeHandle)!);

        declaredTypeHandle = declaredType.TypeHandle;

        obj = DataContractSerializer.SurrogateToDataContractType(_serializationSurrogateProvider!, obj, declaredType, ref objType);
        objTypeHandle = objType.TypeHandle;
        if (oldObj != obj)
            objOldId = SerializedObjects.ReassignId(0, oldObj, obj);

        if (writeXsiType)
        {
            declaredType = Globals.TypeOfObject;
            SerializeWithXsiType(xmlWriter, obj, objTypeHandle, objType, -1, declaredType.TypeHandle, declaredType);
        }
        else if (declaredTypeHandle.Equals(objTypeHandle))
        {
            DataContract contract = GetDataContract(objTypeHandle, objType);
            SerializeWithoutXsiType(contract, xmlWriter, obj, declaredTypeHandle);
        }
        else
        {
            SerializeWithXsiType(xmlWriter, obj, objTypeHandle, objType, -1, declaredTypeHandle, declaredType);
        }

        if (oldObj != obj)
            SerializedObjects.ReassignId(objOldId, obj, oldObj);
    }

    internal override void WriteArraySize(XmlWriterDelegator xmlWriter, int size)
    {
        if (preserveObjectReferences && size > -1)
            xmlWriter.WriteAttributeInt(Globals.SerPrefix, DictionaryGlobals.ArraySizeLocalName, DictionaryGlobals.SerializationNamespace, size);
    }
}

internal class XmlObjectSerializerWriteContext : XmlObjectSerializerContext
{
    private ObjectReferenceStack _byValObjectsInScope;
    private XmlSerializableWriter? _xmlSerializableWriter;
    private const int depthToCheckCyclicReference = 512;
    private bool _isGetOnlyCollection;
    private readonly bool _unsafeTypeForwardingEnabled;
    protected bool serializeReadOnlyTypes;
    protected bool preserveObjectReferences;

    internal static XmlObjectSerializerWriteContext CreateContext(DataContractSerializer serializer, DataContract rootTypeDataContract, DataContractResolver? dataContractResolver)
    {
        return (serializer.PreserveObjectReferences || serializer.SerializationSurrogateProvider != null)
            ? new XmlObjectSerializerWriteContextComplex(serializer, rootTypeDataContract, dataContractResolver)
            : new XmlObjectSerializerWriteContext(serializer, rootTypeDataContract, dataContractResolver);
    }

    protected XmlObjectSerializerWriteContext(DataContractSerializer serializer, DataContract rootTypeDataContract, DataContractResolver? resolver)
        : base(serializer, rootTypeDataContract, resolver)
    {
        this.serializeReadOnlyTypes = serializer.SerializeReadOnlyTypes;
        // Known types restricts the set of types that can be deserialized
        _unsafeTypeForwardingEnabled = true;
    }

    internal XmlObjectSerializerWriteContext(XmlObjectSerializer serializer, int maxItemsInObjectGraph, StreamingContext streamingContext, bool ignoreExtensionDataObject)
        : base(serializer, maxItemsInObjectGraph, streamingContext, ignoreExtensionDataObject)
    {
        // Known types restricts the set of types that can be deserialized
        _unsafeTypeForwardingEnabled = true;
    }

    protected ObjectToIdCache SerializedObjects => field ??= new ObjectToIdCache();

    internal override bool IsGetOnlyCollection
    {
        get { return _isGetOnlyCollection; }
        set { _isGetOnlyCollection = value; }
    }

    internal bool SerializeReadOnlyTypes
    {
        get { return this.serializeReadOnlyTypes; }
    }

    internal bool UnsafeTypeForwardingEnabled
    {
        get { return _unsafeTypeForwardingEnabled; }
    }

    internal void StoreIsGetOnlyCollection()
    {
        _isGetOnlyCollection = true;
    }

    internal void ResetIsGetOnlyCollection()
    {
        _isGetOnlyCollection = false;
    }

    internal void InternalSerializeReference(XmlWriterDelegator xmlWriter, object obj, bool isDeclaredType, bool writeXsiType, int declaredTypeID, RuntimeTypeHandle declaredTypeHandle)
    {
        if (!OnHandleReference(xmlWriter, obj, true /*canContainCyclicReference*/))
            InternalSerialize(xmlWriter, obj, isDeclaredType, writeXsiType, declaredTypeID, declaredTypeHandle);
        OnEndHandleReference(xmlWriter, obj, true /*canContainCyclicReference*/);
    }

    internal virtual void InternalSerialize(XmlWriterDelegator xmlWriter, object obj, bool isDeclaredType, bool writeXsiType, int declaredTypeID, RuntimeTypeHandle declaredTypeHandle)
    {
        if (writeXsiType)
        {
            Type declaredType = Globals.TypeOfObject;
            SerializeWithXsiType(xmlWriter, obj, obj.GetType().TypeHandle, null /*type*/, -1, declaredType.TypeHandle, declaredType);
        }
        else if (isDeclaredType)
        {
            DataContract contract = GetDataContract(declaredTypeID, declaredTypeHandle);
            SerializeWithoutXsiType(contract, xmlWriter, obj, declaredTypeHandle);
        }
        else
        {
            RuntimeTypeHandle objTypeHandle = obj.GetType().TypeHandle;
            if (declaredTypeHandle.GetHashCode() == objTypeHandle.GetHashCode()) // semantically the same as Value == Value; Value is not available in SL
            {
                DataContract dataContract = (declaredTypeID >= 0)
                    ? GetDataContract(declaredTypeID, declaredTypeHandle)
                    : GetDataContract(declaredTypeHandle, null /*type*/);
                SerializeWithoutXsiType(dataContract, xmlWriter, obj, declaredTypeHandle);
            }
            else
            {
                SerializeWithXsiType(xmlWriter, obj, objTypeHandle, null /*type*/, declaredTypeID, declaredTypeHandle, Type.GetTypeFromHandle(declaredTypeHandle)!);
            }
        }
    }

    internal void SerializeWithoutXsiType(DataContract dataContract, XmlWriterDelegator xmlWriter, object obj, RuntimeTypeHandle declaredTypeHandle)
    {
        if (OnHandleIsReference(xmlWriter, dataContract, obj))
            return;

        if (dataContract.KnownDataContracts?.Count > 0)
        {
            scopedKnownTypes.Push(dataContract.KnownDataContracts);
            WriteDataContractValue(dataContract, xmlWriter, obj, declaredTypeHandle);
            scopedKnownTypes.Pop();
        }
        else
        {
            WriteDataContractValue(dataContract, xmlWriter, obj, declaredTypeHandle);
        }
    }

    internal virtual void SerializeWithXsiTypeAtTopLevel(DataContract dataContract, XmlWriterDelegator xmlWriter, object obj, RuntimeTypeHandle originalDeclaredTypeHandle, Type graphType)
    {
        Debug.Assert(rootTypeDataContract != null);

        bool verifyKnownType = false;
        Type declaredType = rootTypeDataContract.OriginalUnderlyingType;

        if (declaredType.IsInterface && CollectionDataContract.IsCollectionInterface(declaredType))
        {
            if (DataContractResolver != null)
            {
                WriteResolvedTypeInfo(xmlWriter, graphType, declaredType);
            }
        }
        else if (!declaredType.IsArray) //Array covariance is not supported in XSD. If declared type is array do not write xsi:type. Instead write xsi:type for each item
        {
            verifyKnownType = WriteTypeInfo(xmlWriter, dataContract, rootTypeDataContract);
        }

        SerializeAndVerifyType(dataContract, xmlWriter, obj, verifyKnownType, originalDeclaredTypeHandle, declaredType);
    }

    protected virtual void SerializeWithXsiType(XmlWriterDelegator xmlWriter, object obj, RuntimeTypeHandle objectTypeHandle, Type? objectType, int declaredTypeID, RuntimeTypeHandle declaredTypeHandle, Type declaredType)
    {
        bool verifyKnownType = false;
        DataContract dataContract;
        if (declaredType.IsInterface && CollectionDataContract.IsCollectionInterface(declaredType))
        {
            dataContract = GetDataContractSkipValidation(DataContract.GetId(objectTypeHandle), objectTypeHandle, objectType);
            if (OnHandleIsReference(xmlWriter, dataContract, obj))
                return;

            dataContract = GetDataContract(declaredTypeHandle, declaredType);
            if (!WriteClrTypeInfo(xmlWriter, dataContract) && DataContractResolver != null)
            {
                objectType ??= Type.GetTypeFromHandle(objectTypeHandle)!;
                WriteResolvedTypeInfo(xmlWriter, objectType, declaredType);
            }
        }
        else if (declaredType.IsArray) //Array covariance is not supported in XSD. If declared type is array do not write xsi:type. Instead write xsi:type for each item
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
                DataContract declaredTypeContract = (declaredTypeID >= 0)
                    ? GetDataContract(declaredTypeID, declaredTypeHandle)
                    : GetDataContract(declaredTypeHandle, declaredType);
                verifyKnownType = WriteTypeInfo(xmlWriter, dataContract, declaredTypeContract);
            }
        }

        SerializeAndVerifyType(dataContract, xmlWriter, obj, verifyKnownType, declaredTypeHandle, declaredType);
    }

    internal bool OnHandleIsReference(XmlWriterDelegator xmlWriter, DataContract contract, object obj)
    {
        if (preserveObjectReferences || !contract.IsReference || _isGetOnlyCollection)
        {
            return false;
        }

        bool isNew = true;
        int objectId = SerializedObjects.GetId(obj, ref isNew);
        _byValObjectsInScope.EnsureSetAsIsReference(obj);
        if (isNew)
        {
            xmlWriter.WriteAttributeString(Globals.SerPrefix, DictionaryGlobals.IdLocalName,
                DictionaryGlobals.SerializationNamespace, string.Create(CultureInfo.InvariantCulture, $"i{objectId}"));
            return false;
        }
        else
        {
            xmlWriter.WriteAttributeString(Globals.SerPrefix, DictionaryGlobals.RefLocalName, DictionaryGlobals.SerializationNamespace, string.Create(CultureInfo.InvariantCulture, $"i{objectId}"));
            return true;
        }
    }

    protected void SerializeAndVerifyType(DataContract dataContract, XmlWriterDelegator xmlWriter, object obj, bool verifyKnownType, RuntimeTypeHandle declaredTypeHandle, Type declaredType)
    {
        bool knownTypesAddedInCurrentScope = false;
        if (dataContract.KnownDataContracts?.Count > 0)
        {
            scopedKnownTypes.Push(dataContract.KnownDataContracts);
            knownTypesAddedInCurrentScope = true;
        }

        if (verifyKnownType)
        {
            if (!IsKnownType(dataContract, declaredType))
            {
                DataContract? knownContract = ResolveDataContractFromKnownTypes(dataContract.XmlName.Name, dataContract.XmlName.Namespace, null /*memberTypeContract*/, declaredType);
                if (knownContract == null || knownContract.UnderlyingType != dataContract.UnderlyingType)
                {
                    throw XmlObjectSerializer.CreateSerializationException(SR.Format(SR.DcTypeNotFoundOnSerialize, DataContract.GetClrTypeFullName(dataContract.UnderlyingType), dataContract.XmlName.Name, dataContract.XmlName.Namespace));
                }
            }
        }

        WriteDataContractValue(dataContract, xmlWriter, obj, declaredTypeHandle);

        if (knownTypesAddedInCurrentScope)
        {
            scopedKnownTypes.Pop();
        }
    }

    internal virtual bool WriteClrTypeInfo(XmlWriterDelegator xmlWriter, DataContract dataContract)
    {
        return false;
    }

    internal virtual bool WriteClrTypeInfo(XmlWriterDelegator xmlWriter, string clrTypeName, string clrAssemblyName)
    {
        return false;
    }

    internal virtual bool WriteClrTypeInfo(XmlWriterDelegator xmlWriter, Type dataContractType, string? clrTypeName, string? clrAssemblyName)
    {
        return false;
    }

    internal virtual bool WriteClrTypeInfo(XmlWriterDelegator xmlWriter, Type dataContractType, SerializationInfo serInfo)
    {
        return false;
    }

    internal virtual void WriteAnyType(XmlWriterDelegator xmlWriter, object value)
    {
        xmlWriter.WriteAnyType(value);
    }

    internal virtual void WriteString(XmlWriterDelegator xmlWriter, string value)
    {
        xmlWriter.WriteString(value);
    }

    internal virtual void WriteString(XmlWriterDelegator xmlWriter, string? value, XmlDictionaryString name, XmlDictionaryString? ns)
    {
        if (value == null)
            WriteNull(xmlWriter, typeof(string), true /*isMemberTypeSerializable*/, name, ns);
        else
        {
            xmlWriter.WriteStartElementPrimitive(name, ns);
            xmlWriter.WriteString(value);
            xmlWriter.WriteEndElementPrimitive();
        }
    }

    internal virtual void WriteBase64(XmlWriterDelegator xmlWriter, byte[] value)
    {
        xmlWriter.WriteBase64(value);
    }

    internal virtual void WriteBase64(XmlWriterDelegator xmlWriter, byte[] value, XmlDictionaryString name, XmlDictionaryString ns)
    {
        if (value == null)
            WriteNull(xmlWriter, typeof(byte[]), true /*isMemberTypeSerializable*/, name, ns);
        else
        {
            xmlWriter.WriteStartElementPrimitive(name, ns);
            xmlWriter.WriteBase64(value);
            xmlWriter.WriteEndElementPrimitive();
        }
    }

    internal virtual void WriteUri(XmlWriterDelegator xmlWriter, Uri value)
    {
        xmlWriter.WriteUri(value);
    }

    internal virtual void WriteUri(XmlWriterDelegator xmlWriter, Uri value, XmlDictionaryString name, XmlDictionaryString ns)
    {
        if (value == null)
            WriteNull(xmlWriter, typeof(Uri), true /*isMemberTypeSerializable*/, name, ns);
        else
        {
            xmlWriter.WriteStartElementPrimitive(name, ns);
            xmlWriter.WriteUri(value);
            xmlWriter.WriteEndElementPrimitive();
        }
    }

    internal virtual void WriteQName(XmlWriterDelegator xmlWriter, XmlQualifiedName value)
    {
        xmlWriter.WriteQName(value);
    }

    internal virtual void WriteQName(XmlWriterDelegator xmlWriter, XmlQualifiedName? value, XmlDictionaryString name, XmlDictionaryString? ns)
    {
        if (value == null)
            WriteNull(xmlWriter, typeof(XmlQualifiedName), true /*isMemberTypeSerializable*/, name, ns);
        else
        {
            if (ns != null && ns.Value != null && ns.Value.Length > 0)
                xmlWriter.WriteStartElement(Globals.ElementPrefix, name, ns);
            else
                xmlWriter.WriteStartElement(name, ns);
            xmlWriter.WriteQName(value);
            xmlWriter.WriteEndElement();
        }
    }

    internal void HandleGraphAtTopLevel(XmlWriterDelegator writer, object obj, DataContract contract)
    {
        writer.WriteXmlnsAttribute(Globals.XsiPrefix, DictionaryGlobals.SchemaInstanceNamespace);
        if (contract.IsISerializable)
        {
            writer.WriteXmlnsAttribute(Globals.XsdPrefix, DictionaryGlobals.SchemaNamespace);
        }

        OnHandleReference(writer, obj, true /*canContainReferences*/);
    }

    internal virtual bool OnHandleReference(XmlWriterDelegator xmlWriter, object obj, bool canContainCyclicReference)
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

    internal virtual void OnEndHandleReference(XmlWriterDelegator xmlWriter, object obj, bool canContainCyclicReference)
    {
        if (xmlWriter.depth < depthToCheckCyclicReference)
            return;

        if (canContainCyclicReference)
        {
            _byValObjectsInScope.Pop(obj);
        }
    }

    internal void WriteNull(XmlWriterDelegator xmlWriter, Type memberType, bool isMemberTypeSerializable)
    {
        CheckIfTypeSerializable(memberType, isMemberTypeSerializable);
        WriteNull(xmlWriter);
    }

    internal void WriteNull(XmlWriterDelegator xmlWriter, Type memberType, bool isMemberTypeSerializable, XmlDictionaryString name, XmlDictionaryString? ns)
    {
        xmlWriter.WriteStartElement(name, ns);
        WriteNull(xmlWriter, memberType, isMemberTypeSerializable);
        xmlWriter.WriteEndElement();
    }

    internal void IncrementArrayCount(XmlWriterDelegator xmlWriter, Array array)
    {
        IncrementCollectionCount(xmlWriter, array.GetLength(0));
    }

    internal void IncrementCollectionCount(XmlWriterDelegator xmlWriter, ICollection collection)
    {
        IncrementCollectionCount(xmlWriter, collection.Count);
    }

    internal void IncrementCollectionCountGeneric<T>(XmlWriterDelegator xmlWriter, ICollection<T> collection)
    {
        IncrementCollectionCount(xmlWriter, collection.Count);
    }

    private void IncrementCollectionCount(XmlWriterDelegator xmlWriter, int size)
    {
        IncrementItemCount(size);
        WriteArraySize(xmlWriter, size);
    }

    internal virtual void WriteArraySize(XmlWriterDelegator xmlWriter, int size)
    {
    }

    internal static bool IsMemberTypeSameAsMemberValue(object obj, Type memberType)
    {
        if (obj == null || memberType == null)
            return false;

        return obj.GetType().TypeHandle.Equals(memberType.TypeHandle);
    }

    internal static T GetDefaultValue<T>()
    {
        return default(T)!;
    }

    internal static T GetNullableValue<T>(Nullable<T> value) where T : struct
    {
        // value.Value will throw if hasValue is false
        return value!.Value;
    }

    internal static void ThrowRequiredMemberMustBeEmitted(string memberName, Type type)
    {
        throw new SerializationException(SR.Format(SR.RequiredMemberMustBeEmitted, memberName, type.FullName));
    }

    internal static bool GetHasValue<T>(Nullable<T> value) where T : struct
    {
        return value.HasValue;
    }

    internal void WriteIXmlSerializable(XmlWriterDelegator xmlWriter, object obj)
    {
        _xmlSerializableWriter ??= new XmlSerializableWriter();
        WriteIXmlSerializable(xmlWriter, obj, _xmlSerializableWriter);
    }

    internal static void WriteRootIXmlSerializable(XmlWriterDelegator xmlWriter, object obj)
    {
        WriteIXmlSerializable(xmlWriter, obj, new XmlSerializableWriter());
    }

    private static void WriteIXmlSerializable(XmlWriterDelegator xmlWriter, object obj, XmlSerializableWriter xmlSerializableWriter)
    {
        xmlSerializableWriter.BeginWrite(xmlWriter.Writer, obj);
        IXmlSerializable? xmlSerializable = obj as IXmlSerializable;
        if (xmlSerializable != null)
            xmlSerializable.WriteXml(xmlSerializableWriter);
        else
        {
            XmlElement? xmlElement = obj as XmlElement;
            if (xmlElement != null)
                xmlElement.WriteTo(xmlSerializableWriter);
            else
            {
                XmlNode[]? xmlNodes = obj as XmlNode[];
                if (xmlNodes != null)
                    foreach (XmlNode xmlNode in xmlNodes)
                        xmlNode.WriteTo(xmlSerializableWriter);
                else
                    throw XmlObjectSerializer.CreateSerializationException(SR.Format(SR.UnknownXmlType, DataContract.GetClrTypeFullName(obj.GetType())));
            }
        }

        xmlSerializableWriter.EndWrite();
    }

    internal static void GetObjectData(ISerializable obj, SerializationInfo serInfo, StreamingContext context)
    {
#pragma warning disable SYSLIB0050 // ISerializable.GetObjectData is obsolete
        obj.GetObjectData(serInfo, context);
#pragma warning restore SYSLIB0050
    }

    public void WriteISerializable(XmlWriterDelegator xmlWriter, ISerializable obj)
    {
        Type objType = obj.GetType();
#pragma warning disable SYSLIB0050 // SerializationInfo ctor is obsolete
        var serInfo = new SerializationInfo(objType, XmlObjectSerializer.FormatterConverter /*!UnsafeTypeForwardingEnabled is always false*/);
#pragma warning restore SYSLIB0050
        GetObjectData(obj, serInfo, GetStreamingContext());

        // (!UnsafeTypeForwardingEnabled) is always false
        //if (!UnsafeTypeForwardingEnabled && serInfo.AssemblyName == Globals.MscorlibAssemblyName)
        //{
        //    // Throw if a malicious type tries to set its assembly name to "0" to get deserialized in mscorlib
        //    throw XmlObjectSerializer.CreateSerializationException(SR.Format(SR.ISerializableAssemblyNameSetToZero, DataContract.GetClrTypeFullName(obj.GetType())));
        //}

        WriteSerializationInfo(xmlWriter, objType, serInfo);
    }

    internal void WriteSerializationInfo(XmlWriterDelegator xmlWriter, Type objType, SerializationInfo serInfo)
    {
        if (DataContract.GetClrTypeFullName(objType) != serInfo.FullTypeName)
        {
            if (DataContractResolver != null)
            {
                XmlDictionaryString? typeName, typeNs;
                if (ResolveType(serInfo.ObjectType, objType, out typeName, out typeNs))
                {
                    xmlWriter.WriteAttributeQualifiedName(Globals.SerPrefix, DictionaryGlobals.ISerializableFactoryTypeLocalName, DictionaryGlobals.SerializationNamespace, typeName, typeNs);
                }
            }
            else
            {
                string typeName, typeNs;
                DataContract.GetDefaultXmlName(serInfo.FullTypeName, out typeName, out typeNs);
                xmlWriter.WriteAttributeQualifiedName(Globals.SerPrefix, DictionaryGlobals.ISerializableFactoryTypeLocalName, DictionaryGlobals.SerializationNamespace, DataContract.GetClrTypeString(typeName), DataContract.GetClrTypeString(typeNs));
            }
        }

        WriteClrTypeInfo(xmlWriter, objType, serInfo);
        IncrementItemCount(serInfo.MemberCount);
        foreach (SerializationEntry serEntry in serInfo)
        {
            XmlDictionaryString name = DataContract.GetClrTypeString(DataContract.EncodeLocalName(serEntry.Name));
            xmlWriter.WriteStartElement(name, DictionaryGlobals.EmptyString);
            object? obj = serEntry.Value;
            if (obj == null)
            {
                WriteNull(xmlWriter);
            }
            else
            {
                InternalSerializeReference(xmlWriter, obj, false /*isDeclaredType*/, false /*writeXsiType*/, -1, Globals.TypeOfObject.TypeHandle);
            }

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
        XmlDictionaryString? typeName, typeNamespace;
        if (ResolveType(objectType, declaredType, out typeName, out typeNamespace))
        {
            WriteTypeInfo(writer, typeName, typeNamespace);
        }
    }

    private bool ResolveType(Type objectType, Type declaredType, [NotNullWhen(true)] out XmlDictionaryString? typeName, [NotNullWhen(true)] out XmlDictionaryString? typeNamespace)
    {
        Debug.Assert(DataContractResolver != null);

        if (!DataContractResolver.TryResolveType(objectType, declaredType, KnownTypeResolver, out typeName, out typeNamespace))
        {
            throw XmlObjectSerializer.CreateSerializationException(SR.Format(SR.ResolveTypeReturnedFalse, DataContract.GetClrTypeFullName(DataContractResolver.GetType()), DataContract.GetClrTypeFullName(objectType)));
        }

        if (typeName == null)
        {
            if (typeNamespace == null)
            {
                return false;
            }
            else
            {
                throw XmlObjectSerializer.CreateSerializationException(SR.Format(SR.ResolveTypeReturnedNull, DataContract.GetClrTypeFullName(DataContractResolver.GetType()), DataContract.GetClrTypeFullName(objectType)));
            }
        }

        if (typeNamespace == null)
        {
            throw XmlObjectSerializer.CreateSerializationException(SR.Format(SR.ResolveTypeReturnedNull, DataContract.GetClrTypeFullName(DataContractResolver.GetType()), DataContract.GetClrTypeFullName(objectType)));
        }

        return true;
    }

    protected virtual bool WriteTypeInfo(XmlWriterDelegator writer, DataContract contract, DataContract declaredContract)
    {
        if (!XmlObjectSerializer.IsContractDeclared(contract, declaredContract))
        {
            if (DataContractResolver == null)
            {
                WriteTypeInfo(writer, contract.Name, contract.Namespace);
                return true;
            }
            else
            {
                WriteResolvedTypeInfo(writer, contract.OriginalUnderlyingType, declaredContract.OriginalUnderlyingType);
                return false;
            }
        }

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

        IList<ExtensionDataMember>? members = extensionData.Members;
        if (members != null)
        {
            for (int i = 0; i < members.Count; i++)
            {
                ExtensionDataMember member = members[i];
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
        IDataNode? dataNode = member.Value;
        WriteExtensionDataValue(xmlWriter, dataNode);
        xmlWriter.WriteEndElement();
    }

    internal virtual void WriteExtensionDataTypeInfo(XmlWriterDelegator xmlWriter, IDataNode dataNode)
    {
        if (dataNode.DataContractName != null)
            WriteTypeInfo(xmlWriter, dataNode.DataContractName, dataNode.DataContractNamespace);

        WriteClrTypeInfo(xmlWriter, dataNode.DataType, dataNode.ClrTypeName, dataNode.ClrAssemblyName);
    }

    internal void WriteExtensionDataValue(XmlWriterDelegator xmlWriter, IDataNode? dataNode)
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

        Type dataType = dataNode.DataType;
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
                object? o = dataNode.Value;
                if (o != null)
                    InternalSerialize(xmlWriter, o, false /*isDeclaredType*/, false /*writeXsiType*/, -1, o.GetType().TypeHandle);
            }
            else
                xmlWriter.WriteExtensionData(dataNode);
        }

        if (dataNode.PreservesReferences)
            OnEndHandleReference(xmlWriter, (dataNode.Value ?? dataNode), true /*canContainCyclicReference*/);
    }

    internal bool TryWriteDeserializedExtensionData(XmlWriterDelegator xmlWriter, IDataNode dataNode)
    {
        object? o = dataNode.Value;
        if (o == null)
            return false;

        Type declaredType = (dataNode.DataContractName == null) ? o.GetType() : Globals.TypeOfObject;
        InternalSerialize(xmlWriter, o, false /*isDeclaredType*/, false /*writeXsiType*/, -1, declaredType.TypeHandle);
        return true;
    }

    private void WriteExtensionClassData(XmlWriterDelegator xmlWriter, ClassDataNode dataNode)
    {
        if (!TryWriteDeserializedExtensionData(xmlWriter, dataNode))
        {
            WriteExtensionDataTypeInfo(xmlWriter, dataNode);

            IList<ExtensionDataMember>? members = dataNode.Members;
            if (members != null)
            {
                for (int i = 0; i < members.Count; i++)
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

            IList<IDataNode?>? items = dataNode.Items;
            if (items != null)
            {
                for (int i = 0; i < items.Count; i++)
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

            IList<ISerializableDataMember>? members = dataNode.Members;
            if (members != null)
            {
                for (int i = 0; i < members.Count; i++)
                {
                    ISerializableDataMember member = members[i];
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
            IList<XmlAttribute>? xmlAttributes = dataNode.XmlAttributes;
            if (xmlAttributes != null)
            {
                foreach (XmlAttribute attribute in xmlAttributes)
                    attribute.WriteTo(xmlWriter.Writer);
            }

            WriteExtensionDataTypeInfo(xmlWriter, dataNode);

            IList<XmlNode>? xmlChildNodes = dataNode.XmlChildNodes;
            if (xmlChildNodes != null)
            {
                foreach (XmlNode node in xmlChildNodes)
                    node.WriteTo(xmlWriter.Writer);
            }
        }
    }
}