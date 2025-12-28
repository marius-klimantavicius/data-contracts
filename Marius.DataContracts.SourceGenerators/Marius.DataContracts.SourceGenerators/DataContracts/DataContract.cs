using System.Collections.Immutable;
using System.Xml;
using Microsoft.CodeAnalysis;

namespace Marius.DataContracts.SourceGenerators.DataContracts;

public abstract partial class DataContract
{
    internal DataContractModel Model { get; }
    internal DataContractContext Context => Model.Context;

    public string Name { get; }
    public string Namespace { get; }

    public ITypeSymbol UnderlyingType => Model.UnderlyingType;
    public ITypeSymbol OriginalUnderlyingType => Model.OriginalUnderlyingType;
    public bool IsReference => Model.IsReference;
    public bool IsValueType => Model.IsValueType;
    public bool IsISerializable => Model.IsISerializable;
    public bool HasRoot => Model.HasRoot;

    public virtual DataContract? BaseContract => null;
    public virtual bool IsPrimitive => false;
    public virtual bool CanContainReferences => true;
    public virtual bool IsBuiltInDataContract => false;
    
    public string? TopLevelElementName => Model.TopLevelElementName;
    public string? TopLevelElementNamespace => Model.TopLevelElementNamespace;
    
    public virtual ImmutableArray<DataContract> KnownDataContracts
    {
        get => Model.KnownDataContracts;
        set => Model.KnownDataContracts = value;
    }

    public XmlQualifiedName XmlName
    {
        get => Model.XmlName;
        internal set => Model.XmlName = value;
    }

    internal DataContract(DataContractModel model)
    {
        Model = model;
        Name = model.Name;
        Namespace = model.Namespace;
    }

    internal virtual DataContract GetValidContract(bool verifyConstructor = false)
    {
        return this;
    }
}