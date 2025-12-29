// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license

using System.Buffers.Binary;
using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;
using System.Xml;

namespace Marius.DataContracts.Runtime;

public abstract class DataContract
{
    private static ReadOnlySpan<int> Shifts => [7, 12, 17, 22, 5, 9, 14, 20, 4, 11, 16, 23, 6, 10, 15, 21];

    private static ReadOnlySpan<uint> Sines =>
    [
        0xd76aa478, 0xe8c7b756, 0x242070db, 0xc1bdceee, 0xf57c0faf, 0x4787c62a, 0xa8304613, 0xfd469501,
        0x698098d8, 0x8b44f7af, 0xffff5bb1, 0x895cd7be, 0x6b901122, 0xfd987193, 0xa679438e, 0x49b40821,

        0xf61e2562, 0xc040b340, 0x265e5a51, 0xe9b6c7aa, 0xd62f105d, 0x02441453, 0xd8a1e681, 0xe7d3fbc8,
        0x21e1cde6, 0xc33707d6, 0xf4d50d87, 0x455a14ed, 0xa9e3e905, 0xfcefa3f8, 0x676f02d9, 0x8d2a4c8a,

        0xfffa3942, 0x8771f681, 0x6d9d6122, 0xfde5380c, 0xa4beea44, 0x4bdecfa9, 0xf6bb4b60, 0xbebfbc70,
        0x289b7ec6, 0xeaa127fa, 0xd4ef3085, 0x04881d05, 0xd9d4d039, 0xe6db99e5, 0x1fa27cf8, 0xc4ac5665,

        0xf4292244, 0x432aff97, 0xab9423a7, 0xfc93a039, 0x655b59c3, 0x8f0ccc92, 0xffeff47d, 0x85845dd1,
        0x6fa87e4f, 0xfe2ce6e0, 0xa3014314, 0x4e0811a1, 0xf7537e82, 0xbd3af235, 0x2ad7d2bb, 0xeb86d391,
    ];

    private static readonly object ClrTypeStringsLock = new object();
    private static XmlDictionary? ClrTypeStringsDictionary;
    private static Dictionary<string, XmlDictionaryString>? ClrTypeStrings;

    public required int Id { get; init; }
    public required Type UnderlyingType { get; init; }
    public required Type OriginalUnderlyingType { get; init; }
    public required XmlQualifiedName XmlName { get; init; }
    public required bool IsISerializable { get; init; }

    public required bool HasRoot { get; init; }
    public required bool IsPrimitive { get; init; }
    public required bool IsReference { get; init; }
    public required XmlDictionaryString? TopLevelElementName { get; init; }
    public required XmlDictionaryString? TopLevelElementNamespace { get; init; }
    public required XmlDictionaryString Name { get; init; }
    public required XmlDictionaryString Namespace { get; init; }

    public required bool IsBuiltInDataContract { get; init; }
    public required bool CanContainReferences { get; init; }

    public DataContract? BaseContract { get; set; }

    public FrozenDictionary<XmlQualifiedName, DataContract> KnownDataContracts { get; set; } = FrozenDictionary<XmlQualifiedName, DataContract>.Empty;

    public virtual object? ReadXmlValue(XmlReaderDelegator xmlReader, XmlObjectSerializerReadContext? context)
    {
        return null;
    }

    public virtual void WriteRootElement(XmlWriterDelegator writer, XmlDictionaryString name, XmlDictionaryString? ns)
    {
        if (ReferenceEquals(ns, DictionaryGlobals.SerializationNamespace) && !IsPrimitive)
            writer.WriteStartElement(Globals.SerPrefix, name, ns);
        else
            writer.WriteStartElement(name, ns);
    }

    public virtual void WriteXmlValue(XmlWriterDelegator xmlWriter, object obj, XmlObjectSerializerWriteContext? context)
    {
    }

    internal static string GetClrTypeFullName(Type type)
    {
        return !type.IsGenericTypeDefinition && type.ContainsGenericParameters ? type.Namespace + "." + type.Name : type.FullName!;
    }

