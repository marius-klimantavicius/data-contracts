using System.Xml.Serialization;

namespace Marius.DataContracts.Runtime;

public class XmlDataContract : DataContract
{
    public required Func<IXmlSerializable> Create { get; init; }
}

public class XmlDataContract<T> : XmlDataContract
{
    public required Func<XmlReaderDelegator, XmlObjectSerializerReadContext, T> Read { get; init; }

    public override object? ReadXmlValue(XmlReaderDelegator xmlReader, XmlObjectSerializerReadContext? context)
    {
        object? o;
        if (context == null)
        {
            o = XmlObjectSerializerReadContext.ReadRootIXmlSerializable(xmlReader, this, true /*isMemberType*/);
        }
        else
        {
            o = context.ReadIXmlSerializable(xmlReader, this, true /*isMemberType*/);
            context.AddNewObject(o);
        }

        xmlReader.ReadEndElement();
        return o;
    }

    public override void WriteXmlValue(XmlWriterDelegator xmlWriter, object obj, XmlObjectSerializerWriteContext? context)
    {
        if (context == null)
            XmlObjectSerializerWriteContext.WriteRootIXmlSerializable(xmlWriter, obj);
        else
            context.WriteIXmlSerializable(xmlWriter, obj);
    }
}