using System.Collections;
using System.Runtime.Serialization;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.DotnetRuntime.Extensions;

namespace Marius.DataContracts.SourceGenerators.DataContracts;

internal sealed class KnownTypeSymbols
{
    public const string SerializableAttributeFullName = "System.SerializableAttribute";
    public const string OnSerializingAttributeFullName = "System.Runtime.Serialization.OnSerializingAttribute";
    public const string OnSerializedAttributeFullName = "System.Runtime.Serialization.OnSerializedAttribute";
    public const string OnDeserializingAttributeFullName = "System.Runtime.Serialization.OnDeserializingAttribute";
    public const string OnDeserializedAttributeFullName = "System.Runtime.Serialization.OnDeserializedAttribute";

    public const string ExtensionDataSetMethod = "set_ExtensionData";
    public const string ExtensionDataSetExplicitMethod = "System.Runtime.Serialization.IExtensibleDataObject.set_ExtensionData";
    public const string ExtensionDataObjectPropertyName = "ExtensionData";
    public const string ExtensionDataObjectFieldName = "extensionDataField";
    
    public Compilation Compilation { get; }

    public INamedTypeSymbol ObjectType => _ObjectType ??= Compilation.GetSpecialType(SpecialType.System_Object);
    private INamedTypeSymbol? _ObjectType;

    public INamedTypeSymbol VoidType => _VoidType ??= Compilation.GetSpecialType(SpecialType.System_Void);
    private INamedTypeSymbol? _VoidType;

    public INamedTypeSymbol BooleanType => _BooleanType ??= Compilation.GetSpecialType(SpecialType.System_Boolean);
    private INamedTypeSymbol? _BooleanType;

    public INamedTypeSymbol ByteType => _ByteType ??= Compilation.GetSpecialType(SpecialType.System_Byte);
    private INamedTypeSymbol? _ByteType;

    public INamedTypeSymbol CharType => _CharType ??= Compilation.GetSpecialType(SpecialType.System_Char);
    private INamedTypeSymbol? _CharType;

    public INamedTypeSymbol DateTimeType => _DateTimeType ??= Compilation.GetSpecialType(SpecialType.System_DateTime);
    private INamedTypeSymbol? _DateTimeType;

    public INamedTypeSymbol DoubleType => _DoubleType ??= Compilation.GetSpecialType(SpecialType.System_Double);
    private INamedTypeSymbol? _DoubleType;

    public INamedTypeSymbol Int16Type => _Int16Type ??= Compilation.GetSpecialType(SpecialType.System_Int16);
    private INamedTypeSymbol? _Int16Type;

    public INamedTypeSymbol Int32Type => _Int32Type ??= Compilation.GetSpecialType(SpecialType.System_Int32);
    private INamedTypeSymbol? _Int32Type;

    public INamedTypeSymbol Int64Type => _Int64Type ??= Compilation.GetSpecialType(SpecialType.System_Int64);
    private INamedTypeSymbol? _Int64Type;

    public INamedTypeSymbol SByteType => _SByteType ??= Compilation.GetSpecialType(SpecialType.System_SByte);
    private INamedTypeSymbol? _SByteType;

    public INamedTypeSymbol SingleType => _SingleType ??= Compilation.GetSpecialType(SpecialType.System_Single);
    private INamedTypeSymbol? _SingleType;

    public INamedTypeSymbol StringType => _StringType ??= Compilation.GetSpecialType(SpecialType.System_String);
    private INamedTypeSymbol? _StringType;

    public INamedTypeSymbol UInt16Type => _UInt16Type ??= Compilation.GetSpecialType(SpecialType.System_UInt16);
    private INamedTypeSymbol? _UInt16Type;

    public INamedTypeSymbol UInt32Type => _UInt32Type ??= Compilation.GetSpecialType(SpecialType.System_UInt32);
    private INamedTypeSymbol? _UInt32Type;

    public INamedTypeSymbol UInt64Type => _UInt64Type ??= Compilation.GetSpecialType(SpecialType.System_UInt64);
    private INamedTypeSymbol? _UInt64Type;

