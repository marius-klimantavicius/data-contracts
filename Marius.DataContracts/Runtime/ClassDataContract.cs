// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license

using System.Collections.Immutable;
using System.Diagnostics;
using System.Xml;

namespace Marius.DataContracts.Runtime;

public class ClassDataContract : DataContract
{
    public required ImmutableArray<XmlDictionaryString> MemberNames { get; init; }
    public required ImmutableArray<XmlDictionaryString> MemberNamespaces { get; init; }
    public required ImmutableArray<XmlDictionaryString?> ChildElementNamespaces { get; init; }
    public required ImmutableArray<XmlDictionaryString?> ContractNamespaces { get; init; }

    public ClassDataContract? BaseClassContract { get; set; }
}

public class ClassDataContract<T> : ClassDataContract
{
    public required Func<XmlReaderDelegator, XmlObjectSerializerReadContext?, ImmutableArray<XmlDictionaryString>, ImmutableArray<XmlDictionaryString>, T> Read { get; init; }
    public required Action<XmlWriterDelegator, XmlObjectSerializerWriteContext, T> Write { get; init; }

    public override object? ReadXmlValue(XmlReaderDelegator xmlReader, XmlObjectSerializerReadContext? context)
    {
        xmlReader.Read();
        object? o = Read(xmlReader, context, MemberNames, MemberNamespaces);
        xmlReader.ReadEndElement();
        return o;
    }

    public override void WriteXmlValue(XmlWriterDelegator xmlWriter, object obj, XmlObjectSerializerWriteContext? context)
    {
        Debug.Assert(context != null);
        Write(xmlWriter, context, (T)obj);
    }
}
