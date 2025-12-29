using Marius.DataContracts.SourceGenerators.Specs;
using Microsoft.CodeAnalysis.CSharp;

namespace Marius.DataContracts.SourceGenerators.Generators;

/// <summary>
/// Generates code for EnumDataContract using Spec classes (free from Roslyn symbols).
/// </summary>
internal class EnumDataContractSpecGenerator : SpecContractGenerator
{
    public EnumDataContractSpec EnumContract { get; }

    public EnumDataContractSpecGenerator(CodeWriter writer, DataContractSetSpec contractSet, EnumDataContractSpec enumContract)
        : base(writer, contractSet)
    {
        EnumContract = enumContract;
    }

    public override void DeclareDataContract()
    {
        AppendLine($"private static global::Marius.DataContracts.Runtime.EnumDataContract<{EnumContract.UnderlyingType.FullyQualifiedName}> {EnumContract.GeneratedName};");
    }

    public override (string, string?) GenerateDataContract(string xmlDictionary)
    {
        AppendLine();

        var classTypeName = EnumContract.UnderlyingType.FullyQualifiedName;
        AppendLine($"{EnumContract.GeneratedName} = new global::Marius.DataContracts.Runtime.EnumDataContract<{classTypeName}>");
        using (Block(end: "};"))
        {
            AppendLine($"Id = {SymbolDisplay.FormatPrimitive(EnumContract.Id, false, false)},");
            AppendLine($"UnderlyingType = typeof({classTypeName}),");
            AppendLine($"OriginalUnderlyingType = typeof({EnumContract.OriginalUnderlyingType.FullyQualifiedName}),");
            AppendLine($"Name = {xmlDictionary}.Add({SymbolDisplay.FormatLiteral(EnumContract.Name, true)}),");
            AppendLine($"Namespace = {xmlDictionary}.Add({SymbolDisplay.FormatLiteral(EnumContract.Namespace, true)}),");
            AppendLine($"XmlName = new global::System.Xml.XmlQualifiedName({SymbolDisplay.FormatLiteral(EnumContract.XmlName, true)}, {SymbolDisplay.FormatLiteral(EnumContract.XmlNamespace, true)}),");
            AppendLine($"IsPrimitive = {(EnumContract.IsPrimitive ? "true" : "false")},");
            AppendLine($"IsReference = {(EnumContract.IsReference ? "true" : "false")},");
            AppendLine($"IsISerializable = {(EnumContract.IsISerializable ? "true" : "false")},");
            AppendLine($"HasRoot = {(EnumContract.HasRoot ? "true" : "false")},");
            AppendLine($"CanContainReferences = {(EnumContract.CanContainReferences ? "true" : "false")},");
            AppendLine($"IsBuiltInDataContract = {(EnumContract.IsBuiltInDataContract ? "true" : "false")},");

            if (EnumContract.TopLevelElementName == null)
                AppendLine("TopLevelElementName = null,");
            else
                AppendLine($"TopLevelElementName = {xmlDictionary}.Add({SymbolDisplay.FormatLiteral(EnumContract.TopLevelElementName, true)}),");

            if (EnumContract.TopLevelElementNamespace == null)
                AppendLine("TopLevelElementNamespace = null,");
            else
                AppendLine($"TopLevelElementNamespace = {xmlDictionary}.Add({SymbolDisplay.FormatLiteral(EnumContract.TopLevelElementNamespace, true)}),");

            AppendLine($"IsFlags = {SymbolDisplay.FormatPrimitive(EnumContract.IsFlags, true, false)},");
            AppendLine($"IsULong = {SymbolDisplay.FormatPrimitive(EnumContract.IsULong, true, false)},");

            if (EnumContract.Members.Length > 0)
            {
                AppendLine("MemberNames = global::System.Runtime.InteropServices.ImmutableCollectionsMarshal.AsImmutableArray(");
                using (Block("[", "]),"))
                {
                    foreach (var item in EnumContract.Members)
                        AppendLine($"{SymbolDisplay.FormatLiteral(item.Name, quote: true)},");
                }
            }
            else
            {
                AppendLine("MemberNames = global::System.Collections.Immutable.ImmutableArray<string>.Empty,");
            }

            if (EnumContract.Values.Length > 0)
            {
                AppendLine("Values = global::System.Runtime.InteropServices.ImmutableCollectionsMarshal.AsImmutableArray(");
                using (Block("[", "]),"))
                {
                    foreach (var item in EnumContract.Values)
                        AppendLine($"{SymbolDisplay.FormatPrimitive(item, false, false)}L,");
                }
            }
            else
            {
                AppendLine("Values = global::System.Collections.Immutable.ImmutableArray<long>.Empty,");
            }
        }

        return (EnumContract.GeneratedName, null);
    }

    public override void GenerateDependencies(string xmlDictionary)
    {
        if (EnumContract.BaseContractId >= 0)
            AppendLine($"{EnumContract.GeneratedName}.BaseContract = {GetContract(EnumContract.BaseContractId)!.GeneratedName};");
    }
}