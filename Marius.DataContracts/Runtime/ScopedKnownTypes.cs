// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license

using System.Runtime.CompilerServices;
using System.Xml;
using DataContractDictionary = System.Collections.Frozen.FrozenDictionary<System.Xml.XmlQualifiedName, Marius.DataContracts.Runtime.DataContract>;

namespace Marius.DataContracts.Runtime;

public struct ScopedKnownTypes
{
    private const int InlineLength = 2;

    private int _count;

    private InlineDataContracts _inlineDataContractDictionaries;
    private DataContractDictionary[]? _dataContractDictionaries;

    internal void Push(DataContractDictionary dataContractDictionary)
    {
        Span<DataContractDictionary> span = _inlineDataContractDictionaries;
        if (_count < InlineLength && _dataContractDictionaries == null)
        {
            span[_count++] = dataContractDictionary;
            return;
        }

        if (_dataContractDictionaries == null)
        {
            _dataContractDictionaries = new DataContractDictionary[8];
            span.CopyTo(_dataContractDictionaries);
            span.Clear();
        }
        else if (_count == _dataContractDictionaries.Length)
        {
            Array.Resize(ref _dataContractDictionaries, _dataContractDictionaries.Length * 2);
        }

        _dataContractDictionaries[_count++] = dataContractDictionary;
    }

    internal void Pop()
    {
        _count--;
    }

    internal DataContract? GetDataContract(XmlQualifiedName qname)
    {
        Span<DataContractDictionary> span = _inlineDataContractDictionaries;
        if (_dataContractDictionaries != null)
            span = _dataContractDictionaries;

        for (var i = _count - 1; i >= 0; i--)
        {
            var dataContractDictionary = span[i];
            if (dataContractDictionary.TryGetValue(qname, out var dataContract))
                return dataContract;
        }

        return null;
    }

    [InlineArray(InlineLength)]
    private struct InlineDataContracts
    {
        private DataContractDictionary _element;
    }
}