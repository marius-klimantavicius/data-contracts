// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Runtime.Serialization;

namespace Marius.DataContracts.Runtime;

public class XmlObjectSerializerReadContextComplex : XmlObjectSerializerReadContext
{
    private readonly bool _preserveObjectReferences;
    private readonly SerializationSurrogate? _surrogate;

    public XmlObjectSerializerReadContextComplex(DataContractSerializer serializer, DataContract rootTypeDataContract)
        : base(serializer, rootTypeDataContract)
    {
        _preserveObjectReferences = serializer.PreserveObjectReferences;
        _surrogate = serializer.CreateSurrogate();
    }

    public XmlObjectSerializerReadContextComplex(XmlObjectSerializer serializer, int maxItemsInObjectGraph, StreamingContext streamingContext, bool ignoreExtensionDataObject)
        : base(serializer, maxItemsInObjectGraph, streamingContext, ignoreExtensionDataObject)
    {
    }

    public override int GetArraySize()
    {
        Debug.Assert(attributes != null);

        return _preserveObjectReferences ? attributes.ArraySZSize : -1;
    }

    public override Type GetSurrogatedType(Type type)
    {
        if (_surrogate == null)
            return base.GetSurrogatedType(type);

        type = Nullable.GetUnderlyingType(type) ?? type;
        var replacement = _surrogate.GetSurrogatedType(type);
        if (IsGetOnlyCollection && replacement != type)
            throw new InvalidDataContractException(SR.Format(SR.SurrogatesWithGetOnlyCollectionsNotSupportedSerDeser, DataContract.GetClrTypeFullName(type)));

        return replacement;
    }

    public override void CheckIfTypeSerializable(Type memberType, bool isMemberTypeSerializable)
    {
        if (_surrogate == null)
        {
            base.CheckIfTypeSerializable(memberType, isMemberTypeSerializable);
            return;
        }

        while (memberType.IsArray)
            memberType = memberType.GetElementType()!;
        _surrogate.GetSurrogatedType(memberType);
    }

    public override object? InternalDeserialize(XmlReaderDelegator reader, Type declaredType, string? name, string? ns) =>
        InternalDeserialize(reader, declaredType, null, name, ns);

    public override T? Read<T>(XmlReaderDelegator reader, string name, string ns, DataContract? contract) where T : default =>
        _surrogate == null
            ? base.Read<T>(reader, name, ns, contract)
            : (T?)InternalDeserialize(reader, typeof(T), null, name, ns);

    internal T? ReadWithParsedAttributesCore<T>(XmlReaderDelegator reader, string name, string ns, DataContract? contract)
        => _surrogate == null
            ? ReadCore<T>(reader, name, ns, contract, attributesRead: true)
            : (T?)InternalDeserializeWithSurrogate(reader, typeof(T), null, name, ns, attributesRead: true);

    public override object? InternalDeserialize(XmlReaderDelegator reader, Type declaredType, DataContract? contract, string? name, string? ns)
    {
        if (_surrogate == null)
            return base.InternalDeserialize(reader, declaredType, contract, name, ns);

        return InternalDeserializeWithSurrogate(reader, declaredType, contract, name, ns, attributesRead: false);
    }

    private object? InternalDeserializeWithSurrogate(XmlReaderDelegator reader, Type declaredType, DataContract? contract, string? name, string? ns, bool attributesRead)
    {
        Debug.Assert(_surrogate != null);

        // A supplied root contract is already surrogated. Read<T> discards static
        // member contracts so their provider decisions are evaluated at runtime.
        contract ??= GetSurrogatedContract(declaredType, attributesRead);

        if (IsGetOnlyCollection && contract.UnderlyingType != declaredType)
            throw new InvalidDataContractException(SR.Format(SR.SurrogatesWithGetOnlyCollectionsNotSupportedSerDeser, DataContract.GetClrTypeFullName(declaredType)));

        if (!attributesRead)
            ReadAttributes(reader);

        var objectId = GetObjectId();
        var oldObject = GetType() == typeof(XmlObjectSerializerReadContextComplex)
            ? InternalDeserializeWithParsedAttributes(reader, name, ns, declaredType, ref contract)
            : InternalDeserialize(reader, name, ns, declaredType, ref contract);
        var replacement = _surrogate.GetDeserializedObject(oldObject, contract, declaredType);
        ReplaceDeserializedObject(objectId, oldObject, replacement);
        return replacement;
    }

    private DataContract GetSurrogatedContract(Type declaredType, bool attributesRead)
    {
        Debug.Assert(_surrogate != null);

        if (!attributesRead)
            return _surrogate.GetGeneratedContract(_surrogate.GetSurrogatedType(declaredType));

        // Both the surrogate callback and every generated-contract lookup can
        // reenter this context on a different reader. Preserve the handoff across
        // the whole selection, before normal null/ref/type/reader policy resumes.
        var parsedAttributes = attributes!;
        var savedAttributes = new Attributes.Snapshot(parsedAttributes);
        try
        {
            return _surrogate.GetGeneratedContract(_surrogate.GetSurrogatedType(declaredType));
        }
        finally
        {
            savedAttributes.Restore(parsedAttributes);
            attributes = parsedAttributes;
        }
    }
}