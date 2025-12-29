// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license

using System.Runtime.CompilerServices;
using System.Xml;

namespace Marius.DataContracts.Runtime;

public class PrimitiveDataContract : DataContract
{
    public Type? InterfaceType { get; init; }

    public override void WriteRootElement(XmlWriterDelegator writer, XmlDictionaryString name, XmlDictionaryString? ns)
    {
        if (ReferenceEquals(ns, DictionaryGlobals.SerializationNamespace))
            writer.WriteStartElement(Globals.SerPrefix, name, ns);
        else if (!string.IsNullOrEmpty(ns?.Value))
            writer.WriteStartElement(Globals.ElementPrefix, name, ns);
        else
            writer.WriteStartElement(name, ns);
    }

    public override void WriteXmlValue(XmlWriterDelegator xmlWriter, object obj, XmlObjectSerializerWriteContext? context)
    {
        xmlWriter.WriteAnyType(obj);
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T HandleReadValue<T>(T value, XmlObjectSerializerReadContext context)
    {
        context.AddNewObject(value);
        return value;
    }
}

public class PrimitiveDataContract<T> : PrimitiveDataContract
{
    public required Func<XmlReaderDelegator, XmlObjectSerializerReadContext?, T> Read { get; init; }
    public required Action<XmlWriterDelegator, XmlObjectSerializerWriteContext?, T> Write { get; init; }

    public override object? ReadXmlValue(XmlReaderDelegator xmlReader, XmlObjectSerializerReadContext? context)
    {
        return Read(xmlReader, context);
    }

    public override void WriteXmlValue(XmlWriterDelegator xmlWriter, object obj, XmlObjectSerializerWriteContext? context)
    {
        Write(xmlWriter, context, (T)obj);
    }
}