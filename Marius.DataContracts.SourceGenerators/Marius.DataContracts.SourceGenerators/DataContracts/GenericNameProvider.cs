using System.Collections.Immutable;
using System.Text;
using System.Xml;
using Microsoft.CodeAnalysis;

namespace Marius.DataContracts.SourceGenerators.DataContracts;

internal sealed class GenericNameProvider : IGenericNameProvider
{
    private readonly DataContractContext _context;
    private readonly string _genericTypeName;
    private readonly object[] _genericParams; //Type or DataContract
    private readonly IList<int> _nestedParamCounts;

    internal GenericNameProvider(DataContractContext context, INamedTypeSymbol type)
        : this(context, DataContractContext.GetClrTypeFullName(type.ConstructedFrom), type.TypeParameters)
    {
    }

    internal GenericNameProvider(DataContractContext context, string genericTypeName, ImmutableArray<ITypeParameterSymbol> genericParams)
    {
        _context = context;
        _genericTypeName = genericTypeName;
        _genericParams = new object[genericParams.Length];
        genericParams.As<object>().CopyTo(_genericParams);

        DataContractContext.GetClrNameAndNamespace(genericTypeName, out var name, out _);
        _nestedParamCounts = DataContractContext.GetDataContractNameForGenericName(name, null);
    }

    public int GetParameterCount()
    {
        return _genericParams.Length;
    }

    public IList<int> GetNestedParameterCounts()
    {
        return _nestedParamCounts;
    }

    public string GetParameterName(int paramIndex)
    {
        return GetXmlName(paramIndex).Name;
    }

    public string GetNamespaces()
    {
        var namespaces = new StringBuilder();
        for (var j = 0; j < GetParameterCount(); j++)
            namespaces.Append(' ').Append(GetXmlName(j).Namespace);
        return namespaces.ToString();
    }

    public string? GetGenericTypeName()
    {
        return _genericTypeName;
    }

    public bool ParametersFromBuiltInNamespaces
    {
        get
        {
            var parametersFromBuiltInNamespaces = true;
            for (var j = 0; j < GetParameterCount(); j++)
            {
                if (parametersFromBuiltInNamespaces)
                    parametersFromBuiltInNamespaces = DataContractContext.IsBuiltInNamespace(GetXmlName(j).Namespace);
                else
                    break;
            }

            return parametersFromBuiltInNamespaces;
        }
    }

    private XmlQualifiedName GetXmlName(int i)
    {
        var o = _genericParams[i];
        var qname = o as XmlQualifiedName;
        if (qname == null)
        {
            if (o is ITypeParameterSymbol paramType)
                _genericParams[i] = qname = _context.GetXmlName(paramType);
            else
                _genericParams[i] = qname = ((DataContract)o).XmlName;
        }

        return qname;
    }
}