    internal static XmlDictionaryString GetClrTypeString(string key)
    {
        lock (ClrTypeStringsLock)
        {
            if (ClrTypeStrings == null)
            {
                ClrTypeStringsDictionary = new XmlDictionary();
                ClrTypeStrings = new Dictionary<string, XmlDictionaryString>();
                try
                {
                    ClrTypeStrings.Add(Globals.TypeOfInt.Assembly.FullName!, ClrTypeStringsDictionary.Add(Globals.MscorlibAssemblyName));
                }
                catch (Exception ex) when (!ExceptionUtility.IsFatal(ex))
                {
                    throw new Exception(ex.Message, ex);
                }
            }

            if (ClrTypeStrings.TryGetValue(key, out var value))
                return value;

            value = ClrTypeStringsDictionary!.Add(key);
            try
            {
                ClrTypeStrings.Add(key, value);
            }
            catch (Exception ex) when (!ExceptionUtility.IsFatal(ex))
            {
                throw new Exception(ex.Message, ex);
            }

            return value;
        }
    }

    private static bool IsAsciiLocalName(string localName)
    {
        if (localName.Length == 0)
            return false;
        if (!char.IsAsciiLetter(localName[0]))
            return false;

        for (var i = 1; i < localName.Length; i++)
        {
            var ch = localName[i];
            if (!char.IsAsciiLetterOrDigit(ch))
                return false;
        }

        return true;
    }

    internal static string EncodeLocalName(string localName)
    {
        if (IsAsciiLocalName(localName))
            return localName;

        if (IsValidNCName(localName))
            return localName;

        return XmlConvert.EncodeLocalName(localName);
    }

    internal static bool IsValidNCName(string name)
    {
        try
        {
            XmlConvert.VerifyNCName(name);
            return true;
        }
        catch (XmlException)
        {
            return false;
        }
    }

    internal static void GetDefaultXmlName(string fullTypeName, out string localName, out string ns)
    {
        var typeReference = new CodeTypeReference(fullTypeName);
        GetDefaultName(typeReference, out localName, out ns);
    }

    private static void GetDefaultName(CodeTypeReference typeReference, out string localName, out string ns)
    {
        var fullTypeName = typeReference.BaseType;
        if (TryGetBuiltInDataContractName(fullTypeName, out var xmlName))
        {
            localName = xmlName.Value.Item1.Value;
            ns = xmlName.Value.Item2.Value;
            return;
        }

        GetClrNameAndNamespace(fullTypeName, out localName, out ns);
        if (typeReference.TypeArguments.Count > 0)
        {
            var localNameBuilder = new StringBuilder();
            var argNamespacesBuilder = new StringBuilder();
            var parametersFromBuiltInNamespaces = true;
            var nestedParamCounts = GetDataContractNameForGenericName(localName, localNameBuilder);
            foreach (CodeTypeReference typeArg in typeReference.TypeArguments)
            {
                GetDefaultName(typeArg, out var typeArgName, out var typeArgNs);
                localNameBuilder.Append(typeArgName);
                argNamespacesBuilder.Append(' ').Append(typeArgNs);
                if (parametersFromBuiltInNamespaces)
                    parametersFromBuiltInNamespaces = IsBuiltInNamespace(typeArgNs);
            }

            if (nestedParamCounts.Count > 1 || !parametersFromBuiltInNamespaces)
            {
                foreach (var count in nestedParamCounts)
                    argNamespacesBuilder.Insert(0, count.ToString(CultureInfo.InvariantCulture)).Insert(0, ' ');

                localNameBuilder.Append(GetNamespacesDigest(argNamespacesBuilder.ToString()));
            }

            localName = localNameBuilder.ToString();
        }

        localName = EncodeLocalName(localName);
        ns = GetDefaultXmlNamespace(ns);
    }

