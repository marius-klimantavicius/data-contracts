using System.Diagnostics;
using Microsoft.CodeAnalysis.CSharp;
using Marius.DataContracts.SourceGenerators.Specs;

namespace Marius.DataContracts.SourceGenerators;

/// <summary>
/// Generates code for XmlDataContract using Spec classes (free from Roslyn symbols).
/// </summary>
internal class XmlDataContractSpecGenerator : SpecContractGenerator
{
    public XmlDataContractSpec XmlContract { get; }

    public XmlDataContractSpecGenerator(CodeWriter writer, DataContractSetSpec contractSet, XmlDataContractSpec xmlContract)
        : base(writer, contractSet)
    {
        XmlContract = xmlContract;
    }

    public override void DeclareDataContract()
    {
        AppendLine($"private static global::Marius.DataContracts.Runtime.XmlDataContract<{XmlContract.UnderlyingType.FullyQualifiedName}> {XmlContract.GeneratedName};");
    }

    public override (string, string?) GenerateDataContract(string xmlDictionary)
    {
        var classTypeName = XmlContract.UnderlyingType.FullyQualifiedName;

        AppendLine();
        var method = LocalName("__XmlDataContract");
        var xmlDictionaryLocal = LocalName("__xmlDictionary");
        AppendLine($"static global::Marius.DataContracts.Runtime.XmlDataContract<{classTypeName}> {method}(global::System.Xml.XmlDictionary {xmlDictionaryLocal})");
        using (Block())
        {
            var xmlName = LocalName("__xmlName");
            AppendLine($"var {xmlName} = new global::System.Xml.XmlQualifiedName({SymbolDisplay.FormatLiteral(XmlContract.XmlName, true)}, {SymbolDisplay.FormatLiteral(XmlContract.XmlNamespace, true)});");

            var hasRoot = LocalName("__hasRoot");
            AppendLine($"var {hasRoot} = {(XmlContract.HasRoot ? "true" : "false")};");

            if (!string.IsNullOrEmpty(XmlContract.SchemaProviderMethod))
            {
                var typeInfo = LocalName("__typeInfo");
                var schemas = LocalName("__schemas");
                AppendLine($"var {schemas} = new global::System.Xml.Schema.XmlSchemaSet();");
                AppendLine($"{schemas}.XmlResolver = null;");
                AppendLine();

                if (!string.IsNullOrEmpty(XmlContract.SchemaProviderMethodAccessorName))
                    AppendLine($"var {typeInfo} = PrivateAccessors.{XmlContract.SchemaProviderMethodAccessorName}(default({XmlContract.UnderlyingType.FullyQualifiedName}), {schemas});");
                else
                    AppendLine($"var {typeInfo} = {XmlContract.UnderlyingType.FullyQualifiedName}.{XmlContract.SchemaProviderMethod}({schemas});");

                if (XmlContract.IsAny)
                {
                    AppendLine($"if ({typeInfo} is not null)");
                    AppendLine($"    throw new global::System.Runtime.Serialization.InvalidDataContractException({SymbolDisplay.FormatLiteral(SR.Format(SR.InvalidNonNullReturnValueByIsAny, XmlContract.UnderlyingType.FullyQualifiedName, XmlContract.SchemaProviderMethod), true)});");
                    AppendLine();
                }

                AppendLine($"if ({typeInfo} is null)");
                using (Block())
                {
                    AppendLine($"{hasRoot} = false;");
                    // xmlName = DataContract.GetDefaultXmlName(clrType) not needed as it is set by default already
                }

                if (XmlContract.SchemaProviderMethodIsXmlSchemaType == true)
                {
                    AppendLine("else");
                    using (Block())
                    {
                        var schemaType = typeInfo;
                        var typeName = LocalName("__typeName");
                        var typeNs = LocalName("__typeNs");
                        AppendLine($"var {typeName} = {schemaType}.Name;");
                        AppendLine($"var {typeNs} = default(string);");

                        AppendLine();
                        AppendLine($"if (!string.IsNullOrEmpty({typeName}))");
                        using (Block())
                        {
                            var schema = LocalName("__schema");
                            AppendLine($"foreach (global::System.Xml.Schema.XmlSchema {schema} in {schemas}.Schemas())");
                            using (Block())
                            {
                                var schemaItem = LocalName("__schemaItem");
                                AppendLine($"foreach (global::System.Xml.Schema.XmlSchemaObject {schemaItem} in {schema}.Items)");
                                using (Block())
                                {
                                    AppendLine($"if ((object){schemaItem} == (object){schemaType})");
                                    using (Block())
                                    {
                                        AppendLine($"{typeNs} = {schema}.TargetNamespace ?? string.Empty;");
                                        AppendLine("break;");
                                    }

                                    AppendLine("");
                                    AppendLine($"if ({typeNs} is not null)");
                                    AppendLine("    break;");
                                }

                                AppendLine();
                                AppendLine($"if ({typeNs} is null)");
                                AppendLine($"    throw new global::System.Runtime.Serialization.InvalidDataContractException(string.Format({SymbolDisplay.FormatLiteral(SR.Format(SR.MissingSchemaType, "{0}", XmlContract.UnderlyingType.FullyQualifiedName), true)}, {typeName}));");

                                AppendLine();
                                AppendLine($"{xmlName} = new global::System.Xml.XmlQualifiedName({typeName}, {typeNs});");
                            }
                        }
                    }
                }

                if (XmlContract.SchemaProviderMethodIsXmlSchemaType == false)
                {
                    AppendLine("else");
                    using (Block())
                    {
                        AppendLine($"{xmlName} = {typeInfo};");
                    }
                }
            }

            AppendLine();
            var topLevelElementName = LocalName("__topLevelElementName");
            var topLevelElementNamespace = LocalName("__topLevelElementNamespace");
            if (XmlContract.TopLevelElementName == null)
                AppendLine($"var {topLevelElementName} = default(global::System.Xml.XmlDictionaryString);");
            else
                AppendLine($"var {topLevelElementName} = {xmlDictionaryLocal}.Add({SymbolDisplay.FormatLiteral(XmlContract.TopLevelElementName, true)});");

            if (XmlContract.TopLevelElementNamespace == null)
                AppendLine($"var {topLevelElementNamespace} = default(global::System.Xml.XmlDictionaryString);");
            else
                AppendLine($"var {topLevelElementNamespace} = {xmlDictionaryLocal}.Add({SymbolDisplay.FormatLiteral(XmlContract.TopLevelElementNamespace, true)});");

            AppendLine($"if ({hasRoot})");
            using (Block())
            {
                if (XmlContract.XmlRootAttribute == null)
                {
                    AppendLine($"{topLevelElementName} = {xmlDictionaryLocal}.Add({xmlName}.Name);");
                    AppendLine($"{topLevelElementNamespace} = ({xmlName}.Namespace == global::Marius.DataContracts.Runtime.Globals.SchemaNamespace) ? global::Marius.DataContracts.Runtime.DictionaryGlobals.EmptyString : {xmlDictionaryLocal}.Add({xmlName}.Namespace);");
                }
                else
                {
                    if (string.IsNullOrEmpty(XmlContract.XmlRootAttribute.ElementName))
                        AppendLine($"{topLevelElementName} = {xmlDictionaryLocal}.Add({xmlName}.Name);");
                    else
                        AppendLine($"{topLevelElementName} = {xmlDictionaryLocal}.Add({SymbolDisplay.FormatLiteral(XmlContract.XmlRootAttribute.ElementName, true)});");

                    if (string.IsNullOrEmpty(XmlContract.XmlRootAttribute.Namespace))
                        AppendLine($"{topLevelElementNamespace} = global::Marius.DataContracts.Runtime.DictionaryGlobals.EmptyString;");
                    else
                        AppendLine($"{topLevelElementNamespace} = {xmlDictionaryLocal}.Add({SymbolDisplay.FormatLiteral(XmlContract.XmlRootAttribute.Namespace, true)});");
                }
            }

            if (XmlContract.XmlRootAttribute != null)
            {
                AppendLine("else");
                using (Block())
                    AppendLine($"throw new InvalidDataContractException({SymbolDisplay.FormatLiteral(SR.Format(SR.IsAnyCannotHaveXmlRoot, XmlContract.UnderlyingType.FullyQualifiedName), true)});");
            }

            AppendLine();
            AppendLine($"return new global::Marius.DataContracts.Runtime.XmlDataContract<{classTypeName}>");
            using (Block(end: "};"))
            {
                AppendLine($"Id = {SymbolDisplay.FormatPrimitive(XmlContract.Id, false, false)},");
                AppendLine($"UnderlyingType = typeof({classTypeName}),");
                AppendLine($"OriginalUnderlyingType = typeof({XmlContract.OriginalUnderlyingType.FullyQualifiedName}),");
                AppendLine($"Name = {xmlDictionaryLocal}.Add({xmlName}.Name),");
                AppendLine($"Namespace = {xmlDictionaryLocal}.Add({xmlName}.Namespace),");
                AppendLine($"XmlName = {xmlName},");
                AppendLine($"IsISerializable = {(XmlContract.IsISerializable ? "true" : "false")},");
                AppendLine($"IsPrimitive = {(XmlContract.IsPrimitive ? "true" : "false")},");
                AppendLine($"IsReference = {(XmlContract.IsReference ? "true" : "false")},");
                AppendLine($"HasRoot = {hasRoot},");
                AppendLine($"CanContainReferences = {(XmlContract.CanContainReferences ? "true" : "false")},");
                AppendLine($"IsBuiltInDataContract = {(XmlContract.IsBuiltInDataContract ? "true" : "false")},");
                AppendLine($"TopLevelElementName = {topLevelElementName},");
                AppendLine($"TopLevelElementNamespace = {topLevelElementNamespace},");

                // Generate Create delegate
                if (XmlContract.IsXElement)
                    AppendLine($"Create = static () => new {classTypeName}(\"default\"),");
                else if (XmlContract.IsXmlElementOrXmlNodeArray)
                    AppendLine("Create = static () => throw new global::System.NotSupportedException(),");
                else if (XmlContract.ConstructorAccessorName != null)
                    AppendLine($"Create = static () => PrivateAccessors.{XmlContract.ConstructorAccessorName}(),");
                else
                    AppendLine($"Create = static () => new {classTypeName}(),");

                AppendLine("Read = static (xmlReader, context) =>");
                using (Block(end: "},"))
                {
                    AppendLine("throw new global::System.NotImplementedException();");
                }
            }
        }

        AppendLine($"{XmlContract.GeneratedName} = {method}({xmlDictionary});");

        return (XmlContract.GeneratedName, null);
    }

    public override void GenerateDependencies(string xmlDictionary)
    {
        if (XmlContract.KnownDataContracts.Length > 0)
        {
            var dictionary = LocalName("__knownTypes");

            AppendLine($"var {dictionary} = new global::System.Collections.Generic.Dictionary<global::System.Xml.XmlQualifiedName, global::Marius.DataContracts.Runtime.DataContract>();");
            foreach (var item in XmlContract.KnownDataContracts)
            {
                var knownContract = GetContract(item.ContractId);
                Debug.Assert(knownContract != null);

                AppendLine($"{dictionary}.TryAdd({knownContract.GeneratedName}.XmlName, {knownContract.GeneratedName});");
            }

            AppendLine($"{XmlContract.GeneratedName}.KnownDataContracts = global::System.Collections.Frozen.FrozenDictionary.ToFrozenDictionary({dictionary});");
        }

        if (XmlContract.BaseContractId >= 0)
            AppendLine($"{XmlContract.GeneratedName}.BaseContract = {GetContract(XmlContract.BaseContractId)!.GeneratedName};");
    }
}