    public INamedTypeSymbol DecimalType => _DecimalType ??= Compilation.GetSpecialType(SpecialType.System_Decimal);
    private INamedTypeSymbol? _DecimalType;

    public INamedTypeSymbol EnumType => _EnumType ??= Compilation.GetSpecialType(SpecialType.System_Enum);
    private INamedTypeSymbol? _EnumType;

    public INamedTypeSymbol NullableOfType => _NullableOfType ??= Compilation.GetSpecialType(SpecialType.System_Nullable_T);
    private INamedTypeSymbol? _NullableOfType;

    public INamedTypeSymbol ValueTypeType => _ValueTypeType ??= Compilation.GetSpecialType(SpecialType.System_ValueType);
    private INamedTypeSymbol? _ValueTypeType;

    public INamedTypeSymbol ArrayType => _ArrayType ??= Compilation.GetSpecialType(SpecialType.System_Array);
    private INamedTypeSymbol? _ArrayType;

    public IArrayTypeSymbol ObjectArrayType => _ObjectArrayType ??= Compilation.CreateArrayTypeSymbol(ObjectType, rank: 1);
    private IArrayTypeSymbol? _ObjectArrayType;

    public IArrayTypeSymbol? ByteArrayType => _ByteArrayType.HasValue
        ? _ByteArrayType.Value
        : (_ByteArrayType = new Option<IArrayTypeSymbol?>(Compilation.CreateArrayTypeSymbol(Compilation.GetSpecialType(SpecialType.System_Byte), rank: 1))).Value;

    private Option<IArrayTypeSymbol?> _ByteArrayType;

    public INamedTypeSymbol? UriType => GetOrResolveType(typeof(Uri), ref _UriType);
    private Option<INamedTypeSymbol?> _UriType;

    public INamedTypeSymbol? ReflectionPointerType => GetOrResolveType(typeof(System.Reflection.Pointer), ref _ReflectionPointerType);
    private Option<INamedTypeSymbol?> _ReflectionPointerType;

    public INamedTypeSymbol? TimeSpanType => GetOrResolveType(typeof(TimeSpan), ref _TimeSpanType);
    private Option<INamedTypeSymbol?> _TimeSpanType;

    public INamedTypeSymbol? GuidType => GetOrResolveType(typeof(Guid), ref _GuidType);
    private Option<INamedTypeSymbol?> _GuidType;

    public INamedTypeSymbol? DBNullType => GetOrResolveType(typeof(DBNull), ref _DBNullType);
    private Option<INamedTypeSymbol?> _DBNullType;

    public INamedTypeSymbol? DateOnlyType => GetOrResolveType("System.DateOnly", ref _DateOnlyType);
    private Option<INamedTypeSymbol?> _DateOnlyType;

    public INamedTypeSymbol? TimeOnlyType => GetOrResolveType("System.TimeOnly", ref _TimeOnlyType);
    private Option<INamedTypeSymbol?> _TimeOnlyType;

    public INamedTypeSymbol? XmlQualifiedNameType => GetOrResolveType(typeof(XmlQualifiedName), ref _XmlQualifiedNameType);
    private Option<INamedTypeSymbol?> _XmlQualifiedNameType;

    public INamedTypeSymbol? XmlSchemaTypeType => GetOrResolveType(typeof(XmlSchemaType), ref _XmlSchemaTypeType);
    private Option<INamedTypeSymbol?> _XmlSchemaTypeType;

    public INamedTypeSymbol? XmlSchemaSetType => GetOrResolveType(typeof(XmlSchemaSet), ref _XmlSchemaSetType);
    private Option<INamedTypeSymbol?> _XmlSchemaSetType;

    public INamedTypeSymbol? XmlElementType => GetOrResolveType(typeof(XmlElement), ref _XmlElementType);
    private Option<INamedTypeSymbol?> _XmlElementType;

    public INamedTypeSymbol? XmlNodeType => GetOrResolveType(typeof(XmlNode), ref _XmlNodeType);
    private Option<INamedTypeSymbol?> _XmlNodeType;

