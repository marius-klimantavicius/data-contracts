using System.Xml;

namespace Marius.DataContracts.SourceGenerators.DataContracts;

internal sealed class ReflectionXmlCollectionReader
{
    private readonly ReflectionReader _reflectionReader = new ReflectionXmlReader();

    public object ReflectionReadCollection(XmlReaderDelegator xmlReader, XmlObjectSerializerReadContext context, XmlDictionaryString itemName, XmlDictionaryString itemNamespace, CollectionDataContract collectionContract)
    {
        return _reflectionReader.ReflectionReadCollection(xmlReader, context, itemName, itemNamespace /*itemNamespace*/, collectionContract);
    }

    public void ReflectionReadGetOnlyCollection(XmlReaderDelegator xmlReader, XmlObjectSerializerReadContext context, XmlDictionaryString itemName, XmlDictionaryString itemNs, CollectionDataContract collectionContract)
    {
        _reflectionReader.ReflectionReadGetOnlyCollection(xmlReader, context, itemName, itemNs, collectionContract);
    }
}