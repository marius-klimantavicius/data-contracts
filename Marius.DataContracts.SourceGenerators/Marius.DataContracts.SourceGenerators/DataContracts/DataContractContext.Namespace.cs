using System.Buffers.Binary;
using System.Runtime.Serialization;
using System.Text;
using Microsoft.CodeAnalysis;

namespace Marius.DataContracts.SourceGenerators.DataContracts;

internal partial class DataContractContext
{
    public const string CollectionsNamespace = "http://schemas.microsoft.com/2003/10/Serialization/Arrays";
    
    private static Uri? _dataContractXsdBaseNamespaceUri;
    internal static Uri DataContractXsdBaseNamespaceUri => _dataContractXsdBaseNamespaceUri ??= new Uri(DataContractXsdBaseNamespace);

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

    private static string GetDefaultDataContractNamespace(ITypeSymbol type)
    {
        var clrNs = GetNamespace(type) ?? string.Empty;
        var ns =
            GetGlobalDataContractNamespace(clrNs, type.ContainingModule) ??
            GetGlobalDataContractNamespace(clrNs, type.ContainingAssembly);

        if (ns == null)
            ns = GetDefaultXmlNamespace(type);
        else
            CheckExplicitDataContractNamespaceUri(ns, type);

        return ns;
    }

    internal static string GetDefaultXmlNamespace(ITypeSymbol type)
    {
        if (type.TypeKind == TypeKind.TypeParameter)
            return "{ns}";

        return GetDefaultXmlNamespace(GetNamespace(type));
    }

    internal static string GetDefaultXmlNamespace(string? clrNs)
    {
        return new Uri(DataContractXsdBaseNamespaceUri, clrNs ?? string.Empty).AbsoluteUri;
    }

    private static void CheckExplicitDataContractNamespaceUri(string dataContractNs, ITypeSymbol type)
    {
        if (dataContractNs.Length > 0)
        {
            var trimmedNs = dataContractNs.Trim();
            // Code similar to XmlConvert.ToUri (string.Empty is a valid uri but not "   ")
            if (trimmedNs.Length == 0 || trimmedNs.Contains("##", StringComparison.Ordinal)) 
                ThrowInvalidDataContractException(SR.Format(SR.DataContractNamespaceIsNotValid, dataContractNs));

            dataContractNs = trimmedNs;
        }

        if (Uri.TryCreate(dataContractNs, UriKind.RelativeOrAbsolute, out var uri))
        {
            Span<char> formatted = stackalloc char[SerializationNamespace.Length];
            if (uri.TryFormat(formatted, out var charsWritten) &&
                charsWritten == SerializationNamespace.Length &&
                formatted.SequenceEqual(SerializationNamespace))
            {
                ThrowInvalidDataContractException(SR.Format(SR.DataContractNamespaceReserved, SerializationNamespace));
            }
        }
        else
        {
            ThrowInvalidDataContractException(SR.Format(SR.DataContractNamespaceIsNotValid, dataContractNs));
        }
    }
    
    private static string? GetNamespace(ITypeSymbol type)
    {
        return type.ContainingNamespace?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat.WithGlobalNamespaceStyle(SymbolDisplayGlobalNamespaceStyle.Omitted)) is { Length: > 0 } ns ? ns : null;
    }

    private static string? GetGlobalDataContractNamespace(string clrNs, ISymbol symbol)
    {
        var dataContractNs = default(string?);
        var attrs = symbol.GetAttributes();
        foreach (var attribute in attrs)
        {
            if (attribute.AttributeClass?.ToDisplayString() != ContractNamespaceAttributeFullName)
                continue;

            var nsAttribute = Create(attribute);
            foreach (var kvp in attribute.NamedArguments)
            {
                if (kvp.Key == nameof(ContractNamespaceAttribute.ClrNamespace))
                    nsAttribute.ClrNamespace = (string)kvp.Value.Value!;
            }

            var clrNsInAttribute = nsAttribute.ClrNamespace ?? string.Empty;
            if (clrNsInAttribute == clrNs)
            {
                if (nsAttribute.ContractNamespace == null)
                    throw new InvalidDataContractException(SR.Format(SR.InvalidGlobalDataContractNamespace, clrNs));
                if (dataContractNs != null)
                    throw new InvalidDataContractException(SR.Format(SR.DataContractNamespaceAlreadySet, dataContractNs, nsAttribute.ContractNamespace, clrNs));

                dataContractNs = nsAttribute.ContractNamespace;
            }
        }

        return dataContractNs;

        static ContractNamespaceAttribute Create(AttributeData attribute)
        {
            if (attribute.ConstructorArguments.Length == 1 && attribute.ConstructorArguments[0].Value is string elementName)
                return new ContractNamespaceAttribute(elementName);

            throw new NotSupportedException();
        }
    }

    internal static bool IsBuiltInNamespace(string ns)
    {
        return ns == SchemaNamespace || ns == SerializationNamespace;
    }
    
    internal static string GetCollectionNamespace(string elementNs)
    {
        return IsBuiltInNamespace(elementNs) ? CollectionsNamespace : elementNs;
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