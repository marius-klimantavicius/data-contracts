using Microsoft.CodeAnalysis.CSharp;
using Marius.DataContracts.SourceGenerators.Specs;

namespace Marius.DataContracts.SourceGenerators;

internal class PrimitiveDataContractSpecGenerator : SpecContractGenerator
{
    public PrimitiveDataContractSpec PrimitiveContract { get; }

    public PrimitiveDataContractSpecGenerator(CodeWriter writer, DataContractSetSpec contractSet, PrimitiveDataContractSpec primitiveContract)
        : base(writer, contractSet)
    {
        PrimitiveContract = primitiveContract;
    }

    public override void DeclareDataContract()
    {
        AppendLine($"private static global::Marius.DataContracts.Runtime.PrimitiveDataContract<{PrimitiveContract.UnderlyingType.FullyQualifiedName}> {PrimitiveContract.GeneratedName};");
    }

    public override (string, string?) GenerateDataContract(string xmlDictionary)
    {
        AppendLine();
        AppendLine($"{PrimitiveContract.GeneratedName} = new global::Marius.DataContracts.Runtime.PrimitiveDataContract<{PrimitiveContract.UnderlyingType.FullyQualifiedName}>");
        using (Block(end: "};"))
        {
            AppendLine($"Id = {SymbolDisplay.FormatPrimitive(PrimitiveContract.Id, true, false)},");
            AppendLine($"UnderlyingType = typeof({PrimitiveContract.UnderlyingType.FullyQualifiedName}),");
            if (PrimitiveContract.InterfaceType != null)
                AppendLine($"InterfaceType = typeof({PrimitiveContract.InterfaceType.FullyQualifiedName}),");
            else
                AppendLine("InterfaceType = null,");

            AppendLine($"OriginalUnderlyingType = typeof({PrimitiveContract.OriginalUnderlyingType.FullyQualifiedName}),");
            AppendLine($"Name = {xmlDictionary}.Add({SymbolDisplay.FormatLiteral(PrimitiveContract.Name, true)}),");
            AppendLine($"Namespace = {xmlDictionary}.Add({SymbolDisplay.FormatLiteral(PrimitiveContract.Namespace, true)}),");
            AppendLine($"XmlName = new global::System.Xml.XmlQualifiedName({SymbolDisplay.FormatLiteral(PrimitiveContract.XmlName, true)}, {SymbolDisplay.FormatLiteral(PrimitiveContract.XmlNamespace, true)}),");
            AppendLine($"IsPrimitive = {(PrimitiveContract.IsPrimitive ? "true" : "false")},");
            AppendLine($"IsReference = {(PrimitiveContract.IsReference ? "true" : "false")},");
            AppendLine($"IsISerializable = {(PrimitiveContract.IsISerializable ? "true" : "false")},");
            AppendLine($"HasRoot = {(PrimitiveContract.HasRoot ? "true" : "false")},");
            AppendLine($"CanContainReferences = {(PrimitiveContract.CanContainReferences ? "true" : "false")},");
            AppendLine($"IsBuiltInDataContract = {(PrimitiveContract.IsBuiltInDataContract ? "true" : "false")},");

            if (PrimitiveContract.TopLevelElementName == null)
                AppendLine("TopLevelElementName = null,");
            else
                AppendLine($"TopLevelElementName = {xmlDictionary}.Add({SymbolDisplay.FormatLiteral(PrimitiveContract.TopLevelElementName, true)}),");

            if (PrimitiveContract.TopLevelElementNamespace == null)
                AppendLine("TopLevelElementNamespace = null,");
            else
                AppendLine($"TopLevelElementNamespace = {xmlDictionary}.Add({SymbolDisplay.FormatLiteral(PrimitiveContract.TopLevelElementNamespace, true)}),");

            if (PrimitiveContract.ReadMethodName == "ReadElementContentAsAnyType")
            {
                AppendLine($"Read = static (xmlReader, context) => (context is null) ? xmlReader.{PrimitiveContract.ReadMethodName}(typeof(object)) : global::Marius.DataContracts.Runtime.PrimitiveDataContract.HandleReadValue(xmlReader.{PrimitiveContract.ReadMethodName}(typeof(object)), context), ");
                AppendLine("Write = static (xmlWriter, context, obj) => { },"); // write nothing
            }
            else
            {
                AppendLine($"Read = static (xmlReader, context) => (context is null) ? xmlReader.{PrimitiveContract.ReadMethodName}() : global::Marius.DataContracts.Runtime.PrimitiveDataContract.HandleReadValue(xmlReader.{PrimitiveContract.ReadMethodName}(), context), ");
                AppendLine($"Write = static (xmlWriter, context, obj) => xmlWriter.{PrimitiveContract.WriteMethodName}(obj),");
            }
        }

        return (PrimitiveContract.GeneratedName, PrimitiveContract.InterfaceType != null ? "InterfaceType" : null);
    }

    public override void GenerateDependencies(string xmlDictionary)
    {
        if (PrimitiveContract.BaseContractId >= 0)
            AppendLine($"{PrimitiveContract.GeneratedName}.BaseContract = {GetContract(PrimitiveContract.BaseContractId)!.GeneratedName};");

    }
}