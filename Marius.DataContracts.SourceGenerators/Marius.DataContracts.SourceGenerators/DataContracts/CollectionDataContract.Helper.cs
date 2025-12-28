using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.Serialization;
using Microsoft.CodeAnalysis;

namespace Marius.DataContracts.SourceGenerators.DataContracts;

public partial class CollectionDataContract
{
    internal static bool IsCollection(DataContractContext context, ITypeSymbol type)
    {
        return IsCollection(context, type, out _);
    }

    internal static bool IsCollection(DataContractContext context, ITypeSymbol type, [NotNullWhen(true)] out ITypeSymbol? itemType)
    {
        return IsCollection(context, type, out itemType, true /*constructorRequired*/);
    }

    internal static bool IsCollection(DataContractContext context, ITypeSymbol type, bool constructorRequired, bool skipIfReadOnlyContract)
    {
        return IsCollection(context, type, out _, constructorRequired, skipIfReadOnlyContract);
    }

    private static bool IsCollection(DataContractContext context, ITypeSymbol type, [NotNullWhen(true)] out ITypeSymbol? itemType, bool constructorRequired, bool skipIfReadOnlyContract = false)
    {
        if (type is IArrayTypeSymbol arrayType && context.GetBuiltInDataContract(type) == null)
        {
            itemType = arrayType.ElementType;
            return true;
        }

        return IsCollectionOrTryCreate(context, type, tryCreate: false, out _, out itemType, constructorRequired, skipIfReadOnlyContract);
    }

    internal static bool TryCreate(DataContractContext context, ITypeSymbol type, [NotNullWhen(true)] out DataContract? dataContract)
    {
        return IsCollectionOrTryCreate(context, type, tryCreate: true, out dataContract!, out _, constructorRequired: true);
    }

    internal static bool TryCreateGetOnlyCollectionDataContract(DataContractContext context, ITypeSymbol type, [NotNullWhen(true)] out DataContract? dataContract)
    {
        if (type is IArrayTypeSymbol arrayType)
        {
            dataContract = new CollectionDataContract(context, arrayType);
            return true;
        }

        return IsCollectionOrTryCreate(context, type, tryCreate: true, out dataContract!, out _, constructorRequired: false);
    }

    private const string AddMethodName = "Add";
    private const string GetEnumeratorMethodName = "GetEnumerator";