    public IArrayTypeSymbol? XmlNodeArrayType => _XmlNodeArrayType.HasValue
        ? _XmlNodeArrayType.Value
        : (_XmlNodeArrayType = new Option<IArrayTypeSymbol?>(Compilation.CreateArrayTypeSymbol(XmlNodeType!, rank: 1))).Value;

    private Option<IArrayTypeSymbol?> _XmlNodeArrayType;

    public INamedTypeSymbol? XElementType => GetOrResolveType("System.Xml.Linq.XElement", ref _XElementType);
    private Option<INamedTypeSymbol?> _XElementType;

    public INamedTypeSymbol? IXmlSerializableType => GetOrResolveType(typeof(IXmlSerializable), ref _IXmlSerializableType);
    private Option<INamedTypeSymbol?> _IXmlSerializableType;

    public INamedTypeSymbol? ArraySegmentType => GetOrResolveType(typeof(ArraySegment<>), ref _ArraySegmentType);
    private Option<INamedTypeSymbol?> _ArraySegmentType;

    public INamedTypeSymbol? IEnumerableType => GetOrResolveType(typeof(IEnumerable), ref _IEnumerableType);
    private Option<INamedTypeSymbol?> _IEnumerableType;

    public INamedTypeSymbol? IEnumerableOfTType => GetOrResolveType(typeof(IEnumerable<>), ref _IEnumerableOfTType);
    private Option<INamedTypeSymbol?> _IEnumerableOfTType;

    public INamedTypeSymbol? IEnumeratorType => GetOrResolveType(typeof(IEnumerator), ref _IEnumeratorType);
    private Option<INamedTypeSymbol?> _IEnumeratorType;

    public INamedTypeSymbol? IEnumeratorOfType => GetOrResolveType(typeof(IEnumerator<>), ref _IEnumeratorOfType);
    private Option<INamedTypeSymbol?> _IEnumeratorOfType;

    public INamedTypeSymbol? IDictionaryOfTKeyTValueType => GetOrResolveType(typeof(IDictionary<,>), ref _IDictionaryOfTKeyTValueType);
    private Option<INamedTypeSymbol?> _IDictionaryOfTKeyTValueType;

    public INamedTypeSymbol? DictionaryOfTKeyTValueType => GetOrResolveType(typeof(Dictionary<,>), ref _DictionaryOfTKeyTValueType);
    private Option<INamedTypeSymbol?> _DictionaryOfTKeyTValueType;

    public INamedTypeSymbol? ListOfTType => GetOrResolveType(typeof(List<>), ref _ListOfTType);
    private Option<INamedTypeSymbol?> _ListOfTType;

    public INamedTypeSymbol? ArrayListType => GetOrResolveType(typeof(ArrayList), ref _ArrayListType);
    private Option<INamedTypeSymbol?> _ArrayListType;

    public INamedTypeSymbol? HashtableType => GetOrResolveType(typeof(Hashtable), ref _HashtableType);
    private Option<INamedTypeSymbol?> _HashtableType;

    public INamedTypeSymbol? IDictionaryType => GetOrResolveType(typeof(IDictionary), ref _IDictionaryType);
    private Option<INamedTypeSymbol?> _IDictionaryType;

    public INamedTypeSymbol? DictionaryOfEnumeratorType => GetOrResolveType(typeof(Dictionary<,>.Enumerator), ref _DictionaryOfEnumeratorType);
    private Option<INamedTypeSymbol?> _DictionaryOfEnumeratorType;

    public INamedTypeSymbol? IListOfTType => GetOrResolveType(typeof(IList<>), ref _IListOfTType);
    private Option<INamedTypeSymbol?> _IListOfTType;

    public INamedTypeSymbol? ICollectionType => GetOrResolveType(typeof(ICollection), ref _ICollectionType);
    private Option<INamedTypeSymbol?> _ICollectionType;

    public INamedTypeSymbol? ICollectionOfTType => GetOrResolveType(typeof(ICollection<>), ref _ICollectionOfTType);
    private Option<INamedTypeSymbol?> _ICollectionOfTType;

    public INamedTypeSymbol? IListType => GetOrResolveType(typeof(IList), ref _IListType);
    private Option<INamedTypeSymbol?> _IListType;

