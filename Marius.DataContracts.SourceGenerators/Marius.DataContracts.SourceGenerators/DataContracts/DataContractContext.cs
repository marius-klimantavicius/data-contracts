using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using System.Xml.Serialization;
using Microsoft.CodeAnalysis;

namespace Marius.DataContracts.SourceGenerators.DataContracts;

internal partial class DataContractContext
{
    private const string DataContractAttributeFullName = "System.Runtime.Serialization.DataContractAttribute";
    private const string ContractNamespaceAttributeFullName = "System.Runtime.Serialization.ContractNamespaceAttribute";
    private const string CollectionDataContractAttributeFullName = "System.Runtime.Serialization.CollectionDataContractAttribute";
    private const string DataMemberAttributeFullName = "System.Runtime.Serialization.DataMemberAttribute";
    private const string IgnoreDataMemberAttributeFullName = "System.Runtime.Serialization.IgnoreDataMemberAttribute";
    private const string OptionalFieldAttributeFullName = "System.Runtime.Serialization.OptionalFieldAttribute";
    private const string FlagsAttributeFullName = "System.FlagsAttribute";
    private const string EnumMemberAttributeFullName = "System.Runtime.Serialization.EnumMemberAttribute";
    private const string KnownTypeAttributeFullName = "System.Runtime.Serialization.KnownTypeAttribute";
    private const string XmlRootAttributeFullName = "System.Xml.Serialization.XmlRootAttribute";
    private const string XmlSchemaProviderAttributeFullName = "System.Xml.Serialization.XmlSchemaProviderAttribute";

    public const string DataContractXsdBaseNamespace = "http://schemas.datacontract.org/2004/07/";
    public const string DataContractXmlNamespace = DataContractXsdBaseNamespace + "System.Xml";
    public const string SchemaInstanceNamespace = "http://www.w3.org/2001/XMLSchema-instance";
    public const string SchemaNamespace = "http://www.w3.org/2001/XMLSchema";
    public const string SerializationNamespace = "http://schemas.microsoft.com/2003/10/Serialization/";

    public const string ArrayPrefix = "ArrayOf";

    private static readonly SymbolDisplayFormat DisplayFormat = new SymbolDisplayFormat(
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
        miscellaneousOptions: SymbolDisplayMiscellaneousOptions.ExpandNullable
    );

    private static readonly SymbolDisplayFormat WithoutNamespaceOrTypeParametersFormat = new SymbolDisplayFormat(
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypes,
        genericsOptions: SymbolDisplayGenericsOptions.None,
        miscellaneousOptions: SymbolDisplayMiscellaneousOptions.ExpandNullable
    );

    private readonly KnownTypeSymbols _knownSymbols;

    private readonly object _cacheLock = new object();
    private readonly object _createDataContractLock = new object();

    private readonly ConcurrentDictionary<ISymbol, int> _symbolToIdCache = new ConcurrentDictionary<ISymbol, int>(SymbolEqualityComparer.Default);
    private readonly ConcurrentDictionary<ITypeSymbol, DataContract?> _typeToBuiltInContract = new ConcurrentDictionary<ITypeSymbol, DataContract?>(SymbolEqualityComparer.Default);

    private DataContract?[] _dataContractCache = [];
    private int _dataContractId = 1;

    public DataContract?[] DataContracts => _dataContractCache;

    public KnownTypeSymbols KnownSymbols => _knownSymbols;

    public DataContractContext(KnownTypeSymbols knownSymbols)
    {
        _knownSymbols = knownSymbols;
    }

    internal DataContract GetDataContract(ITypeSymbol type)
    {
        var id = GetId(type);
        var dataContract = GetDataContractSkipValidation(id, type);
        return dataContract.GetValidContract();
    }

    internal DataContract GetGetOnlyCollectionDataContract(ITypeSymbol type)
    {
        var id = GetId(type);
        var dataContract = GetGetOnlyCollectionDataContractSkipValidation(id, type);
        dataContract = dataContract.GetValidContract();
        if (dataContract is ClassDataContract)
            throw new SerializationException(SR.Format(SR.ErrorDeserializing, SR.Format(SR.ErrorTypeInfo, GetClrTypeFullName(dataContract.UnderlyingType)), SR.Format(SR.NoSetMethodForProperty, string.Empty, string.Empty)));

        return dataContract;
    }