    private static bool IsCollectionOrTryCreate(DataContractContext context, ITypeSymbol type, bool tryCreate, out DataContract? dataContract, out ITypeSymbol itemType, bool constructorRequired, bool skipIfReadOnlyContract = false)
    {
        dataContract = null;
        itemType = context.KnownSymbols.ObjectType;

        if (context.GetBuiltInDataContract(type) != null)
            return HandleIfInvalidCollection(context, type, tryCreate, false /*hasCollectionDataContract*/, false /*isBaseTypeCollection*/, SR.CollectionTypeCannotBeBuiltIn, null, ref dataContract);

        IMethodSymbol? addMethod, getEnumeratorMethod;
        var hasCollectionDataContract = DataContractContext.HasCollectionDataContractAttribute(type);
        var isReadOnlyContract = false;
        var serializationExceptionMessage = default(string);
        var deserializationExceptionMessage = default(string);

        var knownSymbols = context.KnownSymbols;
        var cmp = SymbolEqualityComparer.Default;

        var baseType = type.BaseType;
        var isBaseTypeCollection = baseType != null && !cmp.Equals(baseType, knownSymbols.ObjectType) && !cmp.Equals(baseType, knownSymbols.ValueTypeType) && !cmp.Equals(baseType, knownSymbols.UriType) && IsCollection(context, baseType);

        // Avoid creating an invalid collection contract for Serializable types since we can create a ClassDataContract instead
        var createContractWithException = isBaseTypeCollection && !knownSymbols.IsSerializable(type);
        if (DataContractContext.HasDataContractAttribute(type))
            return HandleIfInvalidCollection(context, type, tryCreate, hasCollectionDataContract, createContractWithException, SR.CollectionTypeCannotHaveDataContract, null, ref dataContract);

        if (knownSymbols.IsIXmlSerializable(type) || knownSymbols.IsArraySegment(type))
            return false;

        if (!knownSymbols.IsIEnumerable(type))
            return HandleIfInvalidCollection(context, type, tryCreate, hasCollectionDataContract, createContractWithException, SR.CollectionTypeIsNotIEnumerable, null, ref dataContract);

        if (type.TypeKind == TypeKind.Interface)
        {
            var namedType = (INamedTypeSymbol)type;
            var interfaceTypeToCheck = namedType.IsGenericType ? namedType.ConstructedFrom : namedType;
            for (var i = 0; i < knownSymbols.KnownCollectionInterfaces.Length; i++)
            {
                if (cmp.Equals(knownSymbols.KnownCollectionInterfaces[i], interfaceTypeToCheck))
                {
                    addMethod = null;
                    if (namedType.IsGenericType)
                    {
                        var genericArgs = namedType.TypeArguments;
                        if (cmp.Equals(interfaceTypeToCheck, knownSymbols.IDictionaryOfTKeyTValueType))
                        {
                            itemType = knownSymbols.KeyValueOfType!.Construct(genericArgs.ToArray());
                            addMethod = type.GetMembers().OfType<IMethodSymbol>().SingleOrDefault(s => s.Name == AddMethodName);
                            getEnumeratorMethod = knownSymbols.IEnumerableOfTType!.Construct(itemType).GetMembers().OfType<IMethodSymbol>().Single(s => s.Name == GetEnumeratorMethodName);
                        }
                        else
                        {
                            itemType = genericArgs[0];
                            if (cmp.Equals(interfaceTypeToCheck, knownSymbols.ICollectionOfTType) || cmp.Equals(interfaceTypeToCheck, knownSymbols.IListOfTType))
                                addMethod = knownSymbols.ICollectionOfTType!.Construct(itemType).GetMembers().OfType<IMethodSymbol>().SingleOrDefault(s => s.Name == AddMethodName);

                            getEnumeratorMethod = knownSymbols.IEnumerableOfTType!.Construct(itemType).GetMembers().OfType<IMethodSymbol>().Single(s => s.Name == GetEnumeratorMethodName);
                        }
                    }
                    else
                    {
                        if (cmp.Equals(interfaceTypeToCheck, knownSymbols.IDictionaryType))
                        {
                            itemType = knownSymbols.KeyValueOfType!.Construct(knownSymbols.ObjectType, knownSymbols.ObjectType);
                            addMethod = type.GetMembers().OfType<IMethodSymbol>().SingleOrDefault(s => s.Name == AddMethodName);
                        }
                        else
                        {
                            itemType = knownSymbols.ObjectType;

                            // IList has AddMethod
                            if (cmp.Equals(interfaceTypeToCheck, knownSymbols.IListType))
                                addMethod = knownSymbols.IListType!.GetMembers().OfType<IMethodSymbol>().SingleOrDefault(s => s.Name == AddMethodName);
                        }

                        getEnumeratorMethod = knownSymbols.IEnumerableType!.GetMembers().OfType<IMethodSymbol>().Single(s => s.Name == GetEnumeratorMethodName);
                    }

                    if (tryCreate)
                        dataContract = new CollectionDataContract(context, type, (CollectionKind)(i + 1), itemType, getEnumeratorMethod, addMethod, null /*defaultCtor*/);

                    return true;
                }
            }
        }

        var defaultCtor = default(IMethodSymbol);
        if (!type.IsValueType)
        {
            var namedType = (INamedTypeSymbol)type;
            defaultCtor = namedType.Constructors.FirstOrDefault(s => s.Parameters.Length == 0);
            if (defaultCtor == null && constructorRequired)
            {
                // All collection types could be considered read-only collections except collection types that are marked [Serializable].
                // Collection types marked [Serializable] cannot be read-only collections for backward compatibility reasons.
                // DataContract types and POCO types cannot be collection types, so they don't need to be factored in
                if (knownSymbols.IsSerializable(type))
                    return HandleIfInvalidCollection(context, type, tryCreate, hasCollectionDataContract, createContractWithException, SR.CollectionTypeDoesNotHaveDefaultCtor, null, ref dataContract);

                isReadOnlyContract = true;
                GetReadOnlyCollectionExceptionMessages(type, hasCollectionDataContract, SR.CollectionTypeDoesNotHaveDefaultCtor, null, out serializationExceptionMessage, out deserializationExceptionMessage);
            }
        }

        var knownInterfaceType = default(INamedTypeSymbol);
        var kind = CollectionKind.None;
        var multipleDefinitions = false;
        var interfaceTypes = type.AllInterfaces;
        foreach (var interfaceType in interfaceTypes)
        {
            var interfaceTypeToCheck = interfaceType.IsGenericType ? interfaceType.ConstructedFrom : interfaceType;
            for (var i = 0; i < knownSymbols.KnownCollectionInterfaces.Length; i++)
            {
                if (cmp.Equals(knownSymbols.KnownCollectionInterfaces[i], interfaceTypeToCheck))
                {
                    var currentKind = (CollectionKind)(i + 1);
                    if (kind == CollectionKind.None || currentKind < kind)
                    {
                        kind = currentKind;
                        knownInterfaceType = interfaceType;
                        multipleDefinitions = false;
                    }
                    // ReSharper disable once BitwiseOperatorOnEnumWithoutFlags
                    else if ((kind & currentKind) == currentKind)
                    {
                        multipleDefinitions = true;
                    }

                    break;
                }
            }
        }

        if (kind == CollectionKind.None)
            return HandleIfInvalidCollection(context, type, tryCreate, hasCollectionDataContract, createContractWithException, SR.CollectionTypeIsNotIEnumerable, null, ref dataContract);

        Debug.Assert(knownInterfaceType != null);
        if (kind == CollectionKind.Enumerable || kind == CollectionKind.Collection || kind == CollectionKind.GenericEnumerable)
        {
            if (multipleDefinitions)
                knownInterfaceType = knownSymbols.IEnumerableType;

            itemType = knownInterfaceType!.IsGenericType ? knownInterfaceType.TypeArguments[0] : knownSymbols.ObjectType;
            GetCollectionMethods(knownSymbols, type, knownInterfaceType, new[] { itemType }, false /*addMethodOnInterface*/, out getEnumeratorMethod, out addMethod);

            Debug.Assert(getEnumeratorMethod != null);

            if (addMethod == null)
            {
                // All collection types could be considered read-only collections except collection types that are marked [Serializable].
                // Collection types marked [Serializable] cannot be read-only collections for backward compatibility reasons.
                // DataContract types and POCO types cannot be collection types, so they don't need to be factored in.
                if (knownSymbols.IsSerializable(type) || skipIfReadOnlyContract)
                    return HandleIfInvalidCollection(context, type, tryCreate, hasCollectionDataContract, createContractWithException && !skipIfReadOnlyContract, SR.CollectionTypeDoesNotHaveAddMethod, DataContractContext.GetClrTypeFullName(itemType), ref dataContract);

                isReadOnlyContract = true;
                GetReadOnlyCollectionExceptionMessages(type, hasCollectionDataContract, SR.CollectionTypeDoesNotHaveAddMethod, DataContractContext.GetClrTypeFullName(itemType), out serializationExceptionMessage, out deserializationExceptionMessage);
            }

            if (tryCreate)
            {
                dataContract = isReadOnlyContract
                    ? new CollectionDataContract(context, type, kind, itemType, getEnumeratorMethod!, serializationExceptionMessage, deserializationExceptionMessage)
                    : new CollectionDataContract(context, type, kind, itemType, getEnumeratorMethod!, addMethod, defaultCtor, !constructorRequired);
            }
        }
        else
        {
            if (multipleDefinitions)
            {
                return HandleIfInvalidCollection(
                    context,
                    type,
                    tryCreate,
                    hasCollectionDataContract,
                    createContractWithException,
                    SR.CollectionTypeHasMultipleDefinitionsOfInterface,
                    knownSymbols.KnownCollectionInterfaces[(int)kind]?.MetadataName,
                    ref dataContract);
            }

            var addMethodTypeArray = default(ITypeSymbol[]);
            switch (kind)
            {
                case CollectionKind.GenericDictionary:
                    addMethodTypeArray = knownInterfaceType!.TypeArguments.ToArray();
                    var isOpenGeneric = cmp.Equals(knownInterfaceType, knownInterfaceType.ConstructedFrom)
                        || (addMethodTypeArray[0].TypeKind == TypeKind.TypeParameter && addMethodTypeArray[1].TypeKind == TypeKind.TypeParameter);
                    itemType = isOpenGeneric ? knownSymbols.KeyValueOfType! : knownSymbols.KeyValueOfType!.Construct(addMethodTypeArray);
                    break;
                case CollectionKind.Dictionary:
                    addMethodTypeArray = new[] { knownSymbols.ObjectType, knownSymbols.ObjectType };
                    itemType = knownSymbols.KeyValueOfType!.Construct(addMethodTypeArray);
                    break;
                case CollectionKind.GenericList:
                case CollectionKind.GenericCollection:
                    addMethodTypeArray = knownInterfaceType!.TypeArguments.ToArray();
                    itemType = addMethodTypeArray[0];
                    break;
                case CollectionKind.List:
                    itemType = knownSymbols.ObjectType;
                    addMethodTypeArray = new[] { itemType };
                    break;
            }

            if (tryCreate)
            {
                Debug.Assert(addMethodTypeArray != null);
                GetCollectionMethods(knownSymbols, type, knownInterfaceType!, addMethodTypeArray!, true /*addMethodOnInterface*/, out getEnumeratorMethod, out addMethod);

                Debug.Assert(getEnumeratorMethod != null);
                dataContract = isReadOnlyContract
                    ? new CollectionDataContract(context, type, kind, itemType, getEnumeratorMethod!, serializationExceptionMessage, deserializationExceptionMessage)
                    : new CollectionDataContract(context, type, kind, itemType, getEnumeratorMethod!, addMethod, defaultCtor, !constructorRequired);
            }
        }

        return !(isReadOnlyContract && skipIfReadOnlyContract);
    }

