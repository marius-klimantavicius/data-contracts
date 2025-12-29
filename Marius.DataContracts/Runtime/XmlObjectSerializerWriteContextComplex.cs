// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.Serialization;
using System.Xml;

namespace Marius.DataContracts.Runtime;

public class XmlObjectSerializerWriteContextComplex : XmlObjectSerializerWriteContext
{
    public XmlObjectSerializerWriteContextComplex(DataContractSerializer serializer, DataContract rootTypeDataContract)
        : base(serializer, rootTypeDataContract)
    {
        _preserveObjectReferences = serializer.PreserveObjectReferences;
    }

    public XmlObjectSerializerWriteContextComplex(XmlObjectSerializer serializer, int maxItemsInObjectGraph, StreamingContext streamingContext, bool ignoreExtensionDataObject)
        : base(serializer, maxItemsInObjectGraph, streamingContext, ignoreExtensionDataObject)
    {
    }

    public override bool WriteClrTypeInfo(XmlWriterDelegator xmlWriter, DataContract dataContract)
    {
        return false;
    }

    public override bool WriteClrTypeInfo(XmlWriterDelegator xmlWriter, Type dataContractType, string? clrTypeName, string? clrAssemblyName)
    {
        return false;
    }

    public override void WriteAnyType(XmlWriterDelegator xmlWriter, object value)
    {
        if (!OnHandleReference(xmlWriter, value, false /*canContainCyclicReference*/))
            xmlWriter.WriteAnyType(value);
    }

    public override void WriteString(XmlWriterDelegator xmlWriter, string value)
    {
        if (!OnHandleReference(xmlWriter, value, false /*canContainCyclicReference*/))
            xmlWriter.WriteString(value);
    }

    public override void WriteString(XmlWriterDelegator xmlWriter, string? value, XmlDictionaryString name, XmlDictionaryString? ns)
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

    public override void WriteBase64(XmlWriterDelegator xmlWriter, byte[] value)
    {
        if (!OnHandleReference(xmlWriter, value, false /*canContainCyclicReference*/))
            xmlWriter.WriteBase64(value);
    }

    public override void WriteBase64(XmlWriterDelegator xmlWriter, byte[]? value, XmlDictionaryString name, XmlDictionaryString ns)
    {
        if (value == null)
        {
            WriteNull(xmlWriter, typeof(byte[]), true /*isMemberTypeSerializable*/, name, ns);
        }
        else
        {
            xmlWriter.WriteStartElementPrimitive(name, ns);
            if (!OnHandleReference(xmlWriter, value, false /*canContainCyclicReference*/))
                xmlWriter.WriteBase64(value);
            xmlWriter.WriteEndElementPrimitive();
        }
    }

    public override void WriteUri(XmlWriterDelegator xmlWriter, Uri value)
    {
        if (!OnHandleReference(xmlWriter, value, false /*canContainCyclicReference*/))
            xmlWriter.WriteUri(value);
    }

    public override void WriteUri(XmlWriterDelegator xmlWriter, Uri? value, XmlDictionaryString name, XmlDictionaryString ns)
    {
        if (value == null)
        {
            WriteNull(xmlWriter, typeof(Uri), true /*isMemberTypeSerializable*/, name, ns);
        }
        else
        {
            xmlWriter.WriteStartElementPrimitive(name, ns);
            if (!OnHandleReference(xmlWriter, value, false /*canContainCyclicReference*/))
                xmlWriter.WriteUri(value);
            xmlWriter.WriteEndElementPrimitive();
        }
    }

    public override void WriteQName(XmlWriterDelegator xmlWriter, XmlQualifiedName value)
    {
        if (!OnHandleReference(xmlWriter, value, false /*canContainCyclicReference*/))
            xmlWriter.WriteQName(value);
    }

    public override void WriteQName(XmlWriterDelegator xmlWriter, XmlQualifiedName? value, XmlDictionaryString name, XmlDictionaryString? ns)
    {
        if (value == null)
        {
            WriteNull(xmlWriter, typeof(XmlQualifiedName), true /*isMemberTypeSerializable*/, name, ns);
        }
        else
        {
            if (!string.IsNullOrEmpty(ns?.Value))
                xmlWriter.WriteStartElement(Globals.ElementPrefix, name, ns);
            else
                xmlWriter.WriteStartElement(name, ns);
            if (!OnHandleReference(xmlWriter, value, false /*canContainCyclicReference*/))
                xmlWriter.WriteQName(value);
            xmlWriter.WriteEndElement();
        }
    }

    public override bool OnHandleReference(XmlWriterDelegator xmlWriter, object obj, bool canContainCyclicReference)
    {
        if (_preserveObjectReferences && !IsGetOnlyCollection)
        {
            var isNew = true;
            var objectId = SerializedObjects.GetId(obj, ref isNew);
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

    public override void OnEndHandleReference(XmlWriterDelegator xmlWriter, object obj, bool canContainCyclicReference)
    {
        if (_preserveObjectReferences && !IsGetOnlyCollection)
            return;

        base.OnEndHandleReference(xmlWriter, obj, canContainCyclicReference);
    }

    public override void WriteArraySize(XmlWriterDelegator xmlWriter, int size)
    {
        if (_preserveObjectReferences && size > -1)
            xmlWriter.WriteAttributeInt(Globals.SerPrefix, DictionaryGlobals.ArraySizeLocalName, DictionaryGlobals.SerializationNamespace, size);
    }
}