using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.Serialization;
using System.Text;
using System.Xml;
using Microsoft.CodeAnalysis;

namespace Marius.DataContracts.SourceGenerators.DataContracts;

internal partial class DataContractContext
{
    public XmlQualifiedName GetXmlName(ITypeSymbol type)
    {
        return GetXmlName(type, out _);
    }

    public XmlQualifiedName GetXmlName(ITypeSymbol type, out bool hasDataContract)
    {
        return GetXmlName(type, new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default), out hasDataContract);
    }

    internal static bool TryGetDCAttribute(ISymbol symbol, [NotNullWhen(true)] out DataContractAttribute? dataContractAttribute)
    {
        dataContractAttribute = null;

        var attrs = symbol.GetAttributes();
        foreach (var attribute in attrs)
        {
            if (attribute.AttributeClass?.ToDisplayString() != DataContractAttributeFullName)
                continue;

            var value = new DataContractAttribute();
            foreach (var kvp in attribute.NamedArguments)
            {
                if (kvp.Key == nameof(DataContractAttribute.IsReference))
                    value.IsReference = (bool)kvp.Value.Value!;
                else if (kvp.Key == nameof(DataContractAttribute.Name))
                    value.Name = (string)kvp.Value.Value!;
                else if (kvp.Key == nameof(DataContractAttribute.Namespace))
                    value.Namespace = (string)kvp.Value.Value!;
            }

            dataContractAttribute = value;
            return true;
        }

        return false;
    }

    private XmlQualifiedName GetXmlName(ITypeSymbol type, HashSet<ITypeSymbol> previousCollectionTypes, out bool hasDataContract)
    {
        type = UnwrapRedundantNullableType(type);
        if (TryGetBuiltInXmlAndArrayTypeXmlName(type, previousCollectionTypes, out var xmlName))
        {
            hasDataContract = false;
        }
        else
        {
            if (TryGetDCAttribute(type, out var dataContractAttribute))
            {
                xmlName = GetDCTypeXmlName(type, dataContractAttribute);
                hasDataContract = true;
            }
            else
            {
                xmlName = GetNonDCTypeXmlName(type, previousCollectionTypes);
                hasDataContract = false;
            }
        }

        return xmlName;
    }

    private XmlQualifiedName GetDCTypeXmlName(ITypeSymbol type, DataContractAttribute dataContractAttribute)
    {
        string? name, ns;
        if (dataContractAttribute.IsNameSetExplicitly)
        {
            name = dataContractAttribute.Name;
            if (string.IsNullOrEmpty(name))
                throw new InvalidDataContractException(SR.Format(SR.InvalidDataContractName, GetClrTypeFullName(type)));

            if (type is INamedTypeSymbol namedType && namedType.IsGenericType && !SymbolEqualityComparer.Default.Equals(namedType, namedType.ConstructedFrom))
                name = ExpandGenericParameters(name, namedType);

            name = EncodeLocalName(name);
        }
        else
        {
            name = GetDefaultXmlLocalName(type);
        }

        if (dataContractAttribute.IsNamespaceSetExplicitly)
        {
            ns = dataContractAttribute.Namespace;
            if (ns == null)
                throw new InvalidDataContractException(SR.Format(SR.InvalidDataContractNamespace, GetClrTypeFullName(type)));

            CheckExplicitDataContractNamespaceUri(ns, type);
        }
        else
        {
            ns = GetDefaultDataContractNamespace(type);
        }

        return CreateQualifiedName(name, ns);
    }

    private XmlQualifiedName GetNonDCTypeXmlName(ITypeSymbol type, HashSet<ITypeSymbol> previousCollectionTypes)
    {
        string? ns;

        if (CollectionDataContract.IsCollection(this, type, out var itemType))
        {
            ValidatePreviousCollectionTypes(type, itemType, previousCollectionTypes);
            return GetCollectionXmlName(type, itemType, previousCollectionTypes, out _);
        }

        var name = GetDefaultXmlLocalName(type);

        // ensures that ContractNamespaceAttribute is honored when used with non-attributed types
        if (IsNonAttributedTypeValidForSerialization(type))
            ns = GetDefaultDataContractNamespace(type);
        else
            ns = GetDefaultXmlNamespace(type);

        return CreateQualifiedName(name, ns);
    }

    internal XmlQualifiedName GetDefaultXmlName(ITypeSymbol type)
    {
        return CreateQualifiedName(GetDefaultXmlLocalName(type), GetDefaultXmlNamespace(type));
    }

    private string GetDefaultXmlLocalName(ITypeSymbol type)
    {
        if (type is ITypeParameterSymbol typeParameter)
            return "{" + typeParameter.Ordinal + "}";

        string? arrayPrefix = null;
        if (type is IArrayTypeSymbol)
            arrayPrefix = GetArrayPrefix(ref type);

        var typeName = type.ToDisplayString(WithoutNamespaceOrTypeParametersFormat);
        if (arrayPrefix != null)
            typeName = arrayPrefix + typeName;

        if (type is INamedTypeSymbol namedType && namedType.IsGenericType)
        {
            typeName = $"{typeName}`{namedType.TypeArguments.Length}";

            var localName = new StringBuilder();
            var namespaces = new StringBuilder();
            var parametersFromBuiltInNamespaces = true;
            var nestedParamCounts = GetDataContractNameForGenericName(typeName, localName);
            var isTypeOpenGeneric = SymbolEqualityComparer.Default.Equals(namedType, namedType.ConstructedFrom);
            var genParams = namedType.TypeArguments;
            for (var i = 0; i < genParams.Length; i++)
            {
                var genParam = genParams[i];
                if (isTypeOpenGeneric)
                {
                    localName.Append('{').Append(i).Append('}');
                }
                else
                {
                    var qname = GetXmlName(genParam);
                    localName.Append(qname.Name);
                    namespaces.Append(' ').Append(qname.Namespace);
                    if (parametersFromBuiltInNamespaces)
                        parametersFromBuiltInNamespaces = IsBuiltInNamespace(qname.Namespace);
                }
            }

            if (isTypeOpenGeneric)
            {
                localName.Append("{#}");
            }
            else if (nestedParamCounts.Count > 1 || !parametersFromBuiltInNamespaces)
            {
                foreach (var count in nestedParamCounts)
                    namespaces.Insert(0, count.ToString(CultureInfo.InvariantCulture)).Insert(0, " ");
                localName.Append(GetNamespacesDigest(namespaces.ToString()));
            }

            typeName = localName.ToString();
        }

        return EncodeLocalName(typeName);
    }

    private string GetArrayPrefix(ref ITypeSymbol itemType)
    {
        var arrayOfPrefix = string.Empty;
        while (itemType is IArrayTypeSymbol arrayType)
        {
            if (GetBuiltInDataContract(itemType) != null)
                break;

            arrayOfPrefix += ArrayPrefix;
            itemType = arrayType.ElementType;
        }

        return arrayOfPrefix;
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
                nestedParamCounts.Add(int.Parse(typeName.Substring(endIndex + 1), provider: CultureInfo.InvariantCulture));
                break;
            }

            nestedParamCounts.Add(int.Parse(typeName.Substring(endIndex + 1, startIndex - endIndex - 1), provider: CultureInfo.InvariantCulture));
        }

        localName?.Append("Of");
        return nestedParamCounts;
    }

    internal static XmlQualifiedName CreateQualifiedName(string localName, string ns)
    {
        return new XmlQualifiedName(localName, ns);
    }

    private bool TryGetBuiltInXmlAndArrayTypeXmlName(ITypeSymbol type, HashSet<ITypeSymbol> previousCollectionTypes, [NotNullWhen(true)] out XmlQualifiedName? xmlName)
    {
        xmlName = null;

        var builtInContract = GetBuiltInDataContract(type);
        if (builtInContract != null)
        {
            xmlName = builtInContract.XmlName;
        }
        else if (KnownSymbols.IsIXmlSerializable(type))
        {
            throw new InvalidDataContractException($"IXmlSerializable is not supported, type: {GetClrTypeFullName(type)}");
        }
        else if (type is IArrayTypeSymbol arrayType)
        {
            var itemType = arrayType.ElementType;
            ValidatePreviousCollectionTypes(type, itemType, previousCollectionTypes);
            xmlName = GetCollectionXmlName(type, itemType, previousCollectionTypes, out _);
        }

        return xmlName != null;
    }

    private static void ValidatePreviousCollectionTypes(ITypeSymbol collectionType, ITypeSymbol itemType, HashSet<ITypeSymbol> previousCollectionTypes)
    {
        previousCollectionTypes.Add(collectionType);
        while (itemType is IArrayTypeSymbol arrayType)
        {
            itemType = arrayType.ElementType;
        }

        // Do a breadth first traversal of the generic type tree to
        // produce the closure of all generic argument types and
        // check that none of these is in the previousCollectionTypes
        var itemTypeClosure = new List<ITypeSymbol>();
        var itemTypeQueue = new Queue<ITypeSymbol>();

        itemTypeQueue.Enqueue(itemType);
        itemTypeClosure.Add(itemType);

        while (itemTypeQueue.Count > 0)
        {
            itemType = itemTypeQueue.Dequeue();
            if (previousCollectionTypes.Contains(itemType))
                throw new InvalidDataContractException(SR.Format(SR.RecursiveCollectionType, GetClrTypeFullName(itemType)));

            if (itemType is INamedTypeSymbol namedType && namedType.IsGenericType)
            {
                foreach (var argType in namedType.TypeArguments)
                {
                    if (!itemTypeClosure.Contains(argType))
                    {
                        itemTypeQueue.Enqueue(argType);
                        itemTypeClosure.Add(argType);
                    }
                }
            }
        }
    }

    private static bool IsAsciiLocalName(string localName)
    {
        if (localName.Length == 0)
            return false;
        if (!IsAsciiLetter(localName[0]))
            return false;

        for (var i = 1; i < localName.Length; i++)
        {
            var ch = localName[i];
            if (!IsAsciiLetterOrDigit(ch))
                return false;
        }

        return true;

        static bool IsAsciiLetter(char c) => (uint)((c | 0x20) - 'a') <= 'z' - 'a';
        static bool IsAsciiLetterOrDigit(char c) => IsAsciiLetter(c) | IsBetween(c, '0', '9');
        static bool IsBetween(char c, char minInclusive, char maxInclusive) => (uint)(c - minInclusive) <= (uint)(maxInclusive - minInclusive);
    }

    internal static string EncodeLocalName(string localName)
    {
        if (IsAsciiLocalName(localName))
            return localName;

        if (IsValidNCName(localName))
            return localName;

        return XmlConvert.EncodeLocalName(localName)!;
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

    private string ExpandGenericParameters(string format, INamedTypeSymbol type)
    {
        var genericNameProviderForType = new GenericNameProvider(this, type);
        return ExpandGenericParameters(format, genericNameProviderForType);
    }

    internal static string ExpandGenericParameters(string format, IGenericNameProvider genericNameProvider)
    {
        string? digest = null;
        var typeName = new StringBuilder();
        var nestedParameterCounts = genericNameProvider.GetNestedParameterCounts();
        for (var i = 0; i < format.Length; i++)
        {
            var ch = format[i];
            if (ch == '{')
            {
                i++;
                var start = i;
                for (; i < format.Length; i++)
                    if (format[i] == '}')
                        break;

                if (i == format.Length)
                    throw new InvalidDataContractException(SR.Format(SR.GenericNameBraceMismatch, format, genericNameProvider.GetGenericTypeName()));

                if (format[start] == '#' && i == start + 1)
                {
                    if (nestedParameterCounts.Count > 1 || !genericNameProvider.ParametersFromBuiltInNamespaces)
                    {
                        if (digest == null)
                        {
                            var namespaces = new StringBuilder(genericNameProvider.GetNamespaces());
                            foreach (var count in nestedParameterCounts)
                                namespaces.Insert(0, count.ToString(CultureInfo.InvariantCulture)).Insert(0, " ");
                            digest = GetNamespacesDigest(namespaces.ToString());
                        }

                        typeName.Append(digest);
                    }
                }
                else
                {
                    if (!int.TryParse(format.Substring(start, i - start), out var paramIndex) || paramIndex < 0 || paramIndex >= genericNameProvider.GetParameterCount())
                        throw new InvalidDataContractException(SR.Format(SR.GenericParameterNotValid, format.Substring(start, i - start), genericNameProvider.GetGenericTypeName(), genericNameProvider.GetParameterCount() - 1));

                    typeName.Append(genericNameProvider.GetParameterName(paramIndex));
                }
            }
            else
                typeName.Append(ch);
        }

        return typeName.ToString();
    }

    internal XmlQualifiedName GetCollectionXmlName(ITypeSymbol type, ITypeSymbol itemType, out CollectionDataContractAttribute? collectionContractAttribute)
    {
        return GetCollectionXmlName(type, itemType, new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default), out collectionContractAttribute);
    }

    internal XmlQualifiedName GetCollectionXmlName(ITypeSymbol type, ITypeSymbol itemType, HashSet<ITypeSymbol> previousCollectionTypes, out CollectionDataContractAttribute? collectionContractAttribute)
    {
        string? name, ns;
        collectionContractAttribute = GetCollectionDataContractAttribute(type);
        if (collectionContractAttribute != null)
        {
            if (collectionContractAttribute.IsNameSetExplicitly)
            {
                name = collectionContractAttribute.Name;
                if (string.IsNullOrEmpty(name))
                    throw new InvalidDataContractException(SR.Format(SR.InvalidCollectionContractName, GetClrTypeFullName(type)));

                if (type is INamedTypeSymbol namedType && !SymbolEqualityComparer.Default.Equals(namedType, namedType.ConstructedFrom))
                    name = ExpandGenericParameters(name, namedType);

                name = EncodeLocalName(name);
            }
            else
            {
                name = GetDefaultXmlLocalName(type);
            }

            if (collectionContractAttribute.IsNamespaceSetExplicitly)
            {
                ns = collectionContractAttribute.Namespace;
                if (ns == null)
                    throw new InvalidDataContractException(SR.Format(SR.InvalidCollectionContractNamespace, GetClrTypeFullName(type)));

                CheckExplicitDataContractNamespaceUri(ns, type);
            }
            else
            {
                ns = GetDefaultDataContractNamespace(type);
            }
        }
        else
        {
            collectionContractAttribute = null;
            var arrayOfPrefix = ArrayPrefix + GetArrayPrefix(ref itemType);
            var elementXmlName = GetXmlName(itemType, previousCollectionTypes, out _);
            name = arrayOfPrefix + elementXmlName.Name;
            ns = GetCollectionNamespace(elementXmlName.Namespace);
        }

        return CreateQualifiedName(name, ns);
    }
}