    public INamedTypeSymbol? KeyValuePairOfType => GetOrResolveType(typeof(KeyValuePair<,>), ref _KeyValuePairOfType);
    private Option<INamedTypeSymbol?> _KeyValuePairOfType;

    public INamedTypeSymbol? KeyValueOfType => GetOrResolveType("Marius.DataContracts.Runtime.KeyValue`2", ref _KeyValueOfType);
    private Option<INamedTypeSymbol?> _KeyValueOfType;

    public INamedTypeSymbol? ISerializableType => GetOrResolveType(typeof(ISerializable), ref _ISerializableType);
    private Option<INamedTypeSymbol?> _ISerializableType;

    public INamedTypeSymbol? IExtensibleDataObjectType => GetOrResolveType(typeof(IExtensibleDataObject), ref _IExtensibleDataObjectType);
    private Option<INamedTypeSymbol?> _IExtensibleDataObjectType;

    public INamedTypeSymbol? ExtensionDataObjectType => GetOrResolveType(typeof(ExtensionDataObject), ref _ExtensionDataObjectType);
    private Option<INamedTypeSymbol?> _ExtensionDataObjectType;

    public INamedTypeSymbol? StreamingContextType => GetOrResolveType(typeof(StreamingContext), ref _StreamingContextType);
    private Option<INamedTypeSymbol?> _StreamingContextType;

    public INamedTypeSymbol? IDeserializationCallbackType => GetOrResolveType(typeof(IDeserializationCallback), ref _IDeserializationCallbackType);
    private Option<INamedTypeSymbol?> _IDeserializationCallbackType;

#pragma warning disable SYSLIB0050
    public INamedTypeSymbol? IObjectReferenceType => GetOrResolveType(typeof(IObjectReference), ref _IObjectReferenceType);
    private Option<INamedTypeSymbol?> _IObjectReferenceType;
#pragma warning restore SYSLIB0050

    public INamedTypeSymbol? SerializationInfoType => GetOrResolveType(typeof(SerializationInfo), ref _SerializationInfoType);
    private Option<INamedTypeSymbol?> _SerializationInfoType;

    public INamedTypeSymbol?[] KnownCollectionInterfaces => _KnownCollectionInterfaces ??=
    [
        IDictionaryOfTKeyTValueType,
        IDictionaryType,
        IListOfTType,
        ICollectionOfTType,
        IListType,
        IEnumerableOfTType,
        ICollectionType,
        IEnumerableType,
    ];

    private INamedTypeSymbol?[]? _KnownCollectionInterfaces;
    
    public static HasAttributeCheck HasOnSerializingAttribute = new HasAttributeCheck
    {
        FullAttributeName = OnSerializingAttributeFullName,
        HasAttribute = static symbol =>
        {
            return symbol.GetAttributes().Any(attr => attr.AttributeClass?.ToDisplayString() == OnSerializingAttributeFullName);
        },
    };

    public static HasAttributeCheck HasOnSerializedAttribute = new HasAttributeCheck
    {
        FullAttributeName = OnSerializedAttributeFullName,
        HasAttribute = static symbol =>
        {
            return symbol.GetAttributes().Any(attr => attr.AttributeClass?.ToDisplayString() == OnSerializedAttributeFullName);
        },
    };

    public static HasAttributeCheck HasOnDeserializingAttribute = new HasAttributeCheck
    {
        FullAttributeName = OnDeserializingAttributeFullName,
        HasAttribute = static symbol =>
        {
            return symbol.GetAttributes().Any(attr => attr.AttributeClass?.ToDisplayString() == OnDeserializingAttributeFullName);
        },
    };

    public static HasAttributeCheck HasOnDeserializedAttribute = new HasAttributeCheck
    {
        FullAttributeName = OnDeserializedAttributeFullName,
        HasAttribute = static symbol =>
        {
            return symbol.GetAttributes().Any(attr => attr.AttributeClass?.ToDisplayString() == OnDeserializedAttributeFullName);
        },
    };

    public KnownTypeSymbols(Compilation compilation)
    {
        Compilation = compilation;
    }

