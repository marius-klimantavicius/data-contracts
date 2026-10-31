// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license

using System.Collections.Immutable;
using System.Diagnostics;
using System.Xml;

namespace Marius.DataContracts.Runtime;

public readonly record struct ReadReplacement
{
    public bool HasReplacement { get; init; }
    public object? Value { get; init; }

    public ReadReplacement(bool hasReplacement, object? value)
    {
        HasReplacement = hasReplacement;
        Value = value;
    }
}

public delegate T ClassReader<out T>(XmlReaderDelegator xmlReader, XmlObjectSerializerReadContext? context, ImmutableArray<XmlDictionaryString> memberNames, ImmutableArray<XmlDictionaryString> memberNamespaces, out ReadReplacement replacement);

// Only adapters implement this interface: the wire reader and the converted
// value have separate type parameters, which may represent the same CLR type.
internal interface ITypedAdapterReader<out T>
{
    bool CanReadTyped { get; }
    T ReadXmlValueTyped(XmlReaderDelegator xmlReader, XmlObjectSerializerReadContext? context, bool attributesRead);
}

internal interface ITypedAdapterWriter<in T>
{
    bool CanWriteTyped { get; }
    void WriteXmlValueTyped(XmlWriterDelegator xmlWriter, T value, XmlObjectSerializerWriteContext context);
}

public class ClassDataContract : DataContract
{
    public required ImmutableArray<XmlDictionaryString> MemberNames { get; init; }
    public required ImmutableArray<XmlDictionaryString> MemberNamespaces { get; init; }
    public required ImmutableArray<XmlDictionaryString?> ChildElementNamespaces { get; init; }
    public required ImmutableArray<XmlDictionaryString?> ContractNamespaces { get; init; }

    public ClassDataContract? BaseClassContract { get; set; }

    // A constrained call preserves mutations on value types. Casting a struct to
    // IDeserializationCallback would invoke the callback on a boxed copy.
    public static void InvokeDeserializationCallback<TValue>(ref TValue value)
        where TValue : System.Runtime.Serialization.IDeserializationCallback
        => value.OnDeserialization(null);
}

public class ClassDataContract<T> : ClassDataContract
{
    public required ClassReader<T> Read { get; init; }
    public required Action<XmlWriterDelegator, XmlObjectSerializerWriteContext, T> Write { get; init; }

    protected T ReadXmlContents(XmlReaderDelegator xmlReader, XmlObjectSerializerReadContext? context, out ReadReplacement replacement)
        => ReadXmlContents(xmlReader, context, out replacement, attributesRead: false);

    private protected T ReadXmlContents(XmlReaderDelegator xmlReader, XmlObjectSerializerReadContext? context, out ReadReplacement replacement, bool attributesRead)
    {
        // Dictionary entry contracts can be read directly, without passing through
        // InternalDeserialize. Their attributes must not come from the parent.
        if (!attributesRead)
            context?.ReadAttributes(xmlReader);

        xmlReader.Read();
        return Read(xmlReader, context, MemberNames, MemberNamespaces, out replacement);
    }

    private T ReadXmlValueOriginal(XmlReaderDelegator xmlReader, XmlObjectSerializerReadContext? context, out ReadReplacement replacement, bool attributesRead = false)
    {
        var original = ReadXmlContents(xmlReader, context, out replacement, attributesRead);
        xmlReader.ReadEndElement();
        return original;
    }

    internal T ReadXmlValueTyped(XmlReaderDelegator xmlReader, XmlObjectSerializerReadContext? context, bool attributesRead = false)
    {
        var original = ReadXmlValueOriginal(xmlReader, context, out var replacement, attributesRead);
        return replacement.HasReplacement ? (T)replacement.Value! : original;
    }

    internal override object? ReadXmlValueWithParsedAttributes(XmlReaderDelegator xmlReader, XmlObjectSerializerReadContext context)
    {
        if (GetType() != typeof(ClassDataContract<T>))
            return ReadXmlValue(xmlReader, context);

        var original = ReadXmlValueOriginal(xmlReader, context, out var replacement, attributesRead: true);
        return replacement.HasReplacement ? replacement.Value : original;
    }