    internal DataContract GetOrAddDataContract(ITypeSymbol type, Func<DataContract> factory)
    {
        var id = GetId(type);
        return _dataContractCache[id] ??= factory();
    }

    internal int GetId(ITypeSymbol symbol)
    {
        symbol = UnwrapNullableType(symbol);
        // ReSharper disable once InconsistentlySynchronizedField
        if (_symbolToIdCache.TryGetValue(symbol, out var idFromSymbol))
            return idFromSymbol;

        try
        {
            lock (_cacheLock)
            {
                return _symbolToIdCache.GetOrAdd(symbol, _ =>
                {
                    var nextId = _dataContractId++;
                    if (nextId >= _dataContractCache.Length)
                    {
                        var newSize = nextId < int.MaxValue / 2 ? nextId * 2 : int.MaxValue;
                        if (newSize <= nextId)
                            throw new SerializationException(SR.DataContractCacheOverflow);

                        Array.Resize(ref _dataContractCache, newSize);
                    }

                    return nextId;
                });
            }
        }
        catch (Exception ex) when (!ExceptionUtility.IsFatal(ex))
        {
            throw new Exception(ex.Message, ex);
        }
    }

    internal DataContract GetDataContractSkipValidation(int id, ITypeSymbol type)
    {
        var dataContract = _dataContractCache[id];
        if (dataContract == null)
            dataContract = CreateDataContract(id, type);
        else
            return dataContract.GetValidContract(verifyConstructor: true);

        return dataContract;
    }

    private DataContract CreateDataContract(int id, ITypeSymbol type)
    {
        var dataContract = _dataContractCache[id];
        if (dataContract == null)
        {
            lock (_createDataContractLock)
            {
                dataContract = _dataContractCache[id];
                if (dataContract == null)
                {
                    dataContract = CreateDataContract(type);
                    AssignDataContractToId(dataContract, id);
                }
            }
        }

        return dataContract;
    }

    internal DataContract GetGetOnlyCollectionDataContractSkipValidation(int id, ITypeSymbol type)
    {
        var dataContract = _dataContractCache[id];
        if (dataContract == null)
            return CreateGetOnlyCollectionDataContract(id, type);

        return dataContract;
    }

