// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license

using System.Collections.Immutable;
using System.Runtime.CompilerServices;

namespace Marius.DataContracts.Runtime;

// The general context has no enum constraint. Only an exact enum contract can
// opt in, so subclasses keep all of their virtual object hooks.
internal interface ITypedEnumContract<T>
{
    bool CanUseTyped { get; }
    T ReadXmlValueTyped(XmlReaderDelegator reader, XmlObjectSerializerReadContext context);
    void WriteXmlValueTyped(XmlWriterDelegator writer, T value);
}

public abstract class EnumDataContract : DataContract
{
    public required ImmutableArray<string> MemberNames { get; init; }
    public required ImmutableArray<long> Values { get; init; }

    public required bool IsFlags { get; init; }
    public required bool IsULong { get; init; }

    public abstract object ReadEnumValue(XmlReaderDelegator reader);

    // Share spelling and flags decomposition across enum widths. The caller
    // supplies an object for diagnostics only if the value is invalid.
    protected bool TryWriteValue(XmlWriterDelegator writer, long longValue)
    {
        for (var i = 0; i < Values.Length; i++)
        {
            if (longValue == Values[i])
            {
                writer.WriteString(MemberNames[i]);
                return true;
            }
        }

        if (IsFlags)
        {
            var zeroIndex = -1;
            var noneWritten = true;
            for (var i = 0; i < Values.Length; i++)
            {
                var current = Values[i];
                if (current == 0)
                {
                    zeroIndex = i;
                    continue;
                }

                if (longValue == 0)
                    break;

                if ((current & longValue) == current)
                {
                    if (noneWritten)
                        noneWritten = false;
                    else
                        writer.WriteString(DictionaryGlobals.Space.Value);

                    writer.WriteString(MemberNames[i]);
                    longValue &= ~current;
                }
            }

            // enforce that enum value was completely parsed
            if (longValue != 0)
                return false;

            if (noneWritten && zeroIndex >= 0)
                writer.WriteString(MemberNames[zeroIndex]);
        }
        else
            return false;

        return true;
    }
}

