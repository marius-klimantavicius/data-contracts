using Microsoft.CodeAnalysis;

namespace Marius.DataContracts.SourceGenerators.DataContracts;

internal abstract class PrimitiveDataContract : DataContract
{
    public override bool IsPrimitive => true;
    public override bool IsBuiltInDataContract => true;
    public override bool CanContainReferences => false;

    public abstract string WriteMethodName { get; }
    public abstract string ReadMethodName { get; }

    protected PrimitiveDataContract(DataContractContext context, ITypeSymbol type, string name, string ns)
        : base(new PrimitiveDataContractModel(context, type, name, ns))
    {
    }

    private sealed class PrimitiveDataContractModel : DataContractModel
    {
        public PrimitiveDataContractModel(DataContractContext context, ITypeSymbol type, string name, string ns)
            : base(context, type)
        {
            SetDataContractName(name, ns);
        }
    }
}

internal class BooleanDataContract : PrimitiveDataContract
{
    public const string LocalName = "boolean";

    public override string WriteMethodName => "WriteBoolean";
    public override string ReadMethodName => "ReadElementContentAsBoolean";

    public BooleanDataContract(DataContractContext context)
        : base(context, context.KnownSymbols.BooleanType, LocalName, DataContractContext.SchemaNamespace)
    {
    }
}

internal class UnsignedByteDataContract : PrimitiveDataContract
{
    public const string LocalName = "unsignedByte";

    public override string WriteMethodName => "WriteUnsignedByte";
    public override string ReadMethodName => "ReadElementContentAsUnsignedByte";

    public UnsignedByteDataContract(DataContractContext context)
        : base(context, context.KnownSymbols.ByteType, LocalName, DataContractContext.SchemaNamespace)
    {
    }
}

internal class CharDataContract : PrimitiveDataContract
{
    public const string LocalName = "char";

    public override string WriteMethodName => "WriteChar";
    public override string ReadMethodName => "ReadElementContentAsChar";

    public CharDataContract(DataContractContext context)
        : base(context, context.KnownSymbols.CharType, LocalName, DataContractContext.SerializationNamespace)
    {
    }
}

internal class DateTimeDataContract : PrimitiveDataContract
{
    public const string LocalName = "dateTime";

    public override string WriteMethodName => "WriteDateTime";
    public override string ReadMethodName => "ReadElementContentAsDateTime";
    
    public DateTimeDataContract(DataContractContext context)
        : base(context, context.KnownSymbols.DateTimeType, LocalName, DataContractContext.SchemaNamespace)
    {
    }
}

internal class DecimalDataContract : PrimitiveDataContract
{
    public const string LocalName = "decimal";

    public override string WriteMethodName => "WriteDecimal";
    public override string ReadMethodName => "ReadElementContentAsDecimal";
    
    public DecimalDataContract(DataContractContext context)
        : base(context, context.KnownSymbols.DecimalType, LocalName, DataContractContext.SchemaNamespace)
    {
    }
}

internal class DoubleDataContract : PrimitiveDataContract
{
    public const string LocalName = "double";

    public override string WriteMethodName => "WriteDouble";
    public override string ReadMethodName => "ReadElementContentAsDouble";
    
    public DoubleDataContract(DataContractContext context)
        : base(context, context.KnownSymbols.DoubleType, LocalName, DataContractContext.SchemaNamespace)
    {
    }
}

internal class ShortDataContract : PrimitiveDataContract
{
    public const string LocalName = "short";

    public override string WriteMethodName => "WriteShort";
    public override string ReadMethodName => "ReadElementContentAsShort";

    public ShortDataContract(DataContractContext context)
        : base(context, context.KnownSymbols.Int16Type, LocalName, DataContractContext.SchemaNamespace)
    {
    }
}

internal class IntDataContract : PrimitiveDataContract
{
    public const string LocalName = "int";

    public override string WriteMethodName => "WriteInt";
    public override string ReadMethodName => "ReadElementContentAsInt";

    public IntDataContract(DataContractContext context)
        : base(context, context.KnownSymbols.Int32Type, LocalName, DataContractContext.SchemaNamespace)
    {
    }
}

internal class LongDataContract : PrimitiveDataContract
{
    public const string LocalName = "long";

    public override string WriteMethodName => "WriteLong";
    public override string ReadMethodName => "ReadElementContentAsLong";

    public LongDataContract(DataContractContext context)
        : base(context, context.KnownSymbols.Int64Type, LocalName, DataContractContext.SchemaNamespace)
    {
    }
}

internal class SignedByteDataContract : PrimitiveDataContract
{
    public const string LocalName = "byte";

    public override string WriteMethodName => "WriteSignedByte";
    public override string ReadMethodName => "ReadElementContentAsSignedByte";

    public SignedByteDataContract(DataContractContext context)
        : base(context, context.KnownSymbols.SByteType, LocalName, DataContractContext.SchemaNamespace)
    {
    }
}

internal class FloatDataContract : PrimitiveDataContract
{
    public const string LocalName = "float";

    public override string WriteMethodName => "WriteFloat";
    public override string ReadMethodName => "ReadElementContentAsFloat";
    
    public FloatDataContract(DataContractContext context)
        : base(context, context.KnownSymbols.SingleType, LocalName, DataContractContext.SchemaNamespace)
    {
    }
}

internal class StringDataContract : PrimitiveDataContract
{
    public const string LocalName = "string";

    public override string WriteMethodName => "WriteString";
    public override string ReadMethodName => "ReadElementContentAsString";
    
    public StringDataContract(DataContractContext context)
        : base(context, context.KnownSymbols.StringType, LocalName, DataContractContext.SchemaNamespace)
    {
    }
}

internal class UnsignedShortDataContract : PrimitiveDataContract
{
    public const string LocalName = "unsignedShort";

    public override string WriteMethodName => "WriteUnsignedShort";
    public override string ReadMethodName => "ReadElementContentAsUnsignedShort";

    public UnsignedShortDataContract(DataContractContext context)
        : base(context, context.KnownSymbols.UInt16Type, LocalName, DataContractContext.SchemaNamespace)
    {
    }
}

internal class UnsignedIntDataContract : PrimitiveDataContract
{
    public const string LocalName = "unsignedInt";

    public override string WriteMethodName => "WriteUnsignedInt";
    public override string ReadMethodName => "ReadElementContentAsUnsignedInt";

    public UnsignedIntDataContract(DataContractContext context)
        : base(context, context.KnownSymbols.UInt32Type, LocalName, DataContractContext.SchemaNamespace)
    {
    }
}

internal class UnsignedLongDataContract : PrimitiveDataContract
{
    public const string LocalName = "unsignedLong";

    public override string WriteMethodName => "WriteUnsignedLong";
    public override string ReadMethodName => "ReadElementContentAsUnsignedLong";

    public UnsignedLongDataContract(DataContractContext context)
        : base(context, context.KnownSymbols.UInt64Type, LocalName, DataContractContext.SchemaNamespace)
    {
    }
}

internal class ObjectDataContract : PrimitiveDataContract
{
    public const string LocalName = "anyType";

    public override bool IsPrimitive => false;

    public override string WriteMethodName => "WriteAnyType";
    public override string ReadMethodName => "ReadElementContentAsAnyType";
    
    public ObjectDataContract(DataContractContext context)
        : base(context, context.KnownSymbols.ObjectType, LocalName, DataContractContext.SchemaNamespace)
    {
    }
}

internal class InterfaceDataContract : ObjectDataContract
{
    public ITypeSymbol InterfaceType { get; }

    public InterfaceDataContract(DataContractContext context, ITypeSymbol interfaceType)
        : base(context)
    {
        InterfaceType = interfaceType;
    }
}

internal class ByteArrayDataContract : PrimitiveDataContract
{
    public const string LocalName = "base64Binary";

    public override string WriteMethodName => "WriteBase64";
    public override string ReadMethodName => "ReadElementContentAsBase64";
    
    public ByteArrayDataContract(DataContractContext context)
        : base(context, context.KnownSymbols.ByteArrayType!, LocalName, DataContractContext.SchemaNamespace)
    {
    }
}

internal class UriDataContract : PrimitiveDataContract
{
    public const string LocalName = "anyURI";

    public override string WriteMethodName => "WriteUri";
    public override string ReadMethodName => "ReadElementContentAsUri";
    
    public UriDataContract(DataContractContext context)
        : base(context, context.KnownSymbols.UriType!, LocalName, DataContractContext.SchemaNamespace)
    {
    }
}

internal class QNameDataContract : PrimitiveDataContract
{
    public const string LocalName = "QName";

    public override string WriteMethodName => "WriteQName";
    public override string ReadMethodName => "ReadElementContentAsQName";
    
    public QNameDataContract(DataContractContext context)
        : base(context, context.KnownSymbols.XmlQualifiedNameType!, LocalName, DataContractContext.SchemaNamespace)
    {
    }
}

internal class TimeSpanDataContract : PrimitiveDataContract
{
    public const string LocalName = "duration";

    public override string WriteMethodName => "WriteTimeSpan";
    public override string ReadMethodName => "ReadElementContentAsTimeSpan";
    
    public TimeSpanDataContract(DataContractContext context)
        : base(context, context.KnownSymbols.TimeSpanType!, LocalName, DataContractContext.SerializationNamespace)
    {
    }
}

internal class GuidDataContract : PrimitiveDataContract
{
    public const string LocalName = "guid";

    public override string WriteMethodName => "WriteGuid";
    public override string ReadMethodName => "ReadElementContentAsGuid";
    
    public GuidDataContract(DataContractContext context)
        : base(context, context.KnownSymbols.GuidType!, LocalName, DataContractContext.SerializationNamespace)
    {
    }
}

internal class DateOnlyDataContract : PrimitiveDataContract
{
    public const string LocalName = "dateOnly";

    public override string WriteMethodName => "WriteDateOnly";
    public override string ReadMethodName => "ReadElementContentAsDateOnly";
    
    public DateOnlyDataContract(DataContractContext context)
        : base(context, context.KnownSymbols.DateOnlyType!, LocalName, DataContractContext.SerializationNamespace)
    {
    }
}

internal class TimeOnlyDataContract : PrimitiveDataContract
{
    public const string LocalName = "timeOnly";

    public override string WriteMethodName => "WriteTimeOnly";
    public override string ReadMethodName => "ReadElementContentAsTimeOnly";
    
    public TimeOnlyDataContract(DataContractContext context)
        : base(context, context.KnownSymbols.TimeOnlyType!, LocalName, DataContractContext.SerializationNamespace)
    {
    }
}

internal class NullPrimitiveDataContract : PrimitiveDataContract
{
    public const string LocalName = "null";

    public override string WriteMethodName => throw new NotSupportedException();
    public override string ReadMethodName => throw new NotSupportedException();

    public NullPrimitiveDataContract(DataContractContext context)
        : base(context, context.KnownSymbols.ObjectType, LocalName, "")
    {
    }
}