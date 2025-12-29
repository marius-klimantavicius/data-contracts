// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license

using System.Xml;
using DataContractDictionary = System.Collections.Frozen.FrozenDictionary<System.Xml.XmlQualifiedName, Marius.DataContracts.Runtime.DataContract>;

namespace Marius.DataContracts.Runtime;

public struct ScopedKnownTypes
{
    internal DataContractDictionary[] dataContractDictionaries;
    private int _count;
    internal void Push(DataContractDictionary dataContractDictionary)
    {
        if (dataContractDictionaries == null)
        {
            dataContractDictionaries = new DataContractDictionary[4];
        }
        else if (_count == dataContractDictionaries.Length)
        {
            Array.Resize(ref dataContractDictionaries, dataContractDictionaries.Length * 2);
        }
 
        dataContractDictionaries[_count++] = dataContractDictionary;
    }
 
    internal void Pop()
    {
        _count--;
    }
 
    internal DataContract? GetDataContract(XmlQualifiedName qname)
    {
        for (var i = _count - 1; i >= 0; i--)
        {
            var dataContractDictionary = dataContractDictionaries[i];
            if (dataContractDictionary.TryGetValue(qname, out var dataContract))
                return dataContract;
        }
        return null;
    }
}