    private static bool HandleIfInvalidCollection(DataContractContext context, ITypeSymbol type, bool tryCreate, bool hasCollectionDataContract, bool createContractWithException, string message, string? param, ref DataContract? dataContract)
    {
        if (hasCollectionDataContract)
        {
            if (tryCreate)
                throw new InvalidDataContractException(GetInvalidCollectionMessage(message, SR.Format(SR.InvalidCollectionDataContract, DataContractContext.GetClrTypeFullName(type)), param));

            return true;
        }

        if (createContractWithException)
        {
            if (tryCreate)
                dataContract = new CollectionDataContract(context, type, GetInvalidCollectionMessage(message, SR.Format(SR.InvalidCollectionType, DataContractContext.GetClrTypeFullName(type)), param));
            return true;
        }

        return false;
    }

    private static string GetInvalidCollectionMessage(string message, string nestedMessage, string? param)
    {
        return param == null ? SR.Format(message, nestedMessage) : SR.Format(message, nestedMessage, param);
    }

    private static void GetReadOnlyCollectionExceptionMessages(ITypeSymbol type, bool hasCollectionDataContract, string message, string? param, out string serializationExceptionMessage, out string deserializationExceptionMessage)
    {
        serializationExceptionMessage = GetInvalidCollectionMessage(message, SR.Format(hasCollectionDataContract ? SR.InvalidCollectionDataContract : SR.InvalidCollectionType, DataContractContext.GetClrTypeFullName(type)), param);
        deserializationExceptionMessage = GetInvalidCollectionMessage(message, SR.Format(SR.ReadOnlyCollectionDeserialization, DataContractContext.GetClrTypeFullName(type)), param);
    }

