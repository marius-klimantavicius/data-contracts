// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license

namespace Marius.DataContracts.Runtime;

internal sealed class HybridObjectCache
{
    private Dictionary<string, object?>? _objectDictionary;
    private Dictionary<string, object?>? _referencedObjectDictionary;

    internal void Add(string id, object? obj)
    {
        _objectDictionary ??= new Dictionary<string, object?>();
 
        if (!_objectDictionary.TryAdd(id, obj))
            throw XmlObjectSerializer.CreateSerializationException(SR.Format(SR.MultipleIdDefinition, id));
    }
 
    internal void Remove(string id)
    {
        _objectDictionary?.Remove(id);
    }
 
    internal object? GetObject(string id)
    {
        _referencedObjectDictionary ??= new Dictionary<string, object?>();
        _referencedObjectDictionary.TryAdd(id, null);
 
        if (_objectDictionary != null)
        {
            _objectDictionary.TryGetValue(id, out var obj);
            return obj;
        }
 
        return null;
    }
 
    internal bool IsObjectReferenced(string id)
    {
        return _referencedObjectDictionary != null && _referencedObjectDictionary.ContainsKey(id);
    }
}