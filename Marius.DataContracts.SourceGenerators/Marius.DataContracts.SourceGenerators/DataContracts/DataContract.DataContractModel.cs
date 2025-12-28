using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Xml;
using Microsoft.CodeAnalysis;

namespace Marius.DataContracts.SourceGenerators.DataContracts;

public abstract partial class DataContract
{
    internal abstract partial class DataContractModel
    {
        private ITypeSymbol? _originalUnderlyingType;
        
        public DataContractContext Context { get; set; }
        public ITypeSymbol UnderlyingType { get; set; }
        public string Name { get; set; } = null!;
        public string Namespace { get; set; } = null!;
        public XmlQualifiedName XmlName { get; set; } = null!;

        public bool IsReference { get; set; }
        public bool IsValueType { get; set; }
        public bool IsISerializable { get; set; }

        public bool HasRoot { get; set; } = true;

        public virtual ImmutableArray<DataContract> KnownDataContracts
        {
            get => ImmutableArray<DataContract>.Empty;
            set
            {
                // empty
            }
        }

        public ITypeSymbol OriginalUnderlyingType => _originalUnderlyingType ??= GetDataContractOriginalType(UnderlyingType);

        [DisallowNull]
        public virtual string? TopLevelElementName
        {
            get => Name;
            set
            {
                Debug.Assert(value != null);
                Name = value;
            }
        }
 
        [DisallowNull]
        public virtual string? TopLevelElementNamespace
        {
            get => Namespace;
            set
            {
                Debug.Assert(value != null);
                Namespace = value;
            }
        }
        
        protected DataContractModel(DataContractContext context, ITypeSymbol underlyingType)
        {
            Context = context;
            UnderlyingType = underlyingType;
        }

        protected void SetDataContractName(XmlQualifiedName xmlName)
        {
            Name = xmlName.Name;
            Namespace = xmlName.Namespace;
            XmlName = xmlName;
        }

        protected void SetDataContractName(string name, string ns)
        {
            Name = name;
            Namespace = ns;
            XmlName = new XmlQualifiedName(name, ns);
        }
        
        private static ITypeSymbol GetDataContractOriginalType(ITypeSymbol type)
        {
            // adapters not supported
            return type;
        }
    }
}