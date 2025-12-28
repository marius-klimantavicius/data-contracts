// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license

using System.ComponentModel;
using System.Runtime.Serialization;

namespace Marius.DataContracts.Runtime;

[EditorBrowsable(EditorBrowsableState.Advanced)]
public interface IKeyValue
{
    object? Key { get; set; }
    object? Value { get; set; }
}

[EditorBrowsable(EditorBrowsableState.Advanced)]
public static class KeyValue
{
    public static KeyValue<K, V> Create<K, V>(K key, V value) => new KeyValue<K, V>(key, value);
}

[EditorBrowsable(EditorBrowsableState.Advanced)]
[DataContract(Namespace = "http://schemas.microsoft.com/2003/10/Serialization/Arrays")]
public struct KeyValue<K, V> : IKeyValue
{
    internal KeyValue(K key, V value)
    {
        Key = key;
        Value = value;
    }

    [DataMember(IsRequired = true)]
    public K Key { get; set; }

    [DataMember(IsRequired = true)]
    public V Value { get; set; }

    object? IKeyValue.Key
    {
        get => Key;
        set => Key = (K)value!;
    }

    object? IKeyValue.Value
    {
        get => Value;
        set => Value = (V)value!;
    }
}