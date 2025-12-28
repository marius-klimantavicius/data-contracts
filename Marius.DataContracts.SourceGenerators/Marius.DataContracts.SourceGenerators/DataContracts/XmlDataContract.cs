using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;
using Microsoft.CodeAnalysis;

namespace Marius.DataContracts.SourceGenerators.DataContracts;

internal class XmlDataContract : DataContract
{
    private new XmlDataContractModel Model => Unsafe.As<XmlDataContractModel>(base.Model);

    public override bool IsBuiltInDataContract => SymbolEqualityComparer.Default.Equals(Context.KnownSymbols.XmlElementType, UnderlyingType) ||
        SymbolEqualityComparer.Default.Equals(Context.KnownSymbols.XmlNodeArrayType, UnderlyingType);

    public override bool CanContainReferences => false;

    public new bool HasRoot => Model.HasRoot;
    public new bool IsValueType => Model.IsValueType;
    public bool IsAny => Model.IsAny;
    public string? SchemaProviderMethod => Model.SchemaProviderMethod;
    public XmlRootAttribute? XmlRootAttribute => Model.XmlRootAttribute;

    public XmlDataContract(DataContractContext context, ITypeSymbol type)
        : base(new XmlDataContractModel(context, type))
    {
    }

    private sealed class XmlDataContractModel : DataContractModel
    {
        private bool _isKnownTypeAttributeChecked;
        private ImmutableArray<DataContract> _knownDataContracts = ImmutableArray<DataContract>.Empty;

        public XmlSchemaType? XsdType { get; set; }
        public bool IsAny { get; set; }
        public string? SchemaProviderMethod { get; set; }
        public XmlRootAttribute? XmlRootAttribute { get; set; }

        public bool IsAnonymous => XsdType != null;

        public bool IsTopLevelElementNullable { get; set; }
        public bool IsTypeDefinedOnImport { get; set; }

        public override string? TopLevelElementName { get; set; }
        public override string? TopLevelElementNamespace { get; set; }

        public override ImmutableArray<DataContract> KnownDataContracts
        {
            get
            {
                if (!_isKnownTypeAttributeChecked)
                {
                    if (!_isKnownTypeAttributeChecked)
                    {
                        _knownDataContracts = Context.ImportKnownTypeAttributes(UnderlyingType);
                        _isKnownTypeAttributeChecked = true;
                    }
                }

                return _knownDataContracts;
            }

            set => _knownDataContracts = value;
        }

        public XmlDataContractModel(DataContractContext context, ITypeSymbol type)
            : base(context, type)
        {
            if (DataContractContext.HasDataContractAttribute(type))
                throw new InvalidDataContractException(SR.Format(SR.IXmlSerializableCannotHaveDataContract, DataContractContext.GetClrTypeFullName(type)));
            if (DataContractContext.HasCollectionDataContractAttribute(type))
                throw new InvalidDataContractException(SR.Format(SR.IXmlSerializableCannotHaveCollectionDataContract, DataContractContext.GetClrTypeFullName(type)));

            GetXmlTypeInfo(type, out var xmlName, out var xsdType, out var hasRoot, out var isAny, out var methodName);
            XmlName = xmlName;
            XsdType = xsdType;
            HasRoot = hasRoot;
            Name = XmlName.Name;
            Namespace = XmlName.Namespace;
            IsAny = isAny;
            SchemaProviderMethod = methodName;
            XmlRootAttribute = DataContractContext.GetXmlRootAttribute(UnderlyingType);
        }

        internal void GetXmlTypeInfo(ITypeSymbol type, out XmlQualifiedName xmlName, out XmlSchemaType? xsdType, out bool hasRoot, out bool isAny, out string? methodName)
        {
            isAny = false;
            methodName = null;
            if (IsSpecialXmlType(type, out xmlName!, out xsdType, out hasRoot))
                return;

            InvokeSchemaProviderMethod(type, out xmlName, out xsdType, out hasRoot, out isAny, out methodName);
            if (string.IsNullOrEmpty(xmlName.Name))
                throw new InvalidDataContractException(SR.Format(SR.InvalidXmlDataContractName, DataContractContext.GetClrTypeFullName(type)));
        }

        internal bool IsSpecialXmlType(ITypeSymbol type, [NotNullWhen(true)] out XmlQualifiedName? typeName, [NotNullWhen(true)] out XmlSchemaType? xsdType, out bool hasRoot)
        {
            xsdType = null;
            hasRoot = true;

            var cmp = SymbolEqualityComparer.Default;
            var knownSymbols = Context.KnownSymbols;
            if (cmp.Equals(knownSymbols.XmlElementType, type) || cmp.Equals(knownSymbols.XmlNodeArrayType, type))
            {
                string? name;
                if (cmp.Equals(knownSymbols.XmlElementType, type))
                {
                    xsdType = CreateAnyElementType();
                    name = "XmlElement";
                    hasRoot = false;
                }
                else
                {
                    xsdType = CreateAnyType();
                    name = "ArrayOfXmlNode";
                    hasRoot = true;
                }

                typeName = new XmlQualifiedName(name, DataContractContext.GetDefaultXmlNamespace(type));
                return true;
            }

            typeName = null;
            return false;
        }

        private void InvokeSchemaProviderMethod(ITypeSymbol clrType, out XmlQualifiedName xmlName, out XmlSchemaType? xsdType, out bool hasRoot, out bool isAny, out string? methodName)
        {
            xsdType = null;
            hasRoot = true;
            xmlName = Context.GetDefaultXmlName(clrType);
            isAny = false;
            methodName = null;

            var provider = DataContractContext.GetXmlSchemaProviderAttribute(clrType);
            if (provider == null)
                return;

            isAny = provider.IsAny;
            if (provider.IsAny)
            {
                xsdType = CreateAnyElementType();
                hasRoot = false;
            }

            methodName = provider.MethodName;
            if (string.IsNullOrEmpty(methodName))
            {
                if (!provider.IsAny)
                    throw new InvalidDataContractException(SR.Format(SR.InvalidGetSchemaMethod, DataContractContext.GetClrTypeFullName(clrType)));
            }
        }

        private XmlSchemaComplexType CreateAnyElementType()
        {
            var anyElementType = new XmlSchemaComplexType
            {
                IsMixed = false,
                Particle = new XmlSchemaSequence(),
            };
            var any = new XmlSchemaAny
            {
                MinOccurs = 0,
                ProcessContents = XmlSchemaContentProcessing.Lax,
            };
            ((XmlSchemaSequence)anyElementType.Particle).Items.Add(any);
            return anyElementType;
        }

        private XmlSchemaComplexType CreateAnyType()
        {
            var anyType = new XmlSchemaComplexType
            {
                IsMixed = true,
                Particle = new XmlSchemaSequence(),
            };
            var any = new XmlSchemaAny
            {
                MinOccurs = 0,
                MaxOccurs = decimal.MaxValue,
                ProcessContents = XmlSchemaContentProcessing.Lax,
            };
            ((XmlSchemaSequence)anyType.Particle).Items.Add(any);
            anyType.AnyAttribute = new XmlSchemaAnyAttribute();
            return anyType;
        }
    }
}