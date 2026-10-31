using System.Xml;

namespace Marius.DataContracts.Runtime;

public sealed class Attributes
{
    private static readonly XmlDictionaryString[] SerializationLocalNames = new XmlDictionaryString[]
    {
        DictionaryGlobals.IdLocalName,
        DictionaryGlobals.ArraySizeLocalName,
        DictionaryGlobals.RefLocalName,
        DictionaryGlobals.ClrTypeLocalName,
        DictionaryGlobals.ClrAssemblyLocalName,
        DictionaryGlobals.ISerializableFactoryTypeLocalName,
    };

    private static readonly XmlDictionaryString[] SchemaInstanceLocalNames = new XmlDictionaryString[]
    {
        DictionaryGlobals.XsiNilLocalName,
        DictionaryGlobals.XsiTypeLocalName,
    };

    internal string Id = null!; // initialized in Reset
    internal string Ref = null!; // initialized in Reset
    internal string? XsiTypeName;
    internal string? XsiTypeNamespace;
    internal string? XsiTypePrefix;
    internal bool XsiNil;
    internal string? ClrAssembly;
    internal string? ClrType;
    internal int ArraySZSize;
    internal string? FactoryTypeName;
    internal string? FactoryTypeNamespace;
    internal string? FactoryTypePrefix;
    internal bool UnrecognizedAttributesFound;

    // A parsed element can outlive user-controlled surrogate/contract selection.
    // Keep its complete state on the stack while a nested read uses this object.
    internal readonly struct Snapshot
    {
        private readonly string _id;
        private readonly string _ref;
        private readonly string? _xsiTypeName;
        private readonly string? _xsiTypeNamespace;
        private readonly string? _xsiTypePrefix;
        private readonly bool _xsiNil;
        private readonly string? _clrAssembly;
        private readonly string? _clrType;
        private readonly int _arraySZSize;
        private readonly string? _factoryTypeName;
        private readonly string? _factoryTypeNamespace;
        private readonly string? _factoryTypePrefix;
        private readonly bool _unrecognizedAttributesFound;

        internal Snapshot(Attributes attributes)
        {
            _id = attributes.Id;
            _ref = attributes.Ref;
            _xsiTypeName = attributes.XsiTypeName;
            _xsiTypeNamespace = attributes.XsiTypeNamespace;
            _xsiTypePrefix = attributes.XsiTypePrefix;
            _xsiNil = attributes.XsiNil;
            _clrAssembly = attributes.ClrAssembly;
            _clrType = attributes.ClrType;
            _arraySZSize = attributes.ArraySZSize;
            _factoryTypeName = attributes.FactoryTypeName;
            _factoryTypeNamespace = attributes.FactoryTypeNamespace;
            _factoryTypePrefix = attributes.FactoryTypePrefix;
            _unrecognizedAttributesFound = attributes.UnrecognizedAttributesFound;
        }

        internal void Restore(Attributes attributes)
        {
            attributes.Id = _id;
            attributes.Ref = _ref;
            attributes.XsiTypeName = _xsiTypeName;
            attributes.XsiTypeNamespace = _xsiTypeNamespace;
            attributes.XsiTypePrefix = _xsiTypePrefix;
            attributes.XsiNil = _xsiNil;
            attributes.ClrAssembly = _clrAssembly;
            attributes.ClrType = _clrType;
            attributes.ArraySZSize = _arraySZSize;
            attributes.FactoryTypeName = _factoryTypeName;
            attributes.FactoryTypeNamespace = _factoryTypeNamespace;
            attributes.FactoryTypePrefix = _factoryTypePrefix;
            attributes.UnrecognizedAttributesFound = _unrecognizedAttributesFound;
        }
    }

    internal void Read(XmlReaderDelegator reader)
    {
        Reset();

        while (reader.MoveToNextAttribute())
        {
            switch (reader.IndexOfLocalName(SerializationLocalNames, DictionaryGlobals.SerializationNamespace))
            {
                case 0:
                    ReadId(reader);
                    break;
                case 1:
                    ReadArraySize(reader);
                    break;
                case 2:
                    ReadRef(reader);
                    break;
                case 3:
                    ClrType = reader.Value;
                    break;
                case 4:
                    ClrAssembly = reader.Value;
                    break;
                case 5:
                    ReadFactoryType(reader);
                    break;
                default:
                    switch (reader.IndexOfLocalName(SchemaInstanceLocalNames, DictionaryGlobals.SchemaInstanceNamespace))
                    {
                        case 0:
                            ReadXsiNil(reader);
                            break;
                        case 1:
                            ReadXsiType(reader);
                            break;
                        default:
                            if (!reader.IsNamespaceUri(DictionaryGlobals.XmlnsNamespace))
                                UnrecognizedAttributesFound = true;
                            break;
                    }

                    break;
            }
        }

        reader.MoveToElement();
    }

    internal void Reset()
    {
        Id = Globals.NewObjectId;
        Ref = Globals.NewObjectId;
        XsiTypeName = null;
        XsiTypeNamespace = null;
        XsiTypePrefix = null;
        XsiNil = false;
        ClrAssembly = null;
        ClrType = null;
        ArraySZSize = -1;
        FactoryTypeName = null;
        FactoryTypeNamespace = null;
        FactoryTypePrefix = null;
        UnrecognizedAttributesFound = false;
    }

    private void ReadId(XmlReaderDelegator reader)
    {
        Id = reader.ReadContentAsString();
        if (string.IsNullOrEmpty(Id))
        {
            throw XmlObjectSerializer.CreateSerializationException(SR.Format(SR.InvalidXsIdDefinition, Id));
        }
    }

    private void ReadRef(XmlReaderDelegator reader)
    {
        Ref = reader.ReadContentAsString();
        if (string.IsNullOrEmpty(Ref))
        {
            throw XmlObjectSerializer.CreateSerializationException(SR.Format(SR.InvalidXsRefDefinition, Ref));
        }
    }

    private void ReadXsiNil(XmlReaderDelegator reader)
    {
        XsiNil = reader.ReadContentAsBoolean();
    }

    private void ReadArraySize(XmlReaderDelegator reader)
    {
        ArraySZSize = reader.ReadContentAsInt();
        if (ArraySZSize < 0)
            throw XmlObjectSerializer.CreateSerializationException(SR.Format(SR.InvalidSizeDefinition, ArraySZSize));
    }

    private void ReadXsiType(XmlReaderDelegator reader)
    {
        var xsiTypeString = reader.Value;
        if (!string.IsNullOrEmpty(xsiTypeString))
            XmlObjectSerializerReadContext.ParseQualifiedName(xsiTypeString, reader, out XsiTypeName, out XsiTypeNamespace, out XsiTypePrefix);
    }

    private void ReadFactoryType(XmlReaderDelegator reader)
    {
        var factoryTypeString = reader.Value;
        if (!string.IsNullOrEmpty(factoryTypeString))
            XmlObjectSerializerReadContext.ParseQualifiedName(factoryTypeString, reader, out FactoryTypeName, out FactoryTypeNamespace, out FactoryTypePrefix);
    }
}