    private static bool TryGetBuiltInDataContractName(string typeName, [NotNullWhen(true)] out (XmlDictionaryString, XmlDictionaryString)? xmlName)
    {
        xmlName = null;

        if (!typeName.StartsWith("System.", StringComparison.Ordinal))
            return false;

        xmlName = typeName.AsSpan(7) switch
        {
            "Char" => (DictionaryGlobals.CharLocalName, DictionaryGlobals.SerializationNamespace),
            "Boolean" => (DictionaryGlobals.BooleanLocalName, DictionaryGlobals.SchemaNamespace),
            "SByte" => (DictionaryGlobals.SignedByteLocalName, DictionaryGlobals.SchemaNamespace),
            "Byte" => (DictionaryGlobals.UnsignedByteLocalName, DictionaryGlobals.SchemaNamespace),
            "Int16" => (DictionaryGlobals.ShortLocalName, DictionaryGlobals.SchemaNamespace),
            "UInt16" => (DictionaryGlobals.UnsignedShortLocalName, DictionaryGlobals.SchemaNamespace),
            "Int32" => (DictionaryGlobals.IntLocalName, DictionaryGlobals.SchemaNamespace),
            "UInt32" => (DictionaryGlobals.UnsignedIntLocalName, DictionaryGlobals.SchemaNamespace),
            "Int64" => (DictionaryGlobals.LongLocalName, DictionaryGlobals.SchemaNamespace),
            "UInt64" => (DictionaryGlobals.UnsignedLongLocalName, DictionaryGlobals.SchemaNamespace),
            "Single" => (DictionaryGlobals.FloatLocalName, DictionaryGlobals.SchemaNamespace),
            "Double" => (DictionaryGlobals.DoubleLocalName, DictionaryGlobals.SchemaNamespace),
            "Decimal" => (DictionaryGlobals.DecimalLocalName, DictionaryGlobals.SchemaNamespace),
            "DateTime" => (DictionaryGlobals.DateTimeLocalName, DictionaryGlobals.SchemaNamespace),
            "String" => (DictionaryGlobals.StringLocalName, DictionaryGlobals.SchemaNamespace),
            "Object" => (DictionaryGlobals.ObjectLocalName, DictionaryGlobals.SchemaNamespace),
            "TimeSpan" => (DictionaryGlobals.TimeSpanLocalName, DictionaryGlobals.SerializationNamespace),
            "Guid" => (DictionaryGlobals.GuidLocalName, DictionaryGlobals.SerializationNamespace),
            "Uri" => (DictionaryGlobals.UriLocalName, DictionaryGlobals.SerializationNamespace),
            "Xml.XmlQualifiedName" => (DictionaryGlobals.QNameLocalName, DictionaryGlobals.SchemaNamespace),
            "Enum" => (DictionaryGlobals.ObjectLocalName, DictionaryGlobals.SchemaNamespace),
            "ValueType" => (DictionaryGlobals.ObjectLocalName, DictionaryGlobals.SchemaNamespace),
            "Array" => (DictionaryGlobals.ArrayOfAnyTypeLocalName, DictionaryGlobals.ArraySchemaNamespace),
            "Xml.XmlElement" => (DictionaryGlobals.XmlElementLocalName, DictionaryGlobals.SystemXmlNamespace),
            "Xml.XmlNode[]" => (DictionaryGlobals.ArrayOfXmlNodeLocalName, DictionaryGlobals.ArrayOfXmlNodeNamespace),
            _ => null,
        };

        return xmlName != null;
    }

    internal static bool IsBuiltInNamespace(string ns)
    {
        return (ns == Globals.SchemaNamespace || ns == Globals.SerializationNamespace);
    }

    internal static string GetDefaultXmlNamespace(string? clrNs)
    {
        return new Uri(Globals.DataContractXsdBaseNamespaceUri, clrNs ?? string.Empty).AbsoluteUri;
    }

    internal static List<int> GetDataContractNameForGenericName(string typeName, StringBuilder? localName)
    {
        var nestedParamCounts = new List<int>();
        for (var startIndex = 0;;)
        {
            var endIndex = typeName.IndexOf('`', startIndex);
            if (endIndex < 0)
            {
                localName?.Append(typeName.AsSpan(startIndex));
                nestedParamCounts.Add(0);
                break;
            }

            if (localName != null)
            {
                var tempLocalName = typeName.AsSpan(startIndex, endIndex - startIndex);
                localName.Append(tempLocalName);
            }

            while ((startIndex = typeName.IndexOf('.', startIndex + 1, endIndex - startIndex - 1)) >= 0)
                nestedParamCounts.Add(0);
            startIndex = typeName.IndexOf('.', endIndex);
            if (startIndex < 0)
            {
                nestedParamCounts.Add(int.Parse(typeName.AsSpan(endIndex + 1), provider: CultureInfo.InvariantCulture));
                break;
            }

            nestedParamCounts.Add(int.Parse(typeName.AsSpan(endIndex + 1, startIndex - endIndex - 1), provider: CultureInfo.InvariantCulture));
        }

        localName?.Append("Of");
        return nestedParamCounts;
    }