    private DataContract CreateGetOnlyCollectionDataContract(int id, ITypeSymbol type)
    {
        DataContract? dataContract = null;
        lock (_createDataContractLock)
        {
            dataContract = _dataContractCache[id];
            if (dataContract == null)
            {
                type = UnwrapNullableType(type);
                //type = GetDataContractAdapterType(type);
                if (!CollectionDataContract.TryCreateGetOnlyCollectionDataContract(this, type, out dataContract))
                    ThrowInvalidDataContractException(SR.Format(SR.TypeNotSerializable, type));

                AssignDataContractToId(dataContract, id);
            }
        }

        return dataContract;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void AssignDataContractToId(DataContract dataContract, int id)
    {
        lock (_cacheLock)
        {
            _dataContractCache[id] = dataContract;
        }
    }

    private DataContract CreateDataContract(ITypeSymbol type)
    {
        type = UnwrapNullableType(type);
        var originalType = type;
        var dataContract = GetBuiltInDataContract(type);
        if (dataContract == null)
        {
            if (type is IArrayTypeSymbol arrayType)
            {
                dataContract = new CollectionDataContract(this, arrayType);
            }
            else if (type is INamedTypeSymbol namedType && type.TypeKind == TypeKind.Enum)
            {
                dataContract = new EnumDataContract(this, namedType);
            }
            else if (type.TypeKind == TypeKind.TypeParameter)
            {
                dataContract = new GenericParameterDataContract(this, type);
            }
            else if (_knownSymbols.IsIXmlSerializable(type))
            {
                dataContract = new XmlDataContract(this, type);
            }
            else
            {
                if (type.Kind == SymbolKind.PointerType)
                    type = _knownSymbols.ReflectionPointerType!;

                if (!CollectionDataContract.TryCreate(this, type, out dataContract))
                {
                    if (!HasDataContractAttribute(type) && !IsNonAttributedTypeValidForSerialization(type))
                        ThrowInvalidDataContractException(SR.Format(SR.TypeNotSerializable, type.ToDisplayString()));

                    dataContract = new ClassDataContract(this, type);
                    if (!SymbolEqualityComparer.Default.Equals(type, originalType))
                    {
                        var originalDataContract = new ClassDataContract(this, originalType);
                        if (dataContract.XmlName != originalDataContract.XmlName) // for non-DC types, type adapters will not have the same xml name (contract name).
                            dataContract.XmlName = originalDataContract.XmlName;
                    }
                }
            }
        }

        return dataContract;
    }

    internal DataContract? GetBuiltInDataContract(ITypeSymbol type)
    {
        var contract = default(DataContract?);
        if (type.TypeKind == TypeKind.Interface && !CollectionDataContract.IsCollectionInterface(KnownSymbols, type))
        {
            contract = _typeToBuiltInContract.GetOrAdd(type, key =>
            {
                return new InterfaceDataContract(this, key);
            });
        }
        else
        {
            contract = _typeToBuiltInContract.GetOrAdd(type, key =>
            {
                TryCreateBuiltInDataContract(key, out var dataContract);
                return dataContract;
            });
        }

        if (contract != null)
        {
            var id = GetId(type);
            AssignDataContractToId(contract, id);
        }

        return contract;
    }

    internal PrimitiveDataContract? GetPrimitiveDataContract(ITypeSymbol type)
    {
        return GetBuiltInDataContract(type) as PrimitiveDataContract;
    }

    internal bool TryCreateBuiltInDataContract(ITypeSymbol type, [NotNullWhen(true)] out DataContract? dataContract)
    {
        if (type.TypeKind == TypeKind.Enum)
        {
            dataContract = null;
            return false;
        }

        dataContract = null;
        switch (type.SpecialType)
        {
            case SpecialType.System_Boolean:
                dataContract = new BooleanDataContract(this);
                break;
            case SpecialType.System_Byte:
                dataContract = new UnsignedByteDataContract(this);
                break;
            case SpecialType.System_Char:
                dataContract = new CharDataContract(this);
                break;
            case SpecialType.System_DateTime:
                dataContract = new DateTimeDataContract(this);
                break;
            case SpecialType.System_Decimal:
                dataContract = new DecimalDataContract(this);
                break;
            case SpecialType.System_Double:
                dataContract = new DoubleDataContract(this);
                break;
            case SpecialType.System_Int16:
                dataContract = new ShortDataContract(this);
                break;
            case SpecialType.System_Int32:
                dataContract = new IntDataContract(this);
                break;
            case SpecialType.System_Int64:
                dataContract = new LongDataContract(this);
                break;
            case SpecialType.System_SByte:
                dataContract = new SignedByteDataContract(this);
                break;
            case SpecialType.System_Single:
                dataContract = new FloatDataContract(this);
                break;
            case SpecialType.System_String:
                dataContract = new StringDataContract(this);
                break;
            case SpecialType.System_UInt16:
                dataContract = new UnsignedShortDataContract(this);
                break;
            case SpecialType.System_UInt32:
                dataContract = new UnsignedIntDataContract(this);
                break;
            case SpecialType.System_UInt64:
                dataContract = new UnsignedLongDataContract(this);
                break;
            case SpecialType.System_Object:
                dataContract = new ObjectDataContract(this);
                break;
            case SpecialType.System_Enum:
            case SpecialType.System_ValueType:
                dataContract = new SpecialTypeDataContract(this, type);
                break;
            case SpecialType.System_Array:
                dataContract = new CollectionDataContract(this, KnownSymbols.ObjectArrayType);
                break;
            default:
                var cmp = SymbolEqualityComparer.Default;
                if (cmp.Equals(type, _knownSymbols.ByteArrayType))
                    dataContract = new ByteArrayDataContract(this);
                else if (cmp.Equals(type, _knownSymbols.UriType))
                    dataContract = new UriDataContract(this);
                else if (cmp.Equals(type, _knownSymbols.XmlQualifiedNameType))
                    dataContract = new QNameDataContract(this);
                else if (cmp.Equals(type, _knownSymbols.TimeSpanType))
                    dataContract = new TimeSpanDataContract(this);
                else if (cmp.Equals(type, _knownSymbols.GuidType))
                    dataContract = new GuidDataContract(this);
                else if (cmp.Equals(type, _knownSymbols.DateOnlyType))
                    dataContract = new DateOnlyDataContract(this);
                else if (cmp.Equals(type, _knownSymbols.TimeOnlyType))
                    dataContract = new TimeOnlyDataContract(this);
                else if (cmp.Equals(type, _knownSymbols.XmlElementType) || cmp.Equals(type, _knownSymbols.XmlNodeArrayType))
                    dataContract = new XmlDataContract(this, type);

                break;
        }

        return dataContract != null;
    }

    internal static ITypeSymbol UnwrapRedundantNullableType(ITypeSymbol type)
    {
        var nullableType = type;
        while (type is INamedTypeSymbol namedType && namedType.IsGenericType && namedType.ConstructedFrom.SpecialType == SpecialType.System_Nullable_T)
        {
            nullableType = type;
            type = namedType.TypeArguments[0];
        }

        return nullableType;
    }

    internal static ITypeSymbol UnwrapNullableType(ITypeSymbol type)
    {
        while (type is INamedTypeSymbol namedType && namedType.IsGenericType && namedType.ConstructedFrom.SpecialType == SpecialType.System_Nullable_T)
            type = namedType.TypeArguments[0];

        return type;
    }

    internal static string GetClrTypeFullName(ITypeSymbol type)
    {
        return type.ToDisplayString(DisplayFormat);
    }

    internal static bool HasDataContractAttribute(ITypeSymbol type)
    {
        foreach (var attributeData in type.GetAttributes())
        {
            if (attributeData.AttributeClass?.ToDisplayString() == DataContractAttributeFullName)
                return true;
        }

        return false;
    }

    internal static bool HasCollectionDataContractAttribute(ITypeSymbol type)
    {
        var attrs = type.GetAttributes();
        foreach (var attribute in attrs)
        {
            if (attribute.AttributeClass?.ToDisplayString() == CollectionDataContractAttributeFullName)
                return true;
        }

        return false;
    }

    internal static bool HasFlagsAttribute(ITypeSymbol type)
    {
        var attrs = type.GetAttributes();
        foreach (var attribute in attrs)
        {
            if (attribute.AttributeClass?.ToDisplayString() == FlagsAttributeFullName)
                return true;
        }

        return false;
    }

    internal static CollectionDataContractAttribute? GetCollectionDataContractAttribute(ITypeSymbol type)
    {
        var attrs = type.GetAttributes();
        foreach (var attribute in attrs)
        {
            if (attribute.AttributeClass?.ToDisplayString() != CollectionDataContractAttributeFullName)
                continue;

            var value = new CollectionDataContractAttribute();
            foreach (var kvp in attribute.NamedArguments)
            {
                if (kvp.Key == nameof(CollectionDataContractAttribute.IsReference))
                    value.IsReference = (bool)kvp.Value.Value!;
                else if (kvp.Key == nameof(CollectionDataContractAttribute.Name))
                    value.Name = (string?)kvp.Value.Value;
                else if (kvp.Key == nameof(CollectionDataContractAttribute.Namespace))
                    value.Namespace = (string?)kvp.Value.Value;
                else if (kvp.Key == nameof(CollectionDataContractAttribute.ItemName))
                    value.ItemName = (string?)kvp.Value.Value;
                else if (kvp.Key == nameof(CollectionDataContractAttribute.KeyName))
                    value.KeyName = (string?)kvp.Value.Value;
                else if (kvp.Key == nameof(CollectionDataContractAttribute.ValueName))
                    value.ValueName = (string?)kvp.Value.Value;
            }

            return value;
        }

        return null;
    }

    internal static DataMemberAttribute? GetDataMemberAttribute(ISymbol symbol)
    {
        var attrs = symbol.GetAttributes();
        var toReturn = default(DataMemberAttribute?);
        foreach (var attribute in attrs)
        {
            if (attribute.AttributeClass?.ToDisplayString() != DataMemberAttributeFullName)
                continue;

            if (toReturn != null)
                ThrowInvalidDataContractException(SR.Format(SR.TooManyDataMembers, GetClrTypeFullName(symbol.ContainingType!), symbol.MetadataName));

            var value = new DataMemberAttribute();
            foreach (var kvp in attribute.NamedArguments)
            {
                if (kvp.Key == nameof(DataMemberAttribute.Name))
                    value.Name = (string)kvp.Value.Value!;
                else if (kvp.Key == nameof(DataMemberAttribute.Order))
                    value.Order = (int)kvp.Value.Value!;
                else if (kvp.Key == nameof(DataMemberAttribute.IsRequired))
                    value.IsRequired = (bool)kvp.Value.Value!;
                else if (kvp.Key == nameof(DataMemberAttribute.EmitDefaultValue))
                    value.EmitDefaultValue = (bool)kvp.Value.Value!;
            }

            toReturn = value;
        }

        return toReturn;
    }

    internal static IgnoreDataMemberAttribute? GetIgnoreDataMemberAttribute(ISymbol symbol)
    {
        var attrs = symbol.GetAttributes();
        var toReturn = default(IgnoreDataMemberAttribute);
        foreach (var attribute in attrs)
        {
            if (attribute.AttributeClass?.ToDisplayString() != IgnoreDataMemberAttributeFullName)
                continue;

            if (toReturn != null)
                ThrowInvalidDataContractException(SR.Format(SR.TooManyIgnoreDataMemberAttributes, GetClrTypeFullName(symbol.ContainingType!), symbol.Name));

            toReturn = new IgnoreDataMemberAttribute();
        }

        return toReturn;
    }

    internal static OptionalFieldAttribute? GetOptionalFieldAttribute(ISymbol symbol)
    {
        var attrs = symbol.GetAttributes();
        foreach (var attribute in attrs)
        {
            if (attribute.AttributeClass?.ToDisplayString() != OptionalFieldAttributeFullName)
                continue;

            return new OptionalFieldAttribute();
        }

        return null;
    }

    public static EnumMemberAttribute? GetEnumMemberAttribute(ISymbol field)
    {
        var attrs = field.GetAttributes();
        var toReturn = default(EnumMemberAttribute);
        foreach (var attribute in attrs)
        {
            if (attribute.AttributeClass?.ToDisplayString() != EnumMemberAttributeFullName)
                continue;

            if (toReturn != null)
                ThrowInvalidDataContractException(SR.Format(SR.TooManyEnumMembers, GetClrTypeFullName(field.ContainingType!), field.Name));

            var value = new EnumMemberAttribute();
            foreach (var kvp in attribute.NamedArguments)
            {
                if (kvp.Key == nameof(EnumMemberAttribute.Value))
                    value.Value = (string)kvp.Value.Value!;
            }

            toReturn = value;
        }

        return toReturn;
    }

    public static XmlRootAttribute? GetXmlRootAttribute(ISymbol symbol)
    {
        var attrs = symbol.GetAttributes();
        foreach (var attribute in attrs.Where(s => s.AttributeClass?.ToDisplayString() == XmlRootAttributeFullName))
        {
            var value = Create(attribute);
            foreach (var kvp in attribute.NamedArguments)
            {
                if (kvp.Key == nameof(XmlRootAttribute.ElementName))
                    value.ElementName = (string)kvp.Value.Value!;
                else if (kvp.Key == nameof(XmlRootAttribute.Namespace))
                    value.Namespace = (string)kvp.Value.Value!;
                else if (kvp.Key == nameof(XmlRootAttribute.IsNullable))
                    value.IsNullable = (bool)kvp.Value.Value!;
                else if (kvp.Key == nameof(XmlRootAttribute.DataType))
                    value.DataType = (string)kvp.Value.Value!;
            }

            return value;
        }

        return null;

        static XmlRootAttribute Create(AttributeData attribute)
        {
            if (attribute.ConstructorArguments.Length == 1 && attribute.ConstructorArguments[0].Value is string elementName)
                return new XmlRootAttribute(elementName);

            return new XmlRootAttribute();
        }
    }

    public static XmlSchemaProviderAttribute? GetXmlSchemaProviderAttribute(ISymbol symbol)
    {
        var attrs = symbol.GetAttributes();
        foreach (var attribute in attrs)
        {
            if (attribute.AttributeClass?.ToDisplayString() != XmlSchemaProviderAttributeFullName)
                continue;

            var value = Create(attribute);
            foreach (var kvp in attribute.NamedArguments)
            {
                if (kvp.Key == nameof(XmlSchemaProviderAttribute.IsAny))
                    value.IsAny = (bool)kvp.Value.Value!;
            }

            return value;
        }

        return null;

        static XmlSchemaProviderAttribute Create(AttributeData attribute)
        {
            if (attribute.ConstructorArguments.Length == 1 && attribute.ConstructorArguments[0].Value is string methodName)
                return new XmlSchemaProviderAttribute(methodName);

            if (attribute.ConstructorArguments.Length == 1 && attribute.ConstructorArguments[0].Value is null)
                return new XmlSchemaProviderAttribute(null);

            throw new NotSupportedException($"Not supported: {attribute}");
        }
    }

    public static ImmutableArray<ITypeSymbol> GetKnownTypeAttributes(ISymbol symbol)
    {
        var builder = ImmutableArray.CreateBuilder<ITypeSymbol>();

        var attrs = symbol.GetAttributes();
        foreach (var attribute in attrs)
        {
            if (attribute.AttributeClass?.ToDisplayString() != KnownTypeAttributeFullName)
                continue;

            var type = Create(attribute);
            if (type != null)
                builder.Add(type);
        }

        return builder.ToImmutable();

        static ITypeSymbol? Create(AttributeData attribute)
        {
            if (attribute.ConstructorArguments.Length == 1)
            {
                var value = attribute.ConstructorArguments[0];
                if (value.Value is string)
                {
                    // KnownType with method name is not yet supported
                    // Diagnostic DCS4004 is reported during parsing
                    return null;
                }

                Debug.Assert(value.Value is ITypeSymbol);
                return (ITypeSymbol)value.Value;
            }

            throw new NotSupportedException();
        }
    }

    internal ImmutableArray<DataContract> ImportKnownTypeAttributes(ITypeSymbol type)
    {
        var builder = default(ImmutableArray<DataContract>.Builder);

        var typesChecked = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);
        ImportKnownTypeAttributes(type, typesChecked, ref builder);
        if (builder != null)
            return builder.ToImmutable();

        return ImmutableArray<DataContract>.Empty;
    }

    private void ImportKnownTypeAttributes(ITypeSymbol? type, HashSet<ITypeSymbol> typesChecked, ref ImmutableArray<DataContract>.Builder? knownDataContracts)
    {
        while (type != null && IsTypeSerializable(type))
        {
            if (!typesChecked.Add(type))
                return;

            var knownTypeAttributes = GetKnownTypeAttributes(type);
            for (var i = 0; i < knownTypeAttributes.Length; ++i)
                CheckAndAdd(knownTypeAttributes[i], typesChecked, ref knownDataContracts);

            type = type.BaseType;
        }
    }

    internal void CheckAndAdd(ITypeSymbol type, HashSet<ITypeSymbol> typesChecked, [NotNullIfNotNull(nameof(nameToDataContractTable))] ref ImmutableArray<DataContract>.Builder? nameToDataContractTable)
    {
        type = UnwrapNullableType(type);
        var dataContract = GetDataContract(type);
        if (nameToDataContractTable == null)
            nameToDataContractTable = ImmutableArray.CreateBuilder<DataContract>();
        else if (nameToDataContractTable.Contains(dataContract))
            return;

        nameToDataContractTable.Add(dataContract);
        ImportKnownTypeAttributes(type, typesChecked, ref nameToDataContractTable);
    }

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void ThrowInvalidDataContractException(string? message)
    {
        throw new InvalidDataContractException(message);
    }

    internal bool IsNonAttributedTypeValidForSerialization(ITypeSymbol type)
    {
        if (type is IArrayTypeSymbol)
            return false;

        if (type.TypeKind == TypeKind.Enum)
            return false;

        if (type.TypeKind == TypeKind.TypeParameter)
            return false;

        if (_knownSymbols.IsIXmlSerializable(type))
            return false;

        if (type.TypeKind == TypeKind.Pointer)
            return false;

        if (HasCollectionDataContractAttribute(type))
            return false;

        if (!_knownSymbols.IsArraySegment(type))
        {
            foreach (var interfaceType in type.AllInterfaces)
            {
                if (CollectionDataContract.IsCollectionInterface(KnownSymbols, interfaceType))
                    return false;
            }
        }

        if (HasDataContractAttribute(type))
            return false;

        if (type.IsValueType)
            return IsVisible(type);

        if (!IsVisible(type))
            return false;

        if (type is INamedTypeSymbol namedType)
            return namedType.Constructors.Any(s => s.Parameters.Length == 0);

        return false;
    }

    private bool IsVisible(ITypeSymbol type)
    {
        if (type.TypeKind == TypeKind.TypeParameter)
            return true;

        if (type is IArrayTypeSymbol arrayType)
            return IsVisible(arrayType.ElementType);

        if (type is IPointerTypeSymbol pointerType)
            return IsVisible(pointerType.PointedAtType);

        if (type is IFunctionPointerTypeSymbol functionPointerType)
        {
            if (!IsVisible(functionPointerType.Signature.ReturnType))
                return false;

            foreach (var parameterType in functionPointerType.Signature.Parameters)
            {
                if (!IsVisible(parameterType.Type))
                    return false;
            }

            return true;
        }

        var currentType = type;
        while (currentType.ContainingType != null)
        {
            if (currentType.DeclaredAccessibility != Accessibility.Public)
                return false;

            currentType = currentType.ContainingType;
        }

        // Now "currentType" should be a top level type
        if (currentType.DeclaredAccessibility != Accessibility.Public)
            return false;

        if (type is INamedTypeSymbol namedType && namedType.IsGenericType && !namedType.IsDefinition)
        {
            foreach (var typeArg in namedType.TypeArguments)
            {
                if (!IsVisible(typeArg))
                    return false;
            }
        }

        return true;
    }

    public bool IsTypeNullable(ITypeSymbol type)
    {
        if (type.IsValueType)
            return false;

        if (type is INamedTypeSymbol namedType)
            return namedType.IsGenericType && KnownSymbols.IsNullable(namedType.ConstructedFrom);

        return false;
    }

    public bool IsTypeSerializable(ITypeSymbol type, HashSet<ITypeSymbol>? previousCollectionTypes = null)
    {
        if (KnownSymbols.IsSerializable(type) ||
            type.TypeKind == TypeKind.Enum ||
            HasDataContractAttribute(type) ||
            type.TypeKind == TypeKind.Interface ||
            type.TypeKind == TypeKind.Pointer ||
            KnownSymbols.IsDbNull(type) ||
            KnownSymbols.IsIXmlSerializable(type))
            return true;

        if (CollectionDataContract.IsCollection(this, type, out var itemType))
        {
            previousCollectionTypes ??= new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);
            ValidatePreviousCollectionTypes(type, itemType, previousCollectionTypes);
            if (IsTypeSerializable(itemType, previousCollectionTypes))
                return true;
        }

        return GetBuiltInDataContract(type) != null ||
            IsNonAttributedTypeValidForSerialization(type);
    }
}