    // Entries use the contract wrapper directly, rather than context.Read<T>:
    // entry attributes, wrapper consumption and replacements still apply, but
    // the general context's null/ref/type and surrogate policy does not.
    public T ReadDictionaryEntry(XmlReaderDelegator xmlReader, XmlObjectSerializerReadContext? context)
    {
        // Publicly replaceable contract subclasses retain virtual object hooks.
        if (GetType() != typeof(ClassDataContract<T>))
            return (T)ReadXmlValue(xmlReader, context)!;

        return ReadXmlValueTyped(xmlReader, context);
    }

    public void WriteDictionaryEntry(XmlWriterDelegator xmlWriter, T value, XmlObjectSerializerWriteContext? context)
    {
        if (GetType() != typeof(ClassDataContract<T>))
        {
            WriteXmlValue(xmlWriter, value!, context);
            return;
        }

        Debug.Assert(context != null);
        Write(xmlWriter, context, value);
    }

    public override object? ReadXmlValue(XmlReaderDelegator xmlReader, XmlObjectSerializerReadContext? context)
    {
        var original = ReadXmlValueOriginal(xmlReader, context, out var replacement);
        return replacement.HasReplacement ? replacement.Value : original;
    }

    public override void WriteXmlValue(XmlWriterDelegator xmlWriter, object obj, XmlObjectSerializerWriteContext? context)
    {
        Debug.Assert(context != null);
        Write(xmlWriter, context, (T)obj);
    }
}

public class ClassDataContract<TWire, TValue> : ClassDataContract<TWire>, ITypedAdapterReader<TValue>, ITypedAdapterWriter<TValue>
{
    public required Func<TWire, TValue> ReadAdapter { get; init; }
    public required Func<TValue, TWire> WriteAdapter { get; init; }

    bool ITypedAdapterReader<TValue>.CanReadTyped => GetType() == typeof(ClassDataContract<TWire, TValue>);

    bool ITypedAdapterWriter<TValue>.CanWriteTyped => GetType() == typeof(ClassDataContract<TWire, TValue>);

    void ITypedAdapterWriter<TValue>.WriteXmlValueTyped(XmlWriterDelegator xmlWriter, TValue value, XmlObjectSerializerWriteContext context)
    {
        // Even identical wire/value types must pass through the adapter once.
        Write(xmlWriter, context, WriteAdapter(value));
    }

    private TValue ReadAdaptedXmlValue(XmlReaderDelegator xmlReader, XmlObjectSerializerReadContext? context, out ReadReplacement replacement, bool attributesRead = false)
    {
        var wire = ReadXmlContents(xmlReader, context, out replacement, attributesRead);

        // Conversion errors must leave the reader on the wire end element,
        // matching conversion inside the generated reader before this boundary.
        var value = replacement.HasReplacement ? default! : ReadAdapter(wire);
        xmlReader.ReadEndElement();
        return value;
    }

    TValue ITypedAdapterReader<TValue>.ReadXmlValueTyped(XmlReaderDelegator xmlReader, XmlObjectSerializerReadContext? context, bool attributesRead)
    {
        var value = ReadAdaptedXmlValue(xmlReader, context, out var replacement, attributesRead);
        return replacement.HasReplacement ? (TValue)replacement.Value! : value;
    }

    internal override object? ReadXmlValueWithParsedAttributes(XmlReaderDelegator xmlReader, XmlObjectSerializerReadContext context)
    {
        if (GetType() != typeof(ClassDataContract<TWire, TValue>))
            return ReadXmlValue(xmlReader, context);

        var value = ReadAdaptedXmlValue(xmlReader, context, out var replacement, attributesRead: true);
        return replacement.HasReplacement ? replacement.Value : value;
    }

    public override object? ReadXmlValue(XmlReaderDelegator xmlReader, XmlObjectSerializerReadContext? context)
    {
        var value = ReadAdaptedXmlValue(xmlReader, context, out var replacement);
        return replacement.HasReplacement ? replacement.Value : value;
    }

    public override void WriteXmlValue(XmlWriterDelegator xmlWriter, object obj, XmlObjectSerializerWriteContext? context)
    {
        Debug.Assert(context != null);
        Write(xmlWriter, context, WriteAdapter((TValue)obj));
    }
}