    internal static void GetClrNameAndNamespace(string fullTypeName, out string localName, out string ns)
    {
        var nsEnd = fullTypeName.LastIndexOf('.');
        if (nsEnd < 0)
        {
            ns = string.Empty;
            localName = fullTypeName.Replace('+', '.');
        }
        else
        {
            ns = fullTypeName.Substring(0, nsEnd);
            localName = fullTypeName.Substring(nsEnd + 1).Replace('+', '.');
        }

        var iParam = localName.IndexOf('[');
        if (iParam >= 0)
            localName = localName.Substring(0, iParam);
    }

    private static string GetNamespacesDigest(string namespaces)
    {
        const int digestLen = 6;
        var namespaceBytes = Encoding.UTF8.GetBytes(namespaces);
        Span<byte> digestBytes = stackalloc byte[digestLen];
        ComputeHash(namespaceBytes, digestBytes);
        Span<char> digestChars = stackalloc char[24];
        Convert.TryToBase64Chars(digestBytes, digestChars, out var digestCharsLen);
        var digest = new StringBuilder();
        for (var i = 0; i < digestCharsLen; i++)
        {
            var ch = digestChars[i];
            switch (ch)
            {
                case '=':
                    break;
                case '/':
                    digest.Append("_S");
                    break;
                case '+':
                    digest.Append("_P");
                    break;
                default:
                    digest.Append(ch);
                    break;
            }
        }

        return digest.ToString();
    }

    private static void ComputeHash(byte[] namespaces, Span<byte> destination)
    {
        var blocks = (namespaces.Length + 8) / 64 + 1;

        var aa = 0x67452301U;
        var bb = 0xefcdab89;
        var cc = 0x98badcfe;
        var dd = 0x10325476U;

        for (var i = 0; i < blocks; i++)
        {
            var block = namespaces;
            var offset = i * 64;

            if (offset + 64 > namespaces.Length)
            {
                block = new byte[64];

                for (var j = offset; j < namespaces.Length; j++)
                {
                    block[j - offset] = namespaces[j];
                }

                if (offset <= namespaces.Length)
                {
                    block[namespaces.Length - offset] = 0x80;
                }

                if (i == blocks - 1)
                {
                    unchecked
                    {
                        block[56] = (byte)(namespaces.Length << 3);
                        block[57] = (byte)(namespaces.Length >> 5);
                        block[58] = (byte)(namespaces.Length >> 13);
                        block[59] = (byte)(namespaces.Length >> 21);
                    }
                }

                offset = 0;
            }

            var a = aa;
            var b = bb;
            var c = cc;
            var d = dd;

            for (var j = 0; j < 64; j++)
            {
                uint f;
                int g;
                if (j < 16)
                {
                    f = b & c | ~b & d;
                    g = j;
                }
                else if (j < 32)
                {
                    f = b & d | c & ~d;
                    g = 5 * j + 1;
                }
                else if (j < 48)
                {
                    f = b ^ c ^ d;
                    g = 3 * j + 5;
                }
                else
                {
                    f = c ^ (b | ~d);
                    g = 7 * j;
                }

                g = (g & 0x0f) * 4 + offset;

                var hold = d;
                d = c;
                c = b;

                b = unchecked(a + f + Sines[j] + BinaryPrimitives.ReadUInt32LittleEndian(block.AsSpan(g)));
                b = b << Shifts[j & 3 | j >> 2 & ~3] | b >> 32 - Shifts[j & 3 | j >> 2 & ~3];
                b = unchecked(b + c);

                a = hold;
            }

            unchecked
            {
                aa += a;
                bb += b;

                if (i < blocks - 1)
                {
                    cc += c;
                    dd += d;
                }
            }
        }

        unchecked
        {
            destination[0] = (byte)aa;
            destination[1] = (byte)(aa >> 8);
            destination[2] = (byte)(aa >> 16);
            destination[3] = (byte)(aa >> 24);
            destination[4] = (byte)bb;
            destination[5] = (byte)(bb >> 8);
        }
    }
}