public class EnumDataContract<T> : EnumDataContract, ITypedEnumContract<T>
    where T : struct, Enum
{
    private static readonly TypeCode EnumTypeCode = Type.GetTypeCode(typeof(T));

    bool ITypedEnumContract<T>.CanUseTyped =>
        GetType() == typeof(EnumDataContract<T>) && UnderlyingType == typeof(T) &&
        IsULong == (EnumTypeCode == TypeCode.UInt64);

    T ITypedEnumContract<T>.ReadXmlValueTyped(XmlReaderDelegator reader, XmlObjectSerializerReadContext context)
    {
        var value = Read(reader);
        context.TryAddNewObject(value, out _);
        return value;
    }

    void ITypedEnumContract<T>.WriteXmlValueTyped(XmlWriterDelegator writer, T value)
    {
        if (!TryWriteValue(writer, ConvertToInt64(value)))
            ThrowInvalidValue(value);
    }

    public override object ReadXmlValue(XmlReaderDelegator xmlReader, XmlObjectSerializerReadContext? context)
    {
        var obj = ReadEnumValue(xmlReader);
        context?.AddNewObject(obj);
        return obj;
    }

    public override void WriteXmlValue(XmlWriterDelegator writer, object value, XmlObjectSerializerWriteContext? context)
    {
        // Public object callers historically accept other convertible values.
        // Keep their conversion and overflow behavior at this boundary.
        var longValue = value is T typedValue && IsULong == (EnumTypeCode == TypeCode.UInt64)
            ? ConvertToInt64(typedValue)
            : IsULong
                ? unchecked((long)Convert.ToUInt64(value, null))
                : Convert.ToInt64(value, null);
        if (!TryWriteValue(writer, longValue))
            ThrowInvalidValue(value);
    }

    private void ThrowInvalidValue(object value)
    {
        throw XmlObjectSerializer.CreateSerializationException(SR.Format(SR.InvalidEnumValueOnWrite, value, GetClrTypeFullName(UnderlyingType)));
    }

    public override object ReadEnumValue(XmlReaderDelegator reader)
    {
        if (IsULong)
            return Enum.ToObject(UnderlyingType, unchecked((ulong)ReadValue(reader)));

        return Enum.ToObject(UnderlyingType, ReadValue(reader));
    }

    public T Read(XmlReaderDelegator reader)
    {
        var longValue = ReadValue(reader);

        return ConvertFromInt64(longValue);
    }

    private long ReadEnumValue(string value, int index, int count)
    {
        for (var i = 0; i < MemberNames.Length; i++)
        {
            var memberName = MemberNames[i];
            if (memberName.Length == count && string.CompareOrdinal(value, index, memberName, 0, count) == 0)
                return Values[i];
        }

        throw XmlObjectSerializer.CreateSerializationException(SR.Format(SR.InvalidEnumValueOnRead, value.Substring(index, count), GetClrTypeFullName(UnderlyingType)));
    }

    private static T ConvertFromInt64(long value)
    {
        return EnumTypeCode switch
        {
            TypeCode.Int32 => (T)(object)(int)value,
            TypeCode.UInt32 => (T)(object)(uint)value,
            TypeCode.Int64 => (T)(object)value,
            TypeCode.UInt64 => (T)(object)(ulong)value,
            TypeCode.Int16 => (T)(object)(short)value,
            TypeCode.UInt16 => (T)(object)(ushort)value,
            TypeCode.SByte => (T)(object)(sbyte)value,
            _ => (T)(object)(byte)value,
        };
    }

    private static long ConvertToInt64(T value)
    {
        // Every supported enum has exactly one of these fixed-width layouts.
        // Reinterpret only its actual underlying type; sign extension matters
        // for signed minima, and ulong retains all 64 bits in the long table.
        return EnumTypeCode switch
        {
            TypeCode.Int32 => Unsafe.As<T, int>(ref value),
            TypeCode.UInt32 => Unsafe.As<T, uint>(ref value),
            TypeCode.Int64 => Unsafe.As<T, long>(ref value),
            TypeCode.UInt64 => unchecked((long)Unsafe.As<T, ulong>(ref value)),
            TypeCode.Int16 => Unsafe.As<T, short>(ref value),
            TypeCode.UInt16 => Unsafe.As<T, ushort>(ref value),
            TypeCode.SByte => Unsafe.As<T, sbyte>(ref value),
            _ => Unsafe.As<T, byte>(ref value),
        };
    }

    private long ReadValue(XmlReaderDelegator reader)
    {
        var stringValue = reader.ReadElementContentAsString();
        var longValue = 0L;
        var i = 0;
        if (IsFlags)
        {
            // Skip initial spaces
            for (; i < stringValue.Length; i++)
                if (stringValue[i] != ' ')
                    break;

            // Read space-delimited values
            var startIndex = i;
            int count;
            for (; i < stringValue.Length; i++)
            {
                if (stringValue[i] == ' ')
                {
                    count = i - startIndex;
                    if (count > 0)
                        longValue |= ReadEnumValue(stringValue, startIndex, count);
                    for (++i; i < stringValue.Length; i++)
                        if (stringValue[i] != ' ')
                            break;

                    startIndex = i;
                    if (i == stringValue.Length)
                        break;
                }
            }

            count = i - startIndex;
            if (count > 0)
                longValue |= ReadEnumValue(stringValue, startIndex, count);
        }
        else
        {
            if (stringValue.Length == 0)
                throw XmlObjectSerializer.CreateSerializationException(SR.Format(SR.InvalidEnumValueOnRead, stringValue, GetClrTypeFullName(UnderlyingType)));

            longValue = ReadEnumValue(stringValue, 0, stringValue.Length);
        }

        return longValue;
    }
}