    public bool IsIXmlSerializable(ITypeSymbol type)
    {
        var cmp = SymbolEqualityComparer.Default;
        var interfaceType = IXmlSerializableType;
        return interfaceType != null && (cmp.Equals(type, interfaceType) || type.AllInterfaces.Any(i => cmp.Equals(i, interfaceType)));
    }

    public bool IsArraySegment(ITypeSymbol type)
    {
        var cmp = SymbolEqualityComparer.Default;
        var arraySegmentType = ArraySegmentType;
        if (cmp.Equals(type, arraySegmentType))
            return true;

        return type is INamedTypeSymbol namedType && namedType.IsGenericType && cmp.Equals(namedType.ConstructedFrom, arraySegmentType);
    }

    public bool IsIEnumerable(ITypeSymbol type)
    {
        var cmp = SymbolEqualityComparer.Default;
        var interfaceType = IEnumerableType;
        return interfaceType != null && (cmp.Equals(type, interfaceType) || type.AllInterfaces.Any(i => cmp.Equals(i, interfaceType)));
    }

    public bool IsISerializable(ITypeSymbol type)
    {
        var cmp = SymbolEqualityComparer.Default;
        var interfaceType = ISerializableType;
        return interfaceType != null && (cmp.Equals(type, interfaceType) || type.AllInterfaces.Any(i => cmp.Equals(i, interfaceType)));
    }

    public bool IsIExtensibleDataObject(ITypeSymbol type)
    {
        var cmp = SymbolEqualityComparer.Default;
        var interfaceType = IExtensibleDataObjectType;
        return interfaceType != null && (cmp.Equals(type, interfaceType) || type.AllInterfaces.Any(i => cmp.Equals(i, interfaceType)));
    }

    public bool IsNullable(ITypeSymbol type)
    {
        return SymbolEqualityComparer.Default.Equals(type, NullableOfType);
    }

    public bool IsDbNull(ITypeSymbol type)
    {
        return SymbolEqualityComparer.Default.Equals(type, DBNullType);
    }

    public bool IsSerializable(ITypeSymbol type)
    {
        var attributes = type.GetAttributes();
        foreach (var attribute in attributes)
        {
            if (attribute.AttributeClass?.ToDisplayString() == SerializableAttributeFullName)
                return true;
        }

        return false;
    }

    public bool IsExtensionDataObject(ITypeSymbol type)
    {
        return SymbolEqualityComparer.Default.Equals(type, ExtensionDataObjectType);
    }

    public IMethodSymbol GetExtensionDataSetExplicitMethod()
    {
        return IExtensibleDataObjectType?.GetMembers(ExtensionDataSetMethod).OfType<IMethodSymbol>().FirstOrDefault() ?? throw new InvalidOperationException();
    }

    public IMethodSymbol GetEnumeratorCurrentGetMethod()
    {
        return IEnumeratorOfType?.GetMembers("Current").OfType<IPropertySymbol>().FirstOrDefault()?.GetMethod ?? throw new InvalidOperationException();
    }

    public bool IsStreamingContext(ITypeSymbol type)
    {
        return SymbolEqualityComparer.Default.Equals(type, StreamingContextType);
    }

    private INamedTypeSymbol? GetOrResolveType(Type type, ref Option<INamedTypeSymbol?> field)
        => GetOrResolveType(type.FullName!, ref field);

    private INamedTypeSymbol? GetOrResolveType(string fullyQualifiedName, ref Option<INamedTypeSymbol?> field)
    {
        if (field.HasValue)
        {
            return field.Value;
        }

        var type = Compilation.GetBestTypeByMetadataName(fullyQualifiedName);
        field = new Option<INamedTypeSymbol?>(type);
        return type;
    }

    private readonly struct Option<T>
    {
        public readonly bool HasValue;
        public readonly T Value;

        public Option(T value)
        {
            HasValue = true;
            Value = value;
        }
    }

    public struct HasAttributeCheck
    {
        public string FullAttributeName { get; init; }
        public Func<ISymbol, bool> HasAttribute { get; init; }
    }
}