    private static void GetCollectionMethods(
        KnownTypeSymbols knownSymbols,
        ITypeSymbol type,
        ITypeSymbol interfaceType,
        ITypeSymbol[] addMethodTypeArray,
        bool addMethodOnInterface,
        out IMethodSymbol? getEnumeratorMethod,
        out IMethodSymbol? addMethod)
    {
        addMethod = getEnumeratorMethod = null;

        if (addMethodOnInterface)
        {
            addMethod = type.GetMembers(AddMethodName)
                .OfType<IMethodSymbol>()
                .FirstOrDefault(m =>
                    m.MethodKind == MethodKind.Ordinary &&
                    m.DeclaredAccessibility == Accessibility.Public &&
                    !m.IsStatic &&
                    m.Parameters.Length == addMethodTypeArray.Length &&
                    m.Parameters.Select(p => p.Type).SequenceEqual(addMethodTypeArray, SymbolEqualityComparer.Default));

            if (addMethod == null || !SymbolEqualityComparer.Default.Equals(addMethod.Parameters[0].Type, addMethodTypeArray[0]))
            {
                FindCollectionMethodsOnInterface(type, interfaceType, ref addMethod, ref getEnumeratorMethod);
                if (addMethod != null)
                    addMethod = (IMethodSymbol?)type.FindImplementationForInterfaceMember(addMethod);

                if (addMethod == null)
                {
                    var parentInterfaceTypes = interfaceType.AllInterfaces.ToArray();
                    // The for loop below depeneds on the order for the items in parentInterfaceTypes, which
                    // doesnt' seem right. But it's the behavior of DCS on the full framework.
                    // Sorting the array to make sure the behavior is consistent with Desktop's.
                    Array.Sort(parentInterfaceTypes, (x, y) => string.CompareOrdinal(x.ToDisplayString(), y.ToDisplayString()));
                    foreach (var parentInterfaceType in parentInterfaceTypes)
                    {
                        if (IsKnownInterface(knownSymbols, parentInterfaceType))
                        {
                            FindCollectionMethodsOnInterface(type, parentInterfaceType, ref addMethod, ref getEnumeratorMethod);
                            if (addMethod == null)
                                break;
                        }
                    }

                    if (addMethod != null)
                        addMethod = (IMethodSymbol?)type.FindImplementationForInterfaceMember(addMethod);
                }
            }
        }
        else
        {
            addMethod = type.GetMembers(AddMethodName)
                .OfType<IMethodSymbol>()
                .FirstOrDefault(m =>
                    m.MethodKind == MethodKind.Ordinary &&
                    m.DeclaredAccessibility == Accessibility.Public &&
                    !m.IsStatic &&
                    m.Parameters.Length == addMethodTypeArray.Length &&
                    m.Parameters.Select(p => p.Type).SequenceEqual(addMethodTypeArray, SymbolEqualityComparer.Default));
        }

        if (getEnumeratorMethod == null)
        {
            getEnumeratorMethod = type.GetMembers(GetEnumeratorMethodName)
                .OfType<IMethodSymbol>()
                .FirstOrDefault(m =>
                    m.MethodKind == MethodKind.Ordinary &&
                    m.DeclaredAccessibility == Accessibility.Public &&
                    !m.IsStatic &&
                    m.Parameters.Length == 0);

            if (getEnumeratorMethod == null || !SymbolEqualityComparer.Default.Equals(getEnumeratorMethod.ReturnType, knownSymbols.IEnumeratorType))
            {
                var ienumerableInterface =
                    interfaceType.AllInterfaces.FirstOrDefault(t => t.ToDisplayString().StartsWith("System.Collections.IEnumerable")) ??
                    knownSymbols.IEnumerableType;

                getEnumeratorMethod = GetIEnumerableGetEnumeratorMethod(type, ienumerableInterface);
                if (getEnumeratorMethod != null)
                    getEnumeratorMethod = (IMethodSymbol?)type.FindImplementationForInterfaceMember(getEnumeratorMethod);
            }
        }
    }

    private static IMethodSymbol? GetIEnumerableGetEnumeratorMethod(ITypeSymbol type, ITypeSymbol? ienumerableInterface)
    {
        return GetTargetMethodWithName(GetEnumeratorMethodName, type, ienumerableInterface);
    }

    internal static IMethodSymbol? GetTargetMethodWithName(string name,
        ITypeSymbol type,
        ITypeSymbol? interfaceType)
    {
        var interfaces = type.AllInterfaces;
        for (var i = 0; i < interfaces.Length; i++)
        {
            if (SymbolEqualityComparer.Default.Equals(interfaces[i], interfaceType))
                return interfaces[i].GetMembers(name).OfType<IMethodSymbol>().FirstOrDefault();
        }

        return null;
    }

    private static bool IsKnownInterface(KnownTypeSymbols knownSymbols, INamedTypeSymbol type)
    {
        var typeToCheck = type.IsGenericType ? type.ConstructedFrom : type;
        foreach (var knownInterfaceType in knownSymbols.KnownCollectionInterfaces)
        {
            if (SymbolEqualityComparer.Default.Equals(typeToCheck, knownInterfaceType))
                return true;
        }

        return false;
    }

    private static void FindCollectionMethodsOnInterface(
        ITypeSymbol type,
        ITypeSymbol interfaceType,
        ref IMethodSymbol? addMethod,
        ref IMethodSymbol? getEnumeratorMethod)
    {
        var interfaces = type.AllInterfaces;
        for (var i = 0; i < interfaces.Length; i++)
        {
            if (SymbolEqualityComparer.Default.Equals(interfaces[i], interfaceType))
            {
                addMethod = interfaces[i].GetMembers(AddMethodName).OfType<IMethodSymbol>().FirstOrDefault() ?? addMethod;
                getEnumeratorMethod = interfaces[i].GetMembers(GetEnumeratorMethodName).OfType<IMethodSymbol>().FirstOrDefault() ?? getEnumeratorMethod;
                break;
            }
        }
    }

    internal static bool IsCollectionInterface(KnownTypeSymbols knownSymbols, ITypeSymbol type)
    {
        if (type is INamedTypeSymbol namedType && namedType.IsGenericType)
            type = namedType.ConstructedFrom;

        for (var i = 0; i < knownSymbols.KnownCollectionInterfaces.Length; i++)
        {
            if (SymbolEqualityComparer.Default.Equals(knownSymbols.KnownCollectionInterfaces[i], type))
                return true;
        }

        return false;
    }
}