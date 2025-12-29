// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Xml;

namespace Marius.DataContracts.Runtime;

public sealed class ExtensionDataObject
{
    internal ExtensionDataObject()
    {
    }

    internal IList<ExtensionDataMember>? Members { get; set; }
}

public sealed class ExtensionDataMember
{
    public ExtensionDataMember(string name, string ns)
    {
        Name = name;
        Namespace = ns;
    }

    public string Name { get; }

    public string Namespace { get; }

    public IDataNode? Value { get; set; }

    public int MemberIndex { get; set; }
}

public interface IDataNode
{
    Type DataType { get; }
    object? Value { get; set; } // boxes for primitives
    string? DataContractName { get; set; }
    string? DataContractNamespace { get; set; }
    string? ClrTypeName { get; set; }
    string? ClrAssemblyName { get; set; }
    string Id { get; set; }
    bool PreservesReferences { get; }

    // NOTE: consider moving below APIs to DataNode<T> if IDataNode API is made public
    void GetData(ElementData element);
    bool IsFinalValue { get; set; }
    void Clear();
}

public class DataNode<T> : IDataNode
{
    protected Type dataType;
    private T _value = default!;
    private bool _isFinalValue;

    internal DataNode()
    {
        dataType = typeof(T);
        _isFinalValue = true;
    }

    internal DataNode(T value)
        : this()
    {
        _value = value;
    }

    public Type DataType => dataType;

    public object? Value
    {
        get => _value;
        set => _value = (T)value!;
    }

    bool IDataNode.IsFinalValue
    {
        get => _isFinalValue;
        set => _isFinalValue = value;
    }

    public T GetValue()
    {
        return _value;
    }

    public string? DataContractName { get; set; }

    public string? DataContractNamespace { get; set; }

    public string? ClrTypeName { get; set; }

    public string? ClrAssemblyName { get; set; }

    public bool PreservesReferences => Id != Globals.NewObjectId;

    public string Id { get; set; } = Globals.NewObjectId;

    public virtual void GetData(ElementData element)
    {
        element.dataNode = this;
        element.attributeCount = 0;
        element.childElementIndex = 0;

        if (DataContractName != null)
            AddQualifiedNameAttribute(element, Globals.XsiPrefix, Globals.XsiTypeLocalName, Globals.SchemaInstanceNamespace, DataContractName, DataContractNamespace);
        if (ClrTypeName != null)
            element.AddAttribute(Globals.SerPrefix, Globals.SerializationNamespace, Globals.ClrTypeLocalName, ClrTypeName);
        if (ClrAssemblyName != null)
            element.AddAttribute(Globals.SerPrefix, Globals.SerializationNamespace, Globals.ClrAssemblyLocalName, ClrAssemblyName);
    }

    public virtual void Clear()
    {
        // dataContractName not cleared because it is used when re-serializing from unknown data
        ClrTypeName = ClrAssemblyName = null;
    }

    internal static void AddQualifiedNameAttribute(ElementData element, string elementPrefix, string elementName, string elementNs, string valueName, string? valueNs)
    {
        var prefix = ExtensionDataReader.GetPrefix(valueNs);
        element.AddAttribute(elementPrefix, elementNs, elementName, prefix + ":" + valueName);

        var prefixDeclaredOnElement = false;
        if (element.attributes != null)
        {
            for (var i = 0; i < element.attributes.Length; i++)
            {
                var attribute = element.attributes[i];
                if (attribute != null! && attribute.prefix == Globals.XmlnsPrefix && attribute.localName == prefix)
                {
                    prefixDeclaredOnElement = true;
                    break;
                }
            }
        }

        if (!prefixDeclaredOnElement)
            element.AddAttribute(Globals.XmlnsPrefix, Globals.XmlnsNamespace, prefix, valueNs);
    }
}

public sealed class ClassDataNode : DataNode<object>
{
    internal ClassDataNode()
    {
        dataType = Globals.TypeOfClassDataNode;
    }

    internal IList<ExtensionDataMember>? Members { get; set; }

    public override void Clear()
    {
        base.Clear();
        Members = null;
    }
}

public sealed class XmlDataNode : DataNode<object>
{
    internal XmlDataNode()
    {
        dataType = Globals.TypeOfXmlDataNode;
    }

    internal IList<XmlAttribute>? XmlAttributes { get; set; }

    internal IList<XmlNode>? XmlChildNodes { get; set; }

    internal XmlDocument? OwnerDocument { get; set; }

    public override void Clear()
    {
        base.Clear();
        XmlAttributes = null;
        XmlChildNodes = null;
        OwnerDocument = null;
    }
}

public sealed class CollectionDataNode : DataNode<Array>
{
    internal CollectionDataNode()
    {
        dataType = Globals.TypeOfCollectionDataNode;
    }

    internal IList<IDataNode?>? Items { get; set; }

    internal string? ItemName { get; set; }

    internal string? ItemNamespace { get; set; }

    internal int Size { get; set; } = -1;

    public override void GetData(ElementData element)
    {
        base.GetData(element);

        element.AddAttribute(Globals.SerPrefix, Globals.SerializationNamespace, Globals.ArraySizeLocalName, Size.ToString(NumberFormatInfo.InvariantInfo));
    }

    public override void Clear()
    {
        base.Clear();
        Items = null;
        Size = -1;
    }
}

public sealed class ISerializableDataNode : DataNode<object>
{
    internal ISerializableDataNode()
    {
        dataType = Globals.TypeOfISerializableDataNode;
    }

    internal string? FactoryTypeName { get; set; }

    internal string? FactoryTypeNamespace { get; set; }

    internal IList<ISerializableDataMember>? Members { get; set; }

    public override void GetData(ElementData element)
    {
        base.GetData(element);

        if (FactoryTypeName != null)
            AddQualifiedNameAttribute(element, Globals.SerPrefix, Globals.ISerializableFactoryTypeLocalName, Globals.SerializationNamespace, FactoryTypeName, FactoryTypeNamespace);
    }

    public override void Clear()
    {
        base.Clear();
        Members = null;
        FactoryTypeName = FactoryTypeNamespace = null;
    }
}

public sealed class ISerializableDataMember
{
    public ISerializableDataMember(string name)
    {
        Name = name;
    }

    internal string Name { get; }

    internal IDataNode? Value { get; set; }
}

public sealed class ElementData
{
    public string? localName;
    public string? ns;
    public string? prefix;
    public int attributeCount;
    public AttributeData[]? attributes;
    public IDataNode? dataNode;
    public int childElementIndex;

    public void AddAttribute(string prefix, string ns, string name, string? value)
    {
        GrowAttributesIfNeeded();
        var attribute = attributes[attributeCount] ??= new AttributeData();
        attribute.prefix = prefix;
        attribute.ns = ns;
        attribute.localName = name;
        attribute.value = value;
        attributeCount++;
    }

    [MemberNotNull(nameof(attributes))]
    private void GrowAttributesIfNeeded()
    {
        if (attributes == null)
        {
            attributes = new AttributeData[4];
        }
        else if (attributes.Length == attributeCount)
        {
            var newAttributes = new AttributeData[attributes.Length * 2];
            Array.Copy(attributes, newAttributes, attributes.Length);
            attributes = newAttributes;
        }
    }
}

public sealed class AttributeData
{
    public string? prefix;
    public string? ns;
    public string? localName;
    public string? value;
}