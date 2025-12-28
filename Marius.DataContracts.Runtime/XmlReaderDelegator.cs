// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license

using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.Serialization;
using System.Xml;
using System.Xml.Serialization;

namespace Marius.DataContracts.Runtime;

[EditorBrowsable(EditorBrowsableState.Advanced)]
public class XmlReaderDelegator
{
    private readonly XmlDictionaryReader? _dictionaryReader;
    private bool _isEndOfEmptyElement;

    public XmlReaderDelegator(XmlReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);

        UnderlyingReader = reader;
        _dictionaryReader = reader as XmlDictionaryReader;
    }

    public XmlReader UnderlyingReader { get; }

    internal ExtensionDataReader? UnderlyingExtensionDataReader => UnderlyingReader as ExtensionDataReader;

    public int AttributeCount => _isEndOfEmptyElement ? 0 : UnderlyingReader.AttributeCount;

    public string? GetAttribute(string name)
    {
        return _isEndOfEmptyElement ? null : UnderlyingReader.GetAttribute(name);
    }

    public string? GetAttribute(string name, string namespaceUri)
    {
        return _isEndOfEmptyElement ? null : UnderlyingReader.GetAttribute(name, namespaceUri);
    }

    public string GetAttribute(int i)
    {
        if (_isEndOfEmptyElement)
            throw new ArgumentOutOfRangeException(nameof(i), SR.XmlElementAttributes);

        return UnderlyingReader.GetAttribute(i);
    }

    [SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Conceptually, this property describes this instance. Callers should expect to have an instance on hand to 'ask' about this 'emtpy' circumstance.")]
    public bool IsEmptyElement => false;

    public bool IsNamespaceURI(string ns)
    {
        if (_dictionaryReader == null)
            return ns == UnderlyingReader.NamespaceURI;

        return _dictionaryReader.IsNamespaceUri(ns);
    }

    public bool IsLocalName(string localName)
    {
        if (_dictionaryReader == null)
            return localName == UnderlyingReader.LocalName;

        return _dictionaryReader.IsLocalName(localName);
    }

    public bool IsNamespaceUri(XmlDictionaryString ns)
    {
        if (_dictionaryReader == null)
            return ns.Value == UnderlyingReader.NamespaceURI;

        return _dictionaryReader.IsNamespaceUri(ns);
    }

    public bool IsLocalName(XmlDictionaryString localName)
    {
        if (_dictionaryReader == null)
            return localName.Value == UnderlyingReader.LocalName;

        return _dictionaryReader.IsLocalName(localName);
    }

    public int IndexOfLocalName(XmlDictionaryString[] localNames, XmlDictionaryString ns)
    {
        if (_dictionaryReader != null)
            return _dictionaryReader.IndexOfLocalName(localNames, ns);

        if (UnderlyingReader.NamespaceURI == ns.Value)
        {
            var localName = LocalName;
            for (var i = 0; i < localNames.Length; i++)
            {
                if (localName == localNames[i].Value)
                    return i;
            }
        }

        return -1;
    }

    public bool IsStartElement()
    {
        return !_isEndOfEmptyElement && UnderlyingReader.IsStartElement();
    }

    public bool IsStartElement(string localname, string ns)
    {
        return !_isEndOfEmptyElement && UnderlyingReader.IsStartElement(localname, ns);
    }

    public bool IsStartElement(XmlDictionaryString localname, XmlDictionaryString ns)
    {
        if (_dictionaryReader == null)
            return !_isEndOfEmptyElement && UnderlyingReader.IsStartElement(localname.Value, ns.Value);

        return !_isEndOfEmptyElement && _dictionaryReader.IsStartElement(localname, ns);
    }

    public bool MoveToAttribute(string name)
    {
        return !_isEndOfEmptyElement && UnderlyingReader.MoveToAttribute(name);
    }

    public bool MoveToAttribute(string name, string ns)
    {
        return !_isEndOfEmptyElement && UnderlyingReader.MoveToAttribute(name, ns);
    }

    public void MoveToAttribute(int i)
    {
        if (_isEndOfEmptyElement)
            throw new ArgumentOutOfRangeException(nameof(i), SR.XmlElementAttributes);

        UnderlyingReader.MoveToAttribute(i);
    }

    public bool MoveToElement() => !_isEndOfEmptyElement && UnderlyingReader.MoveToElement();
    public bool MoveToFirstAttribute() => !_isEndOfEmptyElement && UnderlyingReader.MoveToFirstAttribute();
    public bool MoveToNextAttribute() => !_isEndOfEmptyElement && UnderlyingReader.MoveToNextAttribute();
    public bool MoveToNextElement() => MoveToContent() != XmlNodeType.EndElement;
    public XmlNodeType NodeType => _isEndOfEmptyElement ? XmlNodeType.EndElement : UnderlyingReader.NodeType;

    public bool Read()
    {
        //reader.MoveToFirstAttribute();
        //if (NodeType == XmlNodeType.Attribute)
        UnderlyingReader.MoveToElement();
        if (!UnderlyingReader.IsEmptyElement)
            return UnderlyingReader.Read();

        if (_isEndOfEmptyElement)
        {
            _isEndOfEmptyElement = false;
            return UnderlyingReader.Read();
        }

        _isEndOfEmptyElement = true;
        return true;
    }

    public XmlNodeType MoveToContent()
    {
        if (_isEndOfEmptyElement)
            return XmlNodeType.EndElement;

        return UnderlyingReader.MoveToContent();
    }

    public bool ReadAttributeValue()
    {
        return !_isEndOfEmptyElement && UnderlyingReader.ReadAttributeValue();
    }

    public void ReadEndElement()
    {
        if (_isEndOfEmptyElement)
            Read();
        else
            UnderlyingReader.ReadEndElement();
    }

    private static InvalidDataContractException CreateInvalidPrimitiveTypeException(Type type)
    {
        return new InvalidDataContractException(SR.Format(
            type.IsInterface ? SR.InterfaceTypeCannotBeCreated : SR.InvalidPrimitiveType_Serialization,
            DataContract.GetClrTypeFullName(type)));
    }

    public object ReadElementContentAsAnyType(Type valueType)
    {
        Read();
        var o = ReadContentAsAnyType(valueType);
        ReadEndElement();
        return o;
    }

    public object ReadContentAsAnyType(Type valueType)
    {
        switch (Type.GetTypeCode(valueType))
        {
            case TypeCode.Boolean:
                return ReadContentAsBoolean();
            case TypeCode.Char:
                return ReadContentAsChar();
            case TypeCode.Byte:
                return ReadContentAsUnsignedByte();
            case TypeCode.Int16:
                return ReadContentAsShort();
            case TypeCode.Int32:
                return ReadContentAsInt();
            case TypeCode.Int64:
                return ReadContentAsLong();
            case TypeCode.Single:
                return ReadContentAsSingle();
            case TypeCode.Double:
                return ReadContentAsDouble();
            case TypeCode.Decimal:
                return ReadContentAsDecimal();
            case TypeCode.DateTime:
                return ReadContentAsDateTime();
            case TypeCode.String:
                return ReadContentAsString();

            case TypeCode.SByte:
                return ReadContentAsSignedByte();
            case TypeCode.UInt16:
                return ReadContentAsUnsignedShort();
            case TypeCode.UInt32:
                return ReadContentAsUnsignedInt();
            case TypeCode.UInt64:
                return ReadContentAsUnsignedLong();
            case TypeCode.Empty:
            case TypeCode.DBNull:
            case TypeCode.Object:
            default:
                if (valueType == Globals.TypeOfByteArray)
                    return ReadContentAsBase64();

                if (valueType == Globals.TypeOfObject)
                    return new object();

                if (valueType == Globals.TypeOfTimeSpan)
                    return ReadContentAsTimeSpan();

                if (valueType == Globals.TypeOfGuid)
                    return ReadContentAsGuid();

                if (valueType == Globals.TypeOfUri)
                    return ReadContentAsUri();
                if (valueType == Globals.TypeOfXmlQualifiedName)
                    return ReadContentAsQName();

                break;
        }

        throw CreateInvalidPrimitiveTypeException(valueType);
    }

    public IDataNode ReadExtensionData(Type valueType)
    {
        switch (Type.GetTypeCode(valueType))
        {
            case TypeCode.Boolean:
                return new DataNode<bool>(ReadContentAsBoolean());
            case TypeCode.Char:
                return new DataNode<char>(ReadContentAsChar());
            case TypeCode.Byte:
                return new DataNode<byte>(ReadContentAsUnsignedByte());
            case TypeCode.Int16:
                return new DataNode<short>(ReadContentAsShort());
            case TypeCode.Int32:
                return new DataNode<int>(ReadContentAsInt());
            case TypeCode.Int64:
                return new DataNode<long>(ReadContentAsLong());
            case TypeCode.Single:
                return new DataNode<float>(ReadContentAsSingle());
            case TypeCode.Double:
                return new DataNode<double>(ReadContentAsDouble());
            case TypeCode.Decimal:
                return new DataNode<decimal>(ReadContentAsDecimal());
            case TypeCode.DateTime:
                return new DataNode<DateTime>(ReadContentAsDateTime());
            case TypeCode.String:
                return new DataNode<string>(ReadContentAsString());
            case TypeCode.SByte:
                return new DataNode<sbyte>(ReadContentAsSignedByte());
            case TypeCode.UInt16:
                return new DataNode<ushort>(ReadContentAsUnsignedShort());
            case TypeCode.UInt32:
                return new DataNode<uint>(ReadContentAsUnsignedInt());
            case TypeCode.UInt64:
                return new DataNode<ulong>(ReadContentAsUnsignedLong());
            case TypeCode.Empty:
            case TypeCode.DBNull:
            case TypeCode.Object:
            default:
                if (valueType == Globals.TypeOfByteArray)
                    return new DataNode<byte[]>(ReadContentAsBase64());

                if (valueType == Globals.TypeOfObject)
                    return new DataNode<object>(new object());

                if (valueType == Globals.TypeOfTimeSpan)
                    return new DataNode<TimeSpan>(ReadContentAsTimeSpan());

                if (valueType == Globals.TypeOfGuid)
                    return new DataNode<Guid>(ReadContentAsGuid());

                if (valueType == Globals.TypeOfUri)
                    return new DataNode<Uri>(ReadContentAsUri());

                if (valueType == Globals.TypeOfXmlQualifiedName)
                    return new DataNode<XmlQualifiedName>(ReadContentAsQName());

                break;
        }

        throw CreateInvalidPrimitiveTypeException(valueType);
    }

    [DoesNotReturn]
    private void ThrowConversionException(string value, string type)
    {
        throw new XmlException(XmlObjectSerializer.TryAddLineInfo(this, SR.Format(SR.XmlInvalidConversion, value, type)));
    }

    [DoesNotReturn]
    private static void ThrowNotAtElement()
    {
        throw new XmlException(SR.Format(SR.XmlStartElementExpected, "EndElement"));
    }

    public virtual char ReadElementContentAsChar()
    {
        return ToChar(ReadElementContentAsInt());
    }

    public virtual char ReadContentAsChar()
    {
        return ToChar(ReadContentAsInt());
    }

    private char ToChar(int value)
    {
        if (value < char.MinValue || value > char.MaxValue)
        {
            ThrowConversionException(value.ToString(NumberFormatInfo.CurrentInfo), "Char");
        }

        return (char)value;
    }

    public string ReadElementContentAsString()
    {
        if (_isEndOfEmptyElement)
            ThrowNotAtElement();

        return UnderlyingReader.ReadElementContentAsString();
    }

    public string ReadContentAsString()
    {
        return _isEndOfEmptyElement ? string.Empty : UnderlyingReader.ReadContentAsString();
    }

    public bool ReadElementContentAsBoolean()
    {
        if (_isEndOfEmptyElement)
            ThrowNotAtElement();

        return UnderlyingReader.ReadElementContentAsBoolean();
    }

    public bool ReadContentAsBoolean()
    {
        if (_isEndOfEmptyElement)
            ThrowConversionException(string.Empty, "Boolean");

        return UnderlyingReader.ReadContentAsBoolean();
    }

    public float ReadElementContentAsFloat()
    {
        if (_isEndOfEmptyElement)
            ThrowNotAtElement();

        return UnderlyingReader.ReadElementContentAsFloat();
    }

    public float ReadContentAsSingle()
    {
        if (_isEndOfEmptyElement)
            ThrowConversionException(string.Empty, "Float");

        return UnderlyingReader.ReadContentAsFloat();
    }

    public double ReadElementContentAsDouble()
    {
        if (_isEndOfEmptyElement)
            ThrowNotAtElement();

        return UnderlyingReader.ReadElementContentAsDouble();
    }

    public double ReadContentAsDouble()
    {
        if (_isEndOfEmptyElement)
            ThrowConversionException(string.Empty, "Double");

        return UnderlyingReader.ReadContentAsDouble();
    }

    public decimal ReadElementContentAsDecimal()
    {
        if (_isEndOfEmptyElement)
            ThrowNotAtElement();

        return UnderlyingReader.ReadElementContentAsDecimal();
    }

    public decimal ReadContentAsDecimal()
    {
        if (_isEndOfEmptyElement)
            ThrowConversionException(string.Empty, "Decimal");

        return UnderlyingReader.ReadContentAsDecimal();
    }

    public virtual byte[] ReadElementContentAsBase64()
    {
        if (_isEndOfEmptyElement)
            ThrowNotAtElement();

        if (_dictionaryReader == null)
        {
            return ReadContentAsBase64(UnderlyingReader.ReadElementContentAsString());
        }

        return _dictionaryReader.ReadElementContentAsBase64();
    }

    public virtual byte[] ReadContentAsBase64()
    {
        if (_isEndOfEmptyElement)
            return Array.Empty<byte>();

        if (_dictionaryReader == null)
        {
            return ReadContentAsBase64(UnderlyingReader.ReadContentAsString());
        }

        return _dictionaryReader.ReadContentAsBase64();
    }

    [return: NotNullIfNotNull(nameof(str))]
    public static byte[]? ReadContentAsBase64(string? str)
    {
        if (str == null)
            return null;

        str = str.Trim();
        if (str.Length == 0)
            return Array.Empty<byte>();

        try
        {
            return Convert.FromBase64String(str);
        }
        catch (ArgumentException exception)
        {
            throw XmlExceptionHelper.CreateConversionException(str, "byte[]", exception);
        }
        catch (FormatException exception)
        {
            throw XmlExceptionHelper.CreateConversionException(str, "byte[]", exception);
        }
    }

    public virtual DateTime ReadElementContentAsDateTime()
    {
        if (_isEndOfEmptyElement)
            ThrowNotAtElement();

        return UnderlyingReader.ReadElementContentAsDateTime();
    }

    public virtual DateTime ReadContentAsDateTime()
    {
        if (_isEndOfEmptyElement)
            ThrowConversionException(string.Empty, "DateTime");

        return UnderlyingReader.ReadContentAsDateTime();
    }

    public virtual DateOnly ReadElementContentAsDateOnly()
    {
        if (_isEndOfEmptyElement)
            ThrowNotAtElement();
        var s = UnderlyingReader.ReadElementContentAsString();
        try
        {
            return ParseDateOnly(s);
        }
        catch (Exception ex) when (ex is FormatException || ex is ArgumentException)
        {
            ThrowConversionException(s, nameof(DateOnly));
            throw; // unreachable
        }
    }

    public virtual DateOnly ReadContentAsDateOnly()
    {
        if (_isEndOfEmptyElement)
            ThrowConversionException(string.Empty, nameof(DateOnly));
        var s = UnderlyingReader.ReadContentAsString();
        try
        {
            return ParseDateOnly(s);
        }
        catch (Exception ex) when (ex is FormatException || ex is ArgumentException)
        {
            ThrowConversionException(s, nameof(DateOnly));
            throw; // unreachable
        }
    }

    public virtual TimeOnly ReadElementContentAsTimeOnly()
    {
        if (_isEndOfEmptyElement)
            ThrowNotAtElement();

        var s = UnderlyingReader.ReadElementContentAsString();

        try
        {
            var dto = XmlConvert.ToDateTimeOffset(s);
            return TimeOnly.FromTimeSpan(dto.TimeOfDay);
        }
        catch (Exception ex) when (ex is FormatException || ex is ArgumentException)
        {
            ThrowConversionException(s, nameof(TimeOnly));
            throw; // unreachable
        }
    }

    public virtual TimeOnly ReadContentAsTimeOnly()
    {
        if (_isEndOfEmptyElement)
            ThrowConversionException(string.Empty, nameof(TimeOnly));

        var s = UnderlyingReader.ReadContentAsString();
        try
        {
            var dto = XmlConvert.ToDateTimeOffset(s);
            return TimeOnly.FromTimeSpan(dto.TimeOfDay);
        }
        catch (Exception ex) when (ex is FormatException || ex is ArgumentException)
        {
            ThrowConversionException(s, nameof(TimeOnly));
            throw; // unreachable
        }
    }

    private static DateOnly ParseDateOnly(string s)
    {
        return DateOnly.ParseExact(s, "yyyy-MM-dd", DateTimeFormatInfo.InvariantInfo, DateTimeStyles.AllowLeadingWhite | DateTimeStyles.AllowTrailingWhite);
    }

    private static TimeOnly ParseTimeOnly(string s)
    {
        // Strictly parse the expected TimeOnly format. No timezone/offset allowed.
        return TimeOnly.ParseExact(s, "HH:mm:ss.FFFFFFF", DateTimeFormatInfo.InvariantInfo, DateTimeStyles.AllowLeadingWhite | DateTimeStyles.AllowTrailingWhite);
    }

    public int ReadElementContentAsInt()
    {
        if (_isEndOfEmptyElement)
            ThrowNotAtElement();

        return UnderlyingReader.ReadElementContentAsInt();
    }

    public int ReadContentAsInt()
    {
        if (_isEndOfEmptyElement)
            ThrowConversionException(string.Empty, "Int32");

        return UnderlyingReader.ReadContentAsInt();
    }

    public long ReadElementContentAsLong()
    {
        if (_isEndOfEmptyElement)
            ThrowNotAtElement();

        return UnderlyingReader.ReadElementContentAsLong();
    }

    public long ReadContentAsLong()
    {
        if (_isEndOfEmptyElement)
            ThrowConversionException(string.Empty, "Int64");

        return UnderlyingReader.ReadContentAsLong();
    }

    public short ReadElementContentAsShort()
    {
        return ToShort(ReadElementContentAsInt());
    }

    public short ReadContentAsShort()
    {
        return ToShort(ReadContentAsInt());
    }

    private short ToShort(int value)
    {
        if (value < short.MinValue || value > short.MaxValue)
        {
            ThrowConversionException(value.ToString(NumberFormatInfo.CurrentInfo), "Int16");
        }

        return (short)value;
    }

    public byte ReadElementContentAsUnsignedByte()
    {
        return ToByte(ReadElementContentAsInt());
    }

    public byte ReadContentAsUnsignedByte()
    {
        return ToByte(ReadContentAsInt());
    }

    private byte ToByte(int value)
    {
        if (value < byte.MinValue || value > byte.MaxValue)
        {
            ThrowConversionException(value.ToString(NumberFormatInfo.CurrentInfo), "Byte");
        }

        return (byte)value;
    }

    public sbyte ReadElementContentAsSignedByte()
    {
        return ToSByte(ReadElementContentAsInt());
    }

    public sbyte ReadContentAsSignedByte()
    {
        return ToSByte(ReadContentAsInt());
    }

    private sbyte ToSByte(int value)
    {
        if (value < sbyte.MinValue || value > sbyte.MaxValue)
        {
            ThrowConversionException(value.ToString(NumberFormatInfo.CurrentInfo), "SByte");
        }

        return (sbyte)value;
    }

    public uint ReadElementContentAsUnsignedInt()
    {
        return ToUInt32(ReadElementContentAsLong());
    }

    public uint ReadContentAsUnsignedInt()
    {
        return ToUInt32(ReadContentAsLong());
    }

    private uint ToUInt32(long value)
    {
        if (value < uint.MinValue || value > uint.MaxValue)
        {
            ThrowConversionException(value.ToString(NumberFormatInfo.CurrentInfo), "UInt32");
        }

        return (uint)value;
    }

    public virtual ulong ReadElementContentAsUnsignedLong()
    {
        if (_isEndOfEmptyElement)
            ThrowNotAtElement();

        var str = UnderlyingReader.ReadElementContentAsString();

        if (string.IsNullOrEmpty(str))
            ThrowConversionException(string.Empty, "UInt64");

        return XmlConverter.ToUInt64(str);
    }

    public virtual ulong ReadContentAsUnsignedLong()
    {
        var str = UnderlyingReader.ReadContentAsString();

        if (string.IsNullOrEmpty(str))
            ThrowConversionException(string.Empty, "UInt64");

        return XmlConverter.ToUInt64(str);
    }

    public ushort ReadElementContentAsUnsignedShort()
    {
        return ToUInt16(ReadElementContentAsInt());
    }

    public ushort ReadContentAsUnsignedShort()
    {
        return ToUInt16(ReadContentAsInt());
    }

    private ushort ToUInt16(int value)
    {
        if (value < ushort.MinValue || value > ushort.MaxValue)
        {
            ThrowConversionException(value.ToString(NumberFormatInfo.CurrentInfo), "UInt16");
        }

        return (ushort)value;
    }

    public TimeSpan ReadElementContentAsTimeSpan()
    {
        if (_isEndOfEmptyElement)
            ThrowNotAtElement();

        var str = UnderlyingReader.ReadElementContentAsString();
        return XmlConverter.ToTimeSpan(str);
    }

    public TimeSpan ReadContentAsTimeSpan()
    {
        var str = UnderlyingReader.ReadContentAsString();
        return XmlConverter.ToTimeSpan(str);
    }

    public Guid ReadElementContentAsGuid()
    {
        if (_isEndOfEmptyElement)
            ThrowNotAtElement();

        var str = UnderlyingReader.ReadElementContentAsString();
        try
        {
            return new Guid(str);
        }
        catch (ArgumentException exception)
        {
            throw XmlExceptionHelper.CreateConversionException(str, "Guid", exception);
        }
        catch (FormatException exception)
        {
            throw XmlExceptionHelper.CreateConversionException(str, "Guid", exception);
        }
        catch (OverflowException exception)
        {
            throw XmlExceptionHelper.CreateConversionException(str, "Guid", exception);
        }
    }

    public Guid ReadContentAsGuid()
    {
        var str = UnderlyingReader.ReadContentAsString();
        try
        {
            return Guid.Parse(str);
        }
        catch (ArgumentException exception)
        {
            throw XmlExceptionHelper.CreateConversionException(str, "Guid", exception);
        }
        catch (FormatException exception)
        {
            throw XmlExceptionHelper.CreateConversionException(str, "Guid", exception);
        }
        catch (OverflowException exception)
        {
            throw XmlExceptionHelper.CreateConversionException(str, "Guid", exception);
        }
    }

    public Uri ReadElementContentAsUri()
    {
        if (_isEndOfEmptyElement)
            ThrowNotAtElement();

        var str = ReadElementContentAsString();
        try
        {
            return new Uri(str, UriKind.RelativeOrAbsolute);
        }
        catch (ArgumentException exception)
        {
            throw XmlExceptionHelper.CreateConversionException(str, "Uri", exception);
        }
        catch (FormatException exception)
        {
            throw XmlExceptionHelper.CreateConversionException(str, "Uri", exception);
        }
    }

    public Uri ReadContentAsUri()
    {
        var str = ReadContentAsString();
        try
        {
            return new Uri(str, UriKind.RelativeOrAbsolute);
        }
        catch (ArgumentException exception)
        {
            throw XmlExceptionHelper.CreateConversionException(str, "Uri", exception);
        }
        catch (FormatException exception)
        {
            throw XmlExceptionHelper.CreateConversionException(str, "Uri", exception);
        }
    }

    public XmlQualifiedName ReadElementContentAsQName()
    {
        Read();
        var obj = ReadContentAsQName();
        ReadEndElement();
        return obj;
    }

    public virtual XmlQualifiedName ReadContentAsQName()
    {
        return ParseQualifiedName(ReadContentAsString());
    }

    private XmlQualifiedName ParseQualifiedName(string str)
    {
        string name;
        string? ns;
        if (string.IsNullOrEmpty(str))
            name = ns = string.Empty;
        else
            XmlObjectSerializerReadContext.ParseQualifiedName(str, this, out name, out ns, out _);
        return new XmlQualifiedName(name, ns);
    }

    private static void CheckExpectedArrayLength(XmlObjectSerializerReadContext context, int arrayLength)
    {
        context.IncrementItemCount(arrayLength);
    }

    protected int GetArrayLengthQuota(XmlObjectSerializerReadContext context)
    {
        if (_dictionaryReader?.Quotas == null)
            return context.RemainingItemCount;

        return Math.Min(context.RemainingItemCount, _dictionaryReader.Quotas.MaxArrayLength);
    }

    private static void CheckActualArrayLength(int expectedLength, int actualLength, XmlDictionaryString itemName, XmlDictionaryString itemNamespace)
    {
        if (expectedLength != actualLength)
            throw XmlObjectSerializer.CreateSerializationException(SR.Format(SR.ArrayExceededSizeAttribute, expectedLength, itemName.Value, itemNamespace.Value));
    }

    public bool TryReadBooleanArray(XmlObjectSerializerReadContext context,
        XmlDictionaryString itemName, XmlDictionaryString itemNamespace,
        int arrayLength, [NotNullWhen(true)] out bool[]? array)
    {
        if (_dictionaryReader == null)
        {
            array = null;
            return false;
        }

        if (arrayLength != -1)
        {
            CheckExpectedArrayLength(context, arrayLength);
            array = new bool[arrayLength];
            int read, offset = 0;
            while ((read = _dictionaryReader.ReadArray(itemName, itemNamespace, array, offset, arrayLength - offset)) > 0)
            {
                offset += read;
            }

            CheckActualArrayLength(arrayLength, offset, itemName, itemNamespace);
        }
        else
        {
            array = BooleanArrayHelperWithDictionaryString.Instance.ReadArray(
                _dictionaryReader, itemName, itemNamespace, GetArrayLengthQuota(context));
            context.IncrementItemCount(array.Length);
        }

        return true;
    }

    public virtual bool TryReadDateTimeArray(XmlObjectSerializerReadContext context,
        XmlDictionaryString itemName, XmlDictionaryString itemNamespace,
        int arrayLength, [NotNullWhen(true)] out DateTime[]? array)
    {
        if (_dictionaryReader == null)
        {
            array = null;
            return false;
        }

        if (arrayLength != -1)
        {
            CheckExpectedArrayLength(context, arrayLength);
            array = new DateTime[arrayLength];
            int read, offset = 0;
            while ((read = _dictionaryReader.ReadArray(itemName, itemNamespace, array, offset, arrayLength - offset)) > 0)
            {
                offset += read;
            }

            CheckActualArrayLength(arrayLength, offset, itemName, itemNamespace);
        }
        else
        {
            array = DateTimeArrayHelperWithDictionaryString.Instance.ReadArray(
                _dictionaryReader, itemName, itemNamespace, GetArrayLengthQuota(context));
            context.IncrementItemCount(array.Length);
        }

        return true;
    }

    public bool TryReadDecimalArray(XmlObjectSerializerReadContext context,
        XmlDictionaryString itemName, XmlDictionaryString itemNamespace,
        int arrayLength, [NotNullWhen(true)] out decimal[]? array)
    {
        if (_dictionaryReader == null)
        {
            array = null;
            return false;
        }

        if (arrayLength != -1)
        {
            CheckExpectedArrayLength(context, arrayLength);
            array = new decimal[arrayLength];
            int read, offset = 0;
            while ((read = _dictionaryReader.ReadArray(itemName, itemNamespace, array, offset, arrayLength - offset)) > 0)
            {
                offset += read;
            }

            CheckActualArrayLength(arrayLength, offset, itemName, itemNamespace);
        }
        else
        {
            array = DecimalArrayHelperWithDictionaryString.Instance.ReadArray(
                _dictionaryReader, itemName, itemNamespace, GetArrayLengthQuota(context));
            context.IncrementItemCount(array.Length);
        }

        return true;
    }

    public bool TryReadInt32Array(XmlObjectSerializerReadContext context,
        XmlDictionaryString itemName, XmlDictionaryString itemNamespace,
        int arrayLength, [NotNullWhen(true)] out int[]? array)
    {
        if (_dictionaryReader == null)
        {
            array = null;
            return false;
        }

        if (arrayLength != -1)
        {
            CheckExpectedArrayLength(context, arrayLength);
            array = new int[arrayLength];
            int read, offset = 0;
            while ((read = _dictionaryReader.ReadArray(itemName, itemNamespace, array, offset, arrayLength - offset)) > 0)
            {
                offset += read;
            }

            CheckActualArrayLength(arrayLength, offset, itemName, itemNamespace);
        }
        else
        {
            array = Int32ArrayHelperWithDictionaryString.Instance.ReadArray(
                _dictionaryReader, itemName, itemNamespace, GetArrayLengthQuota(context));
            context.IncrementItemCount(array.Length);
        }

        return true;
    }

    public bool TryReadInt64Array(XmlObjectSerializerReadContext context,
        XmlDictionaryString itemName, XmlDictionaryString itemNamespace,
        int arrayLength, [NotNullWhen(true)] out long[]? array)
    {
        if (_dictionaryReader == null)
        {
            array = null;
            return false;
        }

        if (arrayLength != -1)
        {
            CheckExpectedArrayLength(context, arrayLength);
            array = new long[arrayLength];
            int read, offset = 0;
            while ((read = _dictionaryReader.ReadArray(itemName, itemNamespace, array, offset, arrayLength - offset)) > 0)
            {
                offset += read;
            }

            CheckActualArrayLength(arrayLength, offset, itemName, itemNamespace);
        }
        else
        {
            array = Int64ArrayHelperWithDictionaryString.Instance.ReadArray(
                _dictionaryReader, itemName, itemNamespace, GetArrayLengthQuota(context));
            context.IncrementItemCount(array.Length);
        }

        return true;
    }

    public bool TryReadSingleArray(XmlObjectSerializerReadContext context,
        XmlDictionaryString itemName, XmlDictionaryString itemNamespace,
        int arrayLength, [NotNullWhen(true)] out float[]? array)
    {
        if (_dictionaryReader == null)
        {
            array = null;
            return false;
        }

        if (arrayLength != -1)
        {
            CheckExpectedArrayLength(context, arrayLength);
            array = new float[arrayLength];
            int read, offset = 0;
            while ((read = _dictionaryReader.ReadArray(itemName, itemNamespace, array, offset, arrayLength - offset)) > 0)
            {
                offset += read;
            }

            CheckActualArrayLength(arrayLength, offset, itemName, itemNamespace);
        }
        else
        {
            array = SingleArrayHelperWithDictionaryString.Instance.ReadArray(
                _dictionaryReader, itemName, itemNamespace, GetArrayLengthQuota(context));
            context.IncrementItemCount(array.Length);
        }

        return true;
    }

    public bool TryReadDoubleArray(XmlObjectSerializerReadContext context,
        XmlDictionaryString itemName, XmlDictionaryString itemNamespace,
        int arrayLength, [NotNullWhen(true)] out double[]? array)
    {
        if (_dictionaryReader == null)
        {
            array = null;
            return false;
        }

        if (arrayLength != -1)
        {
            CheckExpectedArrayLength(context, arrayLength);
            array = new double[arrayLength];
            int read, offset = 0;
            while ((read = _dictionaryReader.ReadArray(itemName, itemNamespace, array, offset, arrayLength - offset)) > 0)
            {
                offset += read;
            }

            CheckActualArrayLength(arrayLength, offset, itemName, itemNamespace);
        }
        else
        {
            array = DoubleArrayHelperWithDictionaryString.Instance.ReadArray(
                _dictionaryReader, itemName, itemNamespace, GetArrayLengthQuota(context));
            context.IncrementItemCount(array.Length);
        }

        return true;
    }

    public IDictionary<string, string>? GetNamespacesInScope(XmlNamespaceScope scope)
    {
        return (UnderlyingReader as IXmlNamespaceResolver)?.GetNamespacesInScope(scope);
    }

    // IXmlLineInfo members
    public bool HasLineInfo()
    {
        var iXmlLineInfo = UnderlyingReader as IXmlLineInfo;
        return iXmlLineInfo?.HasLineInfo() ?? false;
    }

    public int LineNumber
    {
        get
        {
            var iXmlLineInfo = UnderlyingReader as IXmlLineInfo;
            return iXmlLineInfo?.LineNumber ?? 0;
        }
    }

    public int LinePosition
    {
        get
        {
            var iXmlLineInfo = UnderlyingReader as IXmlLineInfo;
            return iXmlLineInfo?.LinePosition ?? 0;
        }
    }

    // IXmlTextParser members
    public bool Normalized
    {
        get
        {
            if (UnderlyingReader is not XmlTextReader xmlTextReader)
            {
                var xmlTextParser = UnderlyingReader as IXmlTextParser;
                return xmlTextParser?.Normalized ?? false;
            }

            return xmlTextReader.Normalization;
        }
        set
        {
            if (UnderlyingReader is not XmlTextReader xmlTextReader)
            {
                if (UnderlyingReader is IXmlTextParser xmlTextParser)
                    xmlTextParser.Normalized = value;
            }
            else
            {
                xmlTextReader.Normalization = value;
            }
        }
    }

    public WhitespaceHandling WhitespaceHandling
    {
        get
        {
            if (UnderlyingReader is not XmlTextReader xmlTextReader)
            {
                var xmlTextParser = UnderlyingReader as IXmlTextParser;
                return xmlTextParser?.WhitespaceHandling ?? WhitespaceHandling.None;
            }

            return xmlTextReader.WhitespaceHandling;
        }
        set
        {
            if (UnderlyingReader is not XmlTextReader xmlTextReader)
            {
                if (UnderlyingReader is IXmlTextParser xmlTextParser)
                    xmlTextParser.WhitespaceHandling = value;
            }
            else
            {
                xmlTextReader.WhitespaceHandling = value;
            }
        }
    }

    // delegating properties and methods
    public string Name => UnderlyingReader.Name;
    public string LocalName => UnderlyingReader.LocalName;
    public string NamespaceURI => UnderlyingReader.NamespaceURI;
    public string Value => UnderlyingReader.Value;
    public Type ValueType => UnderlyingReader.ValueType;
    public int Depth => UnderlyingReader.Depth;
    public string? LookupNamespace(string prefix) { return UnderlyingReader.LookupNamespace(prefix); }
    public bool EOF => UnderlyingReader.EOF;

    public void Skip()
    {
        UnderlyingReader.Skip();
        _isEndOfEmptyElement = false;
    }
}