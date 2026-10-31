// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.Serialization;

namespace Marius.DataContracts.Runtime;

// Runtime provider decisions do not add contracts or grant known-type scope. Every
// contract used on either side of a substitution must already be generated.
internal sealed class SerializationSurrogate
{
    private readonly IDataContractProvider _contracts;
    private readonly ISerializationSurrogateProvider _provider;

    public SerializationSurrogate(IDataContractProvider contracts, ISerializationSurrogateProvider provider)
    {
        _contracts = contracts;
        _provider = provider;
    }

    internal DataContract GetGeneratedContract(Type type)
    {
        if (_contracts.TryGetDataContract(type, out var contract))
            return contract;

        var underlying = Nullable.GetUnderlyingType(type);
        if (underlying != null && _contracts.TryGetDataContract(underlying, out contract))
            return contract;

        throw new InvalidDataContractException($"Serialization surrogate type '{type}' is outside the generated contract set. Generate the source and replacement contracts, including their dependencies and closed generic types, before using a surrogate provider.");
    }

    internal Type GetSurrogatedType(Type type)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;
        var source = GetGeneratedContract(type);
        if (source.IsBuiltInDataContract)
            return type;

        var replacement = _provider.GetSurrogateType(type) ?? type;
        GetGeneratedContract(replacement);
        return replacement;
    }

    internal object? GetObjectToSerialize(object? value, Type targetType, ref Type actualType)
    {
        var source = GetGeneratedContract(actualType);
        if (value == null || source.IsBuiltInDataContract)
            return value;

        var replacement = _provider.GetObjectToSerialize(value, GetProviderTargetType(targetType));
        if (!ReferenceEquals(value, replacement))
            actualType = replacement?.GetType() ?? typeof(object);
        GetGeneratedContract(actualType);
        return replacement;
    }

    private static Type GetProviderTargetType(Type type)
    {
        // Providers observe the framework's wire-adapter Type, even though the
        // generated serializer uses its own statically compiled adapter contract.
        // Literal type names let the trimmer/AOT compiler retain these two Type
        // identities. They never create a contract or enable reflective serialization.
        if (type == typeof(Adapters.DateTimeOffsetAdapter))
            return Type.GetType("System.Runtime.Serialization.DateTimeOffsetAdapter, System.Private.DataContractSerialization", throwOnError: true)!;
        if (type == typeof(Adapters.MemoryStreamAdapter))
            return Type.GetType("System.Runtime.Serialization.MemoryStreamAdapter, System.Private.DataContractSerialization", throwOnError: true)!;
        return type;
    }

    internal object? GetDeserializedObject(object? value, DataContract contract, Type targetType)
    {
        if (value == null || contract.IsBuiltInDataContract)
            return value;

        var replacement = _provider.GetDeserializedObject(value, targetType);
        if (replacement != null)
            GetGeneratedContract(replacement.GetType());
        return replacement;
    }
}
