using System.Diagnostics;
using System.Runtime.Serialization;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Marius.DataContracts.SourceGenerators.DataContracts;
using Marius.DataContracts.SourceGenerators.Generators;

namespace Marius.DataContracts.SourceGenerators.Specs;

/// <summary>
/// Parses DataContractContext and creates immutable Spec models free from Roslyn objects.
/// </summary>
internal sealed class DataContractParser
{
    private readonly DataContractContext _context;
    private readonly Dictionary<ITypeSymbol, TypeSpec> _typeSpecCache;
    private readonly Dictionary<DataContract, int> _contractToId;
    private readonly Dictionary<ISymbol, PrivateAccessorSpec> _accessorCache;
    private readonly List<PrivateAccessorSpec> _accessors;
    private readonly List<DiagnosticInfo> _diagnostics;
    private int _accessorIndex;

    /// <summary>
    /// Gets the location of the context class (first data contract type) for fallback location.
    /// </summary>
    private Location? _contextClassLocation;

    public DataContractParser(DataContractContext context)
    {
        _context = context;
        _typeSpecCache = new Dictionary<ITypeSymbol, TypeSpec>(SymbolEqualityComparer.Default);
        _contractToId = new Dictionary<DataContract, int>(ReferenceEqualityComparer.Instance);
        _accessorCache = new Dictionary<ISymbol, PrivateAccessorSpec>(SymbolEqualityComparer.Default);
        _accessors = new List<PrivateAccessorSpec>();
        _diagnostics = new List<DiagnosticInfo>();
    }

    /// <summary>
    /// Reports a diagnostic to be included in the output.
    /// </summary>
    public void ReportDiagnostic(DiagnosticDescriptor descriptor, Location? location, params object?[]? messageArgs)
    {
        Debug.Assert(_contextClassLocation != null || location != null);

        if (location is null || !_context.KnownSymbols.Compilation.ContainsLocation(location))
        {
            // If location is null or is a location outside the current compilation, fall back to the location of the context class.
            location = _contextClassLocation;
        }

        _diagnostics.Add(DiagnosticInfo.Create(descriptor, location, messageArgs));
    }

    /// <summary>
    /// Reports a diagnostic for a symbol.
    /// </summary>
    public void ReportDiagnostic(DiagnosticDescriptor descriptor, ISymbol? symbol, params object?[]? messageArgs)
    {
        ReportDiagnostic(descriptor, symbol?.GetLocation(), messageArgs);
    }

    /// <summary>
    /// Parses all data contracts from the context and returns an immutable DataContractSetSpec.
    /// </summary>
    public DataContractSetSpec Parse()
    {
        // Build mapping of DataContract to index
        for (var i = 0; i < _context.DataContracts.Length; i++)
        {
            var contract = _context.DataContracts[i];
            if (contract != null)
                _contractToId[contract] = i;
        }

        // Create specs
        var specs = new List<DataContractSpec>();
        for (var i = 0; i < _context.DataContracts.Length; i++)
        {
            var contract = _context.DataContracts[i];
            _contextClassLocation = contract?.UnderlyingType.GetLocation();

            if (contract != null)
            {
                try
                {
                    var spec = CreateSpec(contract, i);
                    if (spec != null)
                        specs.Add(spec);
                }
                catch (InvalidDataContractException ex)
                {
                    // Capture the exception as a diagnostic
                    ReportDiagnostic(
                        DiagnosticDescriptors.InvalidDataContract,
                        contract.UnderlyingType,
                        ex.Message);
                }
                catch (Exception ex) when (!ExceptionUtility.IsFatal(ex))
                {
                    // Capture unexpected exceptions as diagnostics
                    ReportDiagnostic(
                        DiagnosticDescriptors.UnexpectedError,
                        contract.UnderlyingType,
                        DataContractContext.GetClrTypeFullName(contract.UnderlyingType),
                        ex.Message);
                }
            }
        }

        // Build final array with proper indexing
        var resultArray = new DataContractSpec[_context.DataContracts.Length];
        foreach (var spec in specs)
            resultArray[spec.Id] = spec;

        return new DataContractSetSpec
        {
            Contracts = new EquatableArray<DataContractSpec>(resultArray),
            PrivateAccessors = new EquatableArray<PrivateAccessorSpec>(_accessors),
            Diagnostics = new EquatableArray<DiagnosticInfo>(_diagnostics),
        };
    }

    private string GetOrCreateAccessor(ISymbol symbol, bool isRegularConstructor = false)
    {
        if (symbol is IMethodSymbol methodSymbol)
            symbol = methodSymbol.OriginalDefinition;
        else if (symbol is IFieldSymbol fieldSymbol)
            symbol = fieldSymbol.OriginalDefinition;

        if (_accessorCache.TryGetValue(symbol, out var existing))
            return existing.Name;

        var spec = CreateAccessorSpec(symbol, isRegularConstructor);
        _accessorCache[symbol] = spec;
        _accessors.Add(spec);
        return spec.Name;
    }

    private string GetOrCreateSerializableConstructorAccessor(INamedTypeSymbol type)
    {
        // we cannot find the constructor on the type itself - this can happen due to Ref assemblies not having private constructors
        // just assume that one exists
        var name = $"ISerializable_Constructor_{Escape(type.Name)}{_accessorIndex++}";
        var spec = new PrivateAccessorSpec
        {
            Name = name,
            Kind = PrivateAccessorKind.Method,
            TargetName = ".ctor",
            ContainingType = CreateTypeSpec(type),
            ReturnType = CreateTypeSpec(_context.KnownSymbols.VoidType),
            Parameters = new EquatableArray<ParameterSpec>(new[]
            {
                new ParameterSpec
                {
                    Name = "info",
                    Type = CreateTypeSpec(_context.KnownSymbols.SerializationInfoType!),
                    RefKind = ParameterRefKind.None,
                },
                new ParameterSpec
                {
                    Name = "context",
                    Type = CreateTypeSpec(_context.KnownSymbols.StreamingContextType!),
                    RefKind = ParameterRefKind.None,
                },
            }),
            IsRegularConstructor = false,
        };

        _accessorCache[type] = spec;
        _accessors.Add(spec);
        return spec.Name;
    }

    private PrivateAccessorSpec CreateAccessorSpec(ISymbol symbol, bool isRegularConstructor)
    {
        switch (symbol)
        {
            case IMethodSymbol method:
            {
                var name = method.MethodKind == MethodKind.Constructor
                    ? $"Constructor{_accessorIndex++}"
                    : $"Method_{Escape(method.Name)}{_accessorIndex++}";

                var parameters = new List<ParameterSpec>();
                foreach (var p in method.Parameters)
                {
                    var refKind = p.RefKind switch
                    {
                        RefKind.Ref => ParameterRefKind.Ref,
                        RefKind.Out => ParameterRefKind.Out,
                        RefKind.In => ParameterRefKind.In,
                        _ => ParameterRefKind.None,
                    };
                    parameters.Add(new ParameterSpec { Name = p.Name, Type = CreateTypeSpec(p.Type), RefKind = refKind });
                }

                return new PrivateAccessorSpec
                {
                    Name = name,
                    Kind = method.MethodKind == MethodKind.Constructor ? PrivateAccessorKind.Constructor : PrivateAccessorKind.Method,
                    TargetName = method.Name,
                    ContainingType = CreateTypeSpec(method.ContainingType),
                    ReturnType = method.MethodKind == MethodKind.Constructor ? null : CreateTypeSpec(method.ReturnType),
                    Parameters = new EquatableArray<ParameterSpec>(parameters),
                    IsRegularConstructor = isRegularConstructor,
                };
            }

            case IFieldSymbol field:
            {
                var name = $"FieldRef_{Escape(field.Name)}{_accessorIndex++}";
                return new PrivateAccessorSpec
                {
                    Name = name,
                    Kind = PrivateAccessorKind.Field,
                    TargetName = field.Name,
                    ContainingType = CreateTypeSpec(field.ContainingType),
                    ReturnType = CreateTypeSpec(field.Type),
                    Parameters = EquatableArray<ParameterSpec>.Empty,
                    IsRegularConstructor = false,
                };
            }

            default:
                throw new NotSupportedException($"Unsupported symbol type: {symbol.GetType().Name}");
        }
    }

    /// <summary>
    /// Creates a TypeParameterSpec from an ITypeParameterSymbol, including all constraints.
    /// </summary>
    private TypeParameterSpec CreateTypeParameterSpec(ITypeParameterSymbol typeParam, bool isMethodTypeParameter)
    {
        var constraints = new List<TypeConstraintSpec>();
        foreach (var constraintType in typeParam.ConstraintTypes)
        {
            if (constraintType is ITypeParameterSymbol typeParamConstraint)
            {
                constraints.Add(new TypeConstraintSpec
                {
                    Kind = TypeConstraintKind.TypeParameter,
                    ConstraintTypeFullName = typeParamConstraint.Name,
                    IsGenericType = false,
                });
            }
            else
            {
                // Type constraint (base class or interface)
                var typeArgs = EquatableArray<string>.Empty;
                var isGenericType = false;

                if (constraintType is INamedTypeSymbol namedConstraint && namedConstraint.IsGenericType)
                {
                    isGenericType = true;
                    var args = new List<string>();
                    foreach (var arg in namedConstraint.TypeArguments)
                    {
                        // Use the simple name for type parameters, fully qualified for concrete types
                        if (arg is ITypeParameterSymbol)
                            args.Add(arg.Name);
                        else
                            args.Add(arg.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));
                    }

                    typeArgs = new EquatableArray<string>(args);
                }

                constraints.Add(new TypeConstraintSpec
                {
                    Kind = TypeConstraintKind.Type,
                    ConstraintTypeFullName = constraintType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                    IsGenericType = isGenericType,
                    TypeArguments = typeArgs,
                });
            }
        }

        return new TypeParameterSpec
        {
            Name = typeParam.Name,
            Ordinal = typeParam.Ordinal,
            IsMethodTypeParameter = isMethodTypeParameter,
            Constraints = new EquatableArray<TypeConstraintSpec>(constraints),
            HasNotNullConstraint = typeParam.HasNotNullConstraint,
            HasUnmanagedConstraint = typeParam.HasUnmanagedTypeConstraint,
            HasValueTypeConstraint = typeParam.HasValueTypeConstraint,
            HasReferenceTypeConstraint = typeParam.HasReferenceTypeConstraint,
            HasReferenceTypeConstraintNullable = typeParam.ReferenceTypeConstraintNullableAnnotation == NullableAnnotation.Annotated,
            HasConstructorConstraint = typeParam.HasConstructorConstraint,
        };
    }

    public static string Escape(string input)
    {
        if (SyntaxFacts.IsValidIdentifier(input))
            return input;

        Span<char> buffer = stackalloc char[input.Length];
        var index = 0;
        foreach (var item in input)
        {
            if (SyntaxFacts.IsIdentifierPartCharacter(item))
                buffer[index++] = item;
        }

        unsafe
        {
            fixed (char* ptr = buffer)
                return new string(ptr, 0, index);
        }
    }

    private int GetContractId(DataContract? contract)
    {
        if (contract == null)
            return -1;

        if (!_contractToId.TryGetValue(contract, out var id))
        {
            for (var i = 0; i < _context.DataContracts.Length; i++)
            {
                if (ReferenceEquals(_context.DataContracts[i], contract))
                {
                    _contractToId[contract] = i;
                    return i;
                }
            }

            throw new InvalidOperationException($"DataContract {contract.GetType()} not found in context.");
        }

        return id;
    }

    /// <summary>
    /// Checks for KnownType attributes with method names and reports a warning since they are not yet supported.
    /// </summary>
    private void CheckKnownTypeMethodAttributes(ITypeSymbol type)
    {
        const string KnownTypeAttributeFullName = "System.Runtime.Serialization.KnownTypeAttribute";

        foreach (var attribute in type.GetAttributes())
        {
            if (attribute.AttributeClass?.ToDisplayString() != KnownTypeAttributeFullName)
                continue;

            if (attribute.ConstructorArguments.Length == 1)
            {
                var value = attribute.ConstructorArguments[0];
                if (value.Value is string)
                    ReportDiagnostic(DiagnosticDescriptors.KnownTypeMethodNotSupported, type, DataContractContext.GetClrTypeFullName(type));
            }
        }
    }

    private DataContractSpec? CreateSpec(DataContract contract, int id)
    {
        return contract switch
        {
            ClassDataContract classContract => CreateClassSpec(classContract, id),
            CollectionDataContract collectionContract => CreateCollectionSpec(collectionContract, id),
            EnumDataContract enumContract => CreateEnumSpec(enumContract, id),
            PrimitiveDataContract primitiveContract => CreatePrimitiveSpec(primitiveContract, id),
            XmlDataContract xmlContract => CreateXmlSpec(xmlContract, id),
            _ => null,
        };
    }

    private ClassDataContractSpec CreateClassSpec(ClassDataContract contract, int id)
    {
        var type = (INamedTypeSymbol)contract.UnderlyingType;
        var typeSpec = CreateTypeSpec(type);
        var originalUnderlyingTypeSpec = CreateTypeSpec(contract.OriginalUnderlyingType);

        // Create member specs
        var memberSpecs = new List<DataMemberSpec>();
        foreach (var member in contract.Members)
        {
            var memberSpec = CreateDataMemberSpec(member);
            memberSpecs.Add(memberSpec);
        }

        // Get constructor info
        var hasParameterlessConstructor = false;
        string? constructorAccessorName = null;

        if (!contract.Context.KnownSymbols.IsDbNull(type) && !(type.IsAbstract || type.TypeKind == TypeKind.Interface))
        {
            var constructor = type.InstanceConstructors.FirstOrDefault(c => c.Parameters.Length == 0);
            hasParameterlessConstructor = constructor != null;
            if (constructor != null && !constructor.IsAccessible())
                constructorAccessorName = GetOrCreateAccessor(constructor, true);
        }

        // Get ISerializable constructor info
        string? iSerializableConstructorAccessorName = null;
        if (contract.IsISerializable)
        {
            var knownSymbols = contract.Context.KnownSymbols;
            var iSerializableConstructor = type.InstanceConstructors.FirstOrDefault(c =>
                c.Parameters.Length == 2 &&
                SymbolEqualityComparer.Default.Equals(c.Parameters[0].Type, knownSymbols.SerializationInfoType) &&
                SymbolEqualityComparer.Default.Equals(c.Parameters[1].Type, knownSymbols.StreamingContextType));

            if (iSerializableConstructor == null)
                ReportDiagnostic(DiagnosticDescriptors.ISerializableConstructorNotFound, type, DataContractContext.GetClrTypeFullName(type));
            else
                iSerializableConstructorAccessorName = GetOrCreateAccessor(iSerializableConstructor);

            if (iSerializableConstructorAccessorName == null && !knownSymbols.IsDbNull(type))
                iSerializableConstructorAccessorName = GetOrCreateSerializableConstructorAccessor(type);
        }

        // Report diagnostic if ExtensionData is used (not yet supported)
        if (contract.HasExtensionData)
            ReportDiagnostic(DiagnosticDescriptors.ExtensionDataNotSupported, type, DataContractContext.GetClrTypeFullName(type));

        // Report warnings for serialization callbacks (not yet implemented)
        if (contract.OnDeserializing != null)
            ReportDiagnostic(DiagnosticDescriptors.OnDeserializingCallbackNotImplemented, contract.OnDeserializing, DataContractContext.GetClrTypeFullName(type));

        if (contract.OnDeserialized != null)
            ReportDiagnostic(DiagnosticDescriptors.OnDeserializedCallbackNotImplemented, contract.OnDeserialized, DataContractContext.GetClrTypeFullName(type));

        if (contract.OnSerializing != null)
            ReportDiagnostic(DiagnosticDescriptors.OnSerializingCallbackNotImplemented, contract.OnSerializing, DataContractContext.GetClrTypeFullName(type));

        if (contract.OnSerialized != null)
            ReportDiagnostic(DiagnosticDescriptors.OnSerializedCallbackNotImplemented, contract.OnSerialized, DataContractContext.GetClrTypeFullName(type));

        // Check for factory method and deserialization callback
        var hasFactoryMethod = type.AllInterfaces.Any(s => SymbolEqualityComparer.Default.Equals(_context.KnownSymbols.IObjectReferenceType, s));
        var hasDeserializationCallback = type.Interfaces.Any(s => SymbolEqualityComparer.Default.Equals(_context.KnownSymbols.IDeserializationCallbackType, s));

        // Check for KnownType attributes with method names (not yet supported)
        CheckKnownTypeMethodAttributes(type);

        var knownDataContracts = Array.Empty<KnownDataContractSpec>();
        if (contract.KnownDataContracts.Length > 0)
        {
            knownDataContracts = new KnownDataContractSpec[contract.KnownDataContracts.Length];
            var index = 0;
            foreach (var knownContract in contract.KnownDataContracts)
            {
                var knownContractId = GetContractId(knownContract);
                knownDataContracts[index++] = new KnownDataContractSpec { ContractId = knownContractId };
            }
        }

        var typeInfoPropertyName = GetTypeInfoPropertyName(contract.UnderlyingType);
        return new ClassDataContractSpec
        {
            Id = id,
            GeneratedName = typeInfoPropertyName,
            TypeInfoPropertyName = typeInfoPropertyName,
            UnderlyingType = typeSpec,
            OriginalUnderlyingType = originalUnderlyingTypeSpec,
            Name = contract.Name,
            Namespace = contract.Namespace,
            XmlName = contract.XmlName.Name,
            XmlNamespace = contract.XmlName.Namespace,
            IsPrimitive = contract.IsPrimitive,
            IsReference = contract.IsReference,
            IsValueType = contract.IsValueType,
            ContractNamespaces = new EquatableArray<string>(contract.ContractNamespaces ?? Array.Empty<string>()),
            MemberNames = new EquatableArray<string>(contract.MemberNames ?? Array.Empty<string>()),
            MemberNamespaces = new EquatableArray<string>(contract.MemberNamespaces ?? Array.Empty<string>()),
            Members = new EquatableArray<DataMemberSpec>(memberSpecs),
            KnownDataContracts = new EquatableArray<KnownDataContractSpec>(knownDataContracts),
            ChildElementNamespaces = new EquatableArray<string?>(contract.ChildElementNamespaces ?? Array.Empty<string>()),
            SerializationExceptionMessage = contract.SerializationExceptionMessage,
            DeserializationExceptionMessage = contract.DeserializationExceptionMessage,
            IsNonAttributedType = contract.IsNonAttributedType,
            IsISerializable = contract.IsISerializable,
            HasRoot = contract.HasRoot,
            BaseContractId = GetContractId(contract.BaseContract),
            CanContainReferences = contract.CanContainReferences,
            IsBuiltInDataContract = contract.IsBuiltInDataContract,
            TopLevelElementName = contract.TopLevelElementName,
            TopLevelElementNamespace = contract.TopLevelElementNamespace,
            HasExtensionData = contract.HasExtensionData,
            IsDbNull = _context.KnownSymbols.IsDbNull(type),
            BaseClassContractId = GetContractId(contract.BaseClassContract),
            HasParameterlessConstructor = hasParameterlessConstructor,
            ConstructorAccessorName = constructorAccessorName,
            HasFactoryMethod = hasFactoryMethod,
            HasDeserializationCallback = hasDeserializationCallback,
            ISerializableConstructorAccessorName = iSerializableConstructorAccessorName,
            Kind = DataContractKindSpec.Class,
        };
    }

    private CollectionDataContractSpec CreateCollectionSpec(CollectionDataContract contract, int id)
    {
        var typeSpec = CreateTypeSpec(contract.UnderlyingType);
        var itemTypeSpec = CreateTypeSpec(contract.ItemType);
        var originalUnderlyingTypeSec = CreateTypeSpec(contract.OriginalUnderlyingType);
        var collectionElementTypeSpec = default(TypeSpec);

        if (contract.Kind != CollectionKind.Array)
            collectionElementTypeSpec = CreateTypeSpec(contract.GetCollectionElementType());

        // Validate KeyValuePair contract for dictionaries
        if (contract.Kind == CollectionKind.Dictionary || contract.Kind == CollectionKind.GenericDictionary)
        {
            if (contract.ItemContract is not ClassDataContract itemClassContract || itemClassContract.Members.Count < 2)
            {
                ReportDiagnostic(
                    DiagnosticDescriptors.KeyValuePairContractNotFound,
                    contract.UnderlyingType,
                    DataContractContext.GetClrTypeFullName(contract.UnderlyingType));
            }
        }

        var knownDataContracts = Array.Empty<KnownDataContractSpec>();
        if (contract.KnownDataContracts.Length > 0)
        {
            knownDataContracts = new KnownDataContractSpec[contract.KnownDataContracts.Length];
            var index = 0;
            foreach (var knownContract in contract.KnownDataContracts)
            {
                var knownContractId = GetContractId(knownContract);
                knownDataContracts[index++] = new KnownDataContractSpec { ContractId = knownContractId };
            }
        }

        var effectiveTypeSpec = default(TypeSpec?);
        if (typeSpec.TypeKind == TypeKindSpec.Interface)
        {
            var itemType = contract.ItemType;
            switch (contract.Kind)
            {
                case CollectionKind.GenericDictionary:
                    if (itemType is INamedTypeSymbol namedType)
                    {
                        var keyType = namedType.TypeArguments.Length > 0 ? namedType.TypeArguments[0] : itemType;
                        var valueType = namedType.TypeArguments.Length > 1 ? namedType.TypeArguments[1] : itemType;
                        effectiveTypeSpec = CreateTypeSpec(_context.KnownSymbols.DictionaryOfTKeyTValueType!.Construct(keyType, valueType));
                    }
                    else
                    {
                        effectiveTypeSpec = CreateTypeSpec(_context.KnownSymbols.DictionaryOfTKeyTValueType!.Construct(itemType, itemType));
                    }

                    break;
                case CollectionKind.Dictionary:
                    effectiveTypeSpec = CreateTypeSpec(_context.KnownSymbols.DictionaryOfTKeyTValueType!.Construct(_context.KnownSymbols.ObjectType, _context.KnownSymbols.ObjectType));
                    break;
                case CollectionKind.Collection:
                case CollectionKind.GenericCollection:
                case CollectionKind.Enumerable:
                case CollectionKind.GenericEnumerable:
                case CollectionKind.List:
                case CollectionKind.GenericList:
                    effectiveTypeSpec = CreateTypeSpec(_context.KnownSymbols.Compilation.CreateArrayTypeSymbol(itemType, rank: 1));
                    break;
            }
        }

        var typeInfoPropertyName = GetTypeInfoPropertyName(contract.UnderlyingType);
        return new CollectionDataContractSpec
        {
            Id = id,
            GeneratedName = typeInfoPropertyName,
            TypeInfoPropertyName = typeInfoPropertyName,
            UnderlyingType = typeSpec,
            OriginalUnderlyingType = originalUnderlyingTypeSec,
            Name = contract.Name,
            Namespace = contract.Namespace,
            XmlName = contract.XmlName.Name,
            XmlNamespace = contract.XmlName.Namespace,
            IsPrimitive = contract.IsPrimitive,
            IsReference = contract.IsReference,
            IsValueType = contract.IsValueType,
            IsISerializable = contract.IsISerializable,
            HasRoot = contract.HasRoot,
            BaseContractId = GetContractId(contract.BaseContract),
            CanContainReferences = contract.CanContainReferences,
            IsBuiltInDataContract = contract.IsBuiltInDataContract,
            TopLevelElementName = contract.TopLevelElementName,
            TopLevelElementNamespace = contract.TopLevelElementNamespace,
            CollectionKind = contract.Kind,
            CollectionItemName = contract.CollectionItemName,
            CollectionElementType = collectionElementTypeSpec,
            ItemType = itemTypeSpec,
            EffectiveType = effectiveTypeSpec,
            ItemName = contract.ItemName,
            KeyName = contract.KeyName,
            ValueName = contract.ValueName,
            ItemContractId = GetContractId(contract.ItemContract),
            SharedTypeContractId = GetContractId(contract.SharedTypeContract),
            ChildElementNamespace = contract.ChildElementNamespace,
            HasAddMethod = contract.AddMethod != null && contract.AddMethod.IsAccessible(),
            KnownDataContracts = new EquatableArray<KnownDataContractSpec>(knownDataContracts),
            Kind = DataContractKindSpec.Collection,
        };
    }

    private EnumDataContractSpec CreateEnumSpec(EnumDataContract contract, int id)
    {
        var typeSpec = CreateTypeSpec(contract.UnderlyingType);
        var originalUnderlyingTypeSec = CreateTypeSpec(contract.OriginalUnderlyingType);

        // Create member specs for enum members
        var memberSpecs = new List<DataMemberSpec>();
        foreach (var member in contract.Members)
        {
            var memberSpec = CreateDataMemberSpec(member);
            memberSpecs.Add(memberSpec);
        }

        var childElementNames = new List<string>();
        if (contract.ChildElementNames != null)
            childElementNames.AddRange(contract.ChildElementNames);

        var values = new List<long>();
        if (contract.Values != null)
            values.AddRange(contract.Values);

        var typeInfoPropertyName = GetTypeInfoPropertyName(contract.UnderlyingType);
        return new EnumDataContractSpec
        {
            Id = id,
            GeneratedName = typeInfoPropertyName,
            TypeInfoPropertyName = typeInfoPropertyName,
            UnderlyingType = typeSpec,
            OriginalUnderlyingType = originalUnderlyingTypeSec,
            Name = contract.Name,
            Namespace = contract.Namespace,
            XmlName = contract.XmlName.Name,
            XmlNamespace = contract.XmlName.Namespace,
            IsPrimitive = contract.IsPrimitive,
            IsReference = contract.IsReference,
            IsValueType = contract.IsValueType,
            IsISerializable = contract.IsISerializable,
            HasRoot = contract.HasRoot,
            CanContainReferences = contract.CanContainReferences,
            IsBuiltInDataContract = contract.IsBuiltInDataContract,
            TopLevelElementName = contract.TopLevelElementName,
            TopLevelElementNamespace = contract.TopLevelElementNamespace,
            Members = new EquatableArray<DataMemberSpec>(memberSpecs),
            Values = new EquatableArray<long>(values),
            IsFlags = contract.IsFlags,
            IsULong = contract.IsULong,
            ChildElementNames = new EquatableArray<string>(childElementNames),
            BaseContractId = GetContractId(contract.BaseContract),
            BaseContractXmlName = contract.BaseContractName.Name,
            BaseContractXmlNamespace = contract.BaseContractName.Namespace,
            Kind = DataContractKindSpec.Enum,
        };
    }

    private PrimitiveDataContractSpec CreatePrimitiveSpec(PrimitiveDataContract contract, int id)
    {
        var typeSpec = CreateTypeSpec(contract.UnderlyingType);
        var originalUnderlyingTypeSec = CreateTypeSpec(contract.OriginalUnderlyingType);

        var interfaceTypeSpec = default(TypeSpec);
        var typeInfoPropertyName = GetTypeInfoPropertyName(contract.UnderlyingType);
        if (contract is InterfaceDataContract interfaceDataContract)
        {
            interfaceTypeSpec = CreateTypeSpec(interfaceDataContract.InterfaceType);
            typeInfoPropertyName = GetTypeInfoPropertyName(interfaceDataContract.InterfaceType);
        }

        return new PrimitiveDataContractSpec
        {
            Id = id,
            GeneratedName = typeInfoPropertyName,
            TypeInfoPropertyName = typeInfoPropertyName,
            UnderlyingType = typeSpec,
            OriginalUnderlyingType = originalUnderlyingTypeSec,
            Name = contract.Name,
            Namespace = contract.Namespace,
            XmlName = contract.XmlName.Name,
            XmlNamespace = contract.XmlName.Namespace,
            IsPrimitive = contract.IsPrimitive,
            IsReference = contract.IsReference,
            IsValueType = contract.IsValueType,
            IsISerializable = contract.IsISerializable,
            HasRoot = contract.HasRoot,
            BaseContractId = GetContractId(contract.BaseContract),
            CanContainReferences = contract.CanContainReferences,
            IsBuiltInDataContract = contract.IsBuiltInDataContract,
            TopLevelElementName = contract.TopLevelElementName,
            TopLevelElementNamespace = contract.TopLevelElementNamespace,
            PrimitiveContractName = contract.GetType().Name,
            WriteMethodName = contract.WriteMethodName,
            ReadMethodName = contract.ReadMethodName,
            InterfaceType = interfaceTypeSpec,
            Kind = DataContractKindSpec.Primitive,
        };
    }

    private XmlDataContractSpec CreateXmlSpec(XmlDataContract contract, int id)
    {
        var type = contract.UnderlyingType as INamedTypeSymbol;
        var typeSpec = CreateTypeSpec(contract.UnderlyingType);
        var originalUnderlyingTypeSec = CreateTypeSpec(contract.OriginalUnderlyingType);

        // Check if this is XElement type
        var isXElement = type != null && SymbolEqualityComparer.Default.Equals(type, _context.KnownSymbols.XElementType);
        var isXmlElementOrXmlNodeArray = SymbolEqualityComparer.Default.Equals(contract.UnderlyingType, _context.KnownSymbols.XmlElementType)
            || SymbolEqualityComparer.Default.Equals(contract.UnderlyingType, _context.KnownSymbols.XmlNodeArrayType);

        // Get constructor info for non-XElement types
        var constructorAccessorName = default(string);
        if (!isXElement && type != null && !(type.IsAbstract || type.TypeKind == TypeKind.Interface))
        {
            var constructor = type.InstanceConstructors.FirstOrDefault(c => c.Parameters.Length == 0);
            if (constructor != null && !constructor.IsAccessible())
                constructorAccessorName = GetOrCreateAccessor(constructor, true);
        }

        var schemaProviderMethodAccessorName = default(string);
        var schemaProviderMethodIsXmlSchemaType = default(bool?);
        if (!string.IsNullOrEmpty(contract.SchemaProviderMethod))
        {
            var method = type?.GetMembers(contract.SchemaProviderMethod!)
                .OfType<IMethodSymbol>()
                .FirstOrDefault(m => m.Parameters.Length == 1 &&
                    SymbolEqualityComparer.Default.Equals(m.Parameters[0].Type, _context.KnownSymbols.XmlSchemaSetType) &&
                    m.IsStatic);

            if (method == null)
            {
                ReportDiagnostic(
                    DiagnosticDescriptors.SchemaProviderMethodNotFound,
                    contract.UnderlyingType,
                    contract.SchemaProviderMethod,
                    DataContractContext.GetClrTypeFullName(contract.UnderlyingType));
            }
            else
            {
                if (!method.IsAccessible())
                    schemaProviderMethodAccessorName = GetOrCreateAccessor(method);

                // Validate return type of schema provider method using Roslyn
                if (_context.KnownSymbols.Compilation.HasImplicitConversion(method.ReturnType, _context.KnownSymbols.XmlQualifiedNameType))
                {
                    schemaProviderMethodIsXmlSchemaType = false;
                }
                else if (_context.KnownSymbols.Compilation.HasImplicitConversion(method.ReturnType, _context.KnownSymbols.XmlSchemaTypeType))
                {
                    schemaProviderMethodIsXmlSchemaType = true;
                }
                else
                {
                    ReportDiagnostic(
                        DiagnosticDescriptors.InvalidReturnTypeOnGetSchemaMethod,
                        method,
                        contract.SchemaProviderMethod,
                        DataContractContext.GetClrTypeFullName(contract.UnderlyingType),
                        DataContractContext.GetClrTypeFullName(method.ReturnType),
                        DataContractContext.GetClrTypeFullName(_context.KnownSymbols.XmlQualifiedNameType!),
                        DataContractContext.GetClrTypeFullName(_context.KnownSymbols.XmlSchemaTypeType!));
                }
            }
        }

        var knownDataContracts = Array.Empty<KnownDataContractSpec>();
        if (contract.KnownDataContracts.Length > 0)
        {
            knownDataContracts = new KnownDataContractSpec[contract.KnownDataContracts.Length];
            var index = 0;
            foreach (var knownContract in contract.KnownDataContracts)
            {
                var knownContractId = GetContractId(knownContract);
                knownDataContracts[index++] = new KnownDataContractSpec { ContractId = knownContractId };
            }
        }

        // Get XmlRootAttribute from the contract
        var xmlRootAttribute = contract.XmlRootAttribute;
        var xmlRootAttributeSpec = xmlRootAttribute != null
            ? new XmlRootAttributeSpec
            {
                ElementName = xmlRootAttribute.ElementName,
                Namespace = xmlRootAttribute.Namespace,
                IsNullable = xmlRootAttribute.IsNullable,
                DataType = xmlRootAttribute.DataType,
            }
            : null;

        var typeInfoPropertyName = GetTypeInfoPropertyName(contract.UnderlyingType);
        return new XmlDataContractSpec
        {
            Id = id,
            GeneratedName = typeInfoPropertyName,
            TypeInfoPropertyName = typeInfoPropertyName,
            UnderlyingType = typeSpec,
            OriginalUnderlyingType = originalUnderlyingTypeSec,
            Name = contract.Name,
            Namespace = contract.Namespace,
            XmlName = contract.XmlName.Name,
            XmlNamespace = contract.XmlName.Namespace,
            IsPrimitive = contract.IsPrimitive,
            IsReference = contract.IsReference,
            IsValueType = contract.IsValueType,
            IsISerializable = contract.IsISerializable,
            HasRoot = contract.HasRoot,
            BaseContractId = GetContractId(contract.BaseContract),
            CanContainReferences = contract.CanContainReferences,
            IsBuiltInDataContract = contract.IsBuiltInDataContract,
            TopLevelElementName = contract.TopLevelElementName,
            TopLevelElementNamespace = contract.TopLevelElementNamespace,
            IsAny = contract.IsAny,
            SchemaProviderMethod = contract.SchemaProviderMethod,
            SchemaProviderMethodAccessorName = schemaProviderMethodAccessorName,
            SchemaProviderMethodIsXmlSchemaType = schemaProviderMethodIsXmlSchemaType,
            ConstructorAccessorName = constructorAccessorName,
            IsXElement = isXElement,
            IsXmlElementOrXmlNodeArray = isXmlElementOrXmlNodeArray,
            KnownDataContracts = new EquatableArray<KnownDataContractSpec>(knownDataContracts),
            XmlRootAttribute = xmlRootAttributeSpec,
            Kind = DataContractKindSpec.Xml,
        };
    }

    private DataMemberSpec CreateDataMemberSpec(DataMember member)
    {
        var memberTypeSpec = CreateTypeSpec(member.MemberType);
        var memberInfoSpec = CreateMemberSpec(member.MemberInfo, member);

        var primitiveReadMethodName = default(string);
        if (member.MemberPrimitiveContract != null)
            primitiveReadMethodName = member.MemberPrimitiveContract.ReadMethodName;

        var primitiveWriteMethodName = default(string);
        if (member.MemberPrimitiveContract != null)
            primitiveWriteMethodName = member.MemberPrimitiveContract.WriteMethodName;

        var conflictingMember = default(DataMemberSpec);
        if (member.ConflictingMember != null)
            conflictingMember = CreateDataMemberSpec(member.ConflictingMember);

        return new DataMemberSpec
        {
            Name = member.Name,
            IsRequired = member.IsRequired,
            IsNullable = member.IsNullable,
            EmitDefaultValue = member.EmitDefaultValue,
            Order = member.Order,
            IsGetOnlyCollection = member.IsGetOnlyCollection,
            MemberInfo = memberInfoSpec,
            MemberType = memberTypeSpec,
            MemberTypeContractId = GetContractId(member.MemberTypeContract),
            ConflictingMember = conflictingMember,
            PrimitiveReadMethodName = primitiveReadMethodName,
            PrimitiveWriteMethodName = primitiveWriteMethodName,
        };
    }

    private MemberSpec CreateMemberSpec(ISymbol member, DataMember dataMember)
    {
        var declaringTypeSpec = CreateTypeSpec(member.ContainingType);

        TypeSpec memberTypeSpec;
        MemberKindSpec kind;
        bool isAccessible;
        bool hasAccessibleGetter;
        bool hasAccessibleSetter;
        var isSetterInitOnly = false;
        string? getterAccessorName = null;
        string? setterAccessorName = null;

        if (member is IPropertySymbol property)
        {
            memberTypeSpec = CreateTypeSpec(property.Type);
            kind = MemberKindSpec.Property;
            isAccessible = property.IsAccessible();
            hasAccessibleGetter = property.GetMethod?.IsAccessible() ?? false;
            hasAccessibleSetter = property.SetMethod?.IsAccessible() ?? false;
            isSetterInitOnly = property.SetMethod?.IsInitOnly ?? false;

            if (!hasAccessibleGetter && property.GetMethod != null)
                getterAccessorName = GetOrCreateAccessor(property.GetMethod);

            if ((!hasAccessibleSetter || isSetterInitOnly) && property.SetMethod != null)
                setterAccessorName = GetOrCreateAccessor(property.SetMethod);

            // Report diagnostic if property cannot be set and is not get-only collection
            if (!dataMember.IsGetOnlyCollection && property.SetMethod == null)
            {
                ReportDiagnostic(
                    DiagnosticDescriptors.MemberCannotBeSet,
                    member,
                    member.Name,
                    DataContractContext.GetClrTypeFullName(member.ContainingType));
            }
        }
        else if (member is IFieldSymbol field)
        {
            memberTypeSpec = CreateTypeSpec(field.Type);
            kind = MemberKindSpec.Field;
            isAccessible = field.IsAccessible();
            hasAccessibleGetter = isAccessible;
            hasAccessibleSetter = isAccessible;

            if (!isAccessible)
            {
                getterAccessorName = GetOrCreateAccessor(field);
                setterAccessorName = getterAccessorName; // Field accessors return ref, so same for get/set
            }
            else if (field.IsReadOnly)
            {
                setterAccessorName = GetOrCreateAccessor(field);
                hasAccessibleSetter = false;
            }
        }
        else
        {
            // Report diagnostic for unsupported member types
            ReportDiagnostic(
                DiagnosticDescriptors.UnsupportedMemberKind,
                member,
                member.GetType().Name,
                member.Name,
                DataContractContext.GetClrTypeFullName(member.ContainingType));

            // Fallback for other symbol types (shouldn't normally happen)
            memberTypeSpec = CreateTypeSpec((ITypeSymbol)member);
            kind = MemberKindSpec.Field;
            isAccessible = true;
            hasAccessibleGetter = true;
            hasAccessibleSetter = true;
        }

        return new MemberSpec
        {
            Name = member.Name,
            DeclaringType = declaringTypeSpec,
            MemberType = memberTypeSpec,
            Kind = kind,
            IsAccessible = isAccessible,
            HasAccessibleGetter = hasAccessibleGetter,
            HasAccessibleSetter = hasAccessibleSetter,
            IsSetterInitOnly = isSetterInitOnly,
            GetterAccessorName = getterAccessorName,
            SetterAccessorName = setterAccessorName,
        };
    }

    public TypeSpec CreateTypeSpec(ITypeSymbol type)
    {
        if (_typeSpecCache.TryGetValue(type, out var cached))
            return cached;

        var fullyQualifiedName = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        var name = type.Name;
        var ns = type.ContainingNamespace?.ToDisplayString() ?? "";
        var isValueType = type.IsValueType;
        var isArray = type is IArrayTypeSymbol;
        var isGenericType = type is INamedTypeSymbol namedType && namedType.IsGenericType;
        var isAbstract = type.IsAbstract;

        // Check if this is an open generic type (unconstructed, like List<> vs List<string>)
        var isOpenGenericType = false;
        if (type is INamedTypeSymbol namedTypeForOpenCheck && namedTypeForOpenCheck.IsGenericType)
        {
            isOpenGenericType = SymbolEqualityComparer.Default.Equals(namedTypeForOpenCheck, namedTypeForOpenCheck.ConstructedFrom);
            if (!isOpenGenericType)
                isOpenGenericType = namedTypeForOpenCheck.TypeArguments.Any(s => s.TypeKind == TypeKind.TypeParameter);
        }

        var isNullableValueType = false;
        if (type is INamedTypeSymbol namedTypeSymbol && namedTypeSymbol.IsGenericType) 
            isNullableValueType = _context.KnownSymbols.IsNullable(namedTypeSymbol.ConstructedFrom);

        var specialType = ConvertSpecialType(type.SpecialType);
        var typeKind = ConvertTypeKind(type.TypeKind);

        TypeSpec? elementType = null;
        if (type is IArrayTypeSymbol arrayType)
            elementType = CreateTypeSpec(arrayType.ElementType);
        else if (isNullableValueType && type is INamedTypeSymbol nullableType && nullableType.TypeArguments.Length > 0) 
            elementType = CreateTypeSpec(nullableType.TypeArguments[0]);

        var constructedFromSpec = default(TypeSpec);
        var typeArguments = EquatableArray<TypeSpec>.Empty;
        var typeParameters = EquatableArray<TypeParameterSpec>.Empty;
        if (type is INamedTypeSymbol genericNamedType)
        {
            if (genericNamedType.TypeArguments.Length > 0)
            {
                var args = new List<TypeSpec>();
                foreach (var arg in genericNamedType.TypeArguments)
                    args.Add(CreateTypeSpec(arg));

                typeArguments = new EquatableArray<TypeSpec>(args);
            }

            if (genericNamedType.TypeParameters.Length > 0)
            {
                var parameters = new List<TypeParameterSpec>();
                foreach (var item in genericNamedType.TypeParameters)
                    parameters.Add(CreateTypeParameterSpec(item, false));

                typeParameters = new EquatableArray<TypeParameterSpec>(parameters);
            }

            if (genericNamedType.IsGenericType && !SymbolEqualityComparer.Default.Equals(genericNamedType, genericNamedType.ConstructedFrom))
                constructedFromSpec = CreateTypeSpec(genericNamedType.ConstructedFrom);
        }

        var typeParameterSpec = default(TypeParameterSpec);
        if (type is ITypeParameterSymbol typeParameter) 
            typeParameterSpec = CreateTypeParameterSpec(typeParameter, false);

        var isTypeSerializable = _context.IsTypeSerializable(type);
        var spec = new TypeSpec
        {
            FullyQualifiedName = fullyQualifiedName,
            Name = name,
            Namespace = ns,
            IsValueType = isValueType,
            IsNullableValueType = isNullableValueType,
            IsGenericType = isGenericType,
            IsOpenGenericType = isOpenGenericType,
            IsArray = isArray,
            IsAbstract = isAbstract,
            IsTypeSerializable = isTypeSerializable,
            SpecialType = specialType,
            TypeKind = typeKind,
            ElementType = elementType,
            TypeParameter = typeParameterSpec,
            ConstructedFrom = constructedFromSpec,
            TypeArguments = typeArguments,
            TypeParameters = typeParameters,
        };

        _typeSpecCache[type] = spec;
        return spec;
    }

    private static bool IsOpenGeneric(ITypeSymbol type)
    {
        if (type.TypeKind == TypeKind.TypeParameter)
            return true;

        if (type is INamedTypeSymbol namedType)
        {
            if (SymbolEqualityComparer.Default.Equals(namedType, namedType.ConstructedFrom))
                return true;

            return namedType.TypeArguments.Any(IsOpenGeneric);
        }

        return false;
    }

    private static SpecialTypeKind ConvertSpecialType(SpecialType specialType)
    {
        return specialType switch
        {
            SpecialType.System_Object => SpecialTypeKind.Object,
            SpecialType.System_Boolean => SpecialTypeKind.Boolean,
            SpecialType.System_Char => SpecialTypeKind.Char,
            SpecialType.System_SByte => SpecialTypeKind.SByte,
            SpecialType.System_Byte => SpecialTypeKind.Byte,
            SpecialType.System_Int16 => SpecialTypeKind.Int16,
            SpecialType.System_UInt16 => SpecialTypeKind.UInt16,
            SpecialType.System_Int32 => SpecialTypeKind.Int32,
            SpecialType.System_UInt32 => SpecialTypeKind.UInt32,
            SpecialType.System_Int64 => SpecialTypeKind.Int64,
            SpecialType.System_UInt64 => SpecialTypeKind.UInt64,
            SpecialType.System_Single => SpecialTypeKind.Single,
            SpecialType.System_Double => SpecialTypeKind.Double,
            SpecialType.System_Decimal => SpecialTypeKind.Decimal,
            SpecialType.System_String => SpecialTypeKind.String,
            SpecialType.System_DateTime => SpecialTypeKind.DateTime,
            _ => SpecialTypeKind.None,
        };
    }

    private static TypeKindSpec ConvertTypeKind(TypeKind typeKind)
    {
        return typeKind switch
        {
            TypeKind.Array => TypeKindSpec.Array,
            TypeKind.Class => TypeKindSpec.Class,
            TypeKind.Struct => TypeKindSpec.Struct,
            TypeKind.Interface => TypeKindSpec.Interface,
            TypeKind.Enum => TypeKindSpec.Enum,
            TypeKind.Delegate => TypeKindSpec.Delegate,
            TypeKind.TypeParameter => TypeKindSpec.TypeParameter,
            _ => TypeKindSpec.Unknown,
        };
    }

    private static string GetTypeInfoPropertyName(ITypeSymbol type)
    {
        if (type is IArrayTypeSymbol arrayType)
        {
            var rank = arrayType.Rank;
            var suffix = rank == 1 ? "Array" : $"Array{rank}D"; // Array, Array2D, Array3D, ...
            return GetTypeInfoPropertyName(arrayType.ElementType) + suffix;
        }
 
        if (type is not INamedTypeSymbol namedType || !namedType.IsGenericType)
            return type.Name;
 
        var sb = new ValueStringBuilder();
        var name = namedType.Name;
 
        sb.Append(name);
 
        if (namedType.GetAllTypeArgumentsInScope() is List<ITypeSymbol> typeArgsInScope)
        {
            foreach (var genericArg in typeArgsInScope) 
                sb.Append(GetTypeInfoPropertyName(genericArg));
        }
 
        return sb.ToString();
    }
}