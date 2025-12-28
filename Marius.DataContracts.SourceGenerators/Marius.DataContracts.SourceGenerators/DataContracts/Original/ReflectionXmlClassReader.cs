using System.Diagnostics;
using System.Xml;

namespace Marius.DataContracts.SourceGenerators.DataContracts;

internal sealed class ReflectionXmlClassReader
{
    private readonly ClassDataContract _classContract;
    private readonly ReflectionReader _reflectionReader;

    public ReflectionXmlClassReader(ClassDataContract classDataContract)
    {
        Debug.Assert(classDataContract != null);
        _classContract = classDataContract;
        _reflectionReader = new ReflectionXmlReader();
    }

    public object ReflectionReadClass(XmlReaderDelegator xmlReader, XmlObjectSerializerReadContext? context, XmlDictionaryString[]? memberNames, XmlDictionaryString[]? memberNamespaces)
    {
        return _reflectionReader.ReflectionReadClass(xmlReader, context, memberNames, memberNamespaces, _classContract);
    }
}