// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license

using System.Diagnostics;
using System.Xml;

namespace Marius.DataContracts.Runtime;

public class CollectionDataContract : DataContract
{
    private static Type[]? _knownInterfaces;
    private static Type[] KnownInterfaces =>
        // Listed in priority order
        _knownInterfaces ??= new Type[]
        {
            Globals.TypeOfIDictionaryGeneric,
            Globals.TypeOfIDictionary,
            Globals.TypeOfIListGeneric,
            Globals.TypeOfICollectionGeneric,
            Globals.TypeOfIList,
            Globals.TypeOfIEnumerableGeneric,
            Globals.TypeOfICollection,
            Globals.TypeOfIEnumerable,
        };
    
    public required Type ItemType { get; init; }
    public required XmlDictionaryString CollectionItemName { get; init; }
    public required XmlDictionaryString? ChildElementNamespace { get; init; }

    public DataContract ItemContract { get; set; } = null!;

    internal static bool IsCollectionInterface(Type type)
    {
        if (type.IsGenericType)
            type = type.GetGenericTypeDefinition();
        return ((IList<Type>)KnownInterfaces).Contains(type);
    }
}

public class CollectionDataContract<T> : CollectionDataContract
{
    public required Func<XmlReaderDelegator, XmlObjectSerializerReadContext?, XmlDictionaryString, XmlDictionaryString, T> Read { get; init; }
    public required Action<XmlReaderDelegator, XmlObjectSerializerReadContext?, XmlDictionaryString, XmlDictionaryString> ReadGetOnly { get; init; }
    public required Action<XmlWriterDelegator, XmlObjectSerializerWriteContext, T> Write { get; init; }

    public override object? ReadXmlValue(XmlReaderDelegator xmlReader, XmlObjectSerializerReadContext? context)
    {
        Debug.Assert(context != null);
 
        xmlReader.Read();
        object? o = null;
        if (context.IsGetOnlyCollection)
        {
            context.IsGetOnlyCollection = false;
            ReadGetOnly(xmlReader, context, CollectionItemName, Namespace);
        }
        else
        {
            o = Read(xmlReader, context, CollectionItemName, Namespace);
        }
        xmlReader.ReadEndElement();
        return o;
    }

    public override void WriteXmlValue(XmlWriterDelegator xmlWriter, object obj, XmlObjectSerializerWriteContext? context)
    {
        Debug.Assert(context != null);
 
        // IsGetOnlyCollection value has already been used to create current collectiondatacontract, value can now be reset.
        context.IsGetOnlyCollection = false;
        Write(xmlWriter, context, (T)obj);
    }
}