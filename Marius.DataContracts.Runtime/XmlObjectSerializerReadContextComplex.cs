// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Runtime.Serialization;

namespace Marius.DataContracts.Runtime;

public class XmlObjectSerializerReadContextComplex : XmlObjectSerializerReadContext
{
    private readonly bool _preserveObjectReferences;

    public XmlObjectSerializerReadContextComplex(DataContractSerializer serializer, DataContract rootTypeDataContract)
        : base(serializer, rootTypeDataContract)
    {
        _preserveObjectReferences = serializer.PreserveObjectReferences;
    }

    public XmlObjectSerializerReadContextComplex(XmlObjectSerializer serializer, int maxItemsInObjectGraph, StreamingContext streamingContext, bool ignoreExtensionDataObject)
        : base(serializer, maxItemsInObjectGraph, streamingContext, ignoreExtensionDataObject)
    {
    }

    public override int GetArraySize()
    {
        Debug.Assert(attributes != null);

        return _preserveObjectReferences ? attributes.ArraySZSize : -1;
    }
}