using Marius.DataContracts.SourceGenerators.Specs;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Marius.DataContracts.SourceGenerators.Generators;

/// <summary>
/// Base class for contract generators that work with Spec classes (free from Roslyn symbols).
/// </summary>
internal abstract class SpecContractGenerator
{
    private CodeWriter _writer;

    protected CodeWriter Writer => _writer;
    
    protected DataContractSetSpec ContractSet { get; }

    protected SpecContractGenerator(CodeWriter writer, DataContractSetSpec contractSet)
    {
        _writer = writer;
        ContractSet = contractSet;
    }

    public abstract void DeclareDataContract();
    public abstract (string localName, string? underlyingTypeOverride) GenerateDataContract(SourceProductionContext context, string xmlDictionary);
    public abstract void GenerateDependencies(string xmlDictionary);

    protected CodeWriter.IndentDisposable Block(string start = "{", string end = "}") => _writer.Block(start, end);

    protected WriterDisposable NewWriter()
    {
        _writer = new CodeWriter(_writer);
        return new WriterDisposable(this);
    }

    protected void AppendLine() => _writer.AppendLine();
    protected void AppendLine(string value) => _writer.AppendLine(value);
    protected string LocalName(string prefix) => _writer.LocalName(prefix);

    protected string GetMemberValue(string instance, TypeSpec type, MemberSpec memberInfo)
    {
        if (memberInfo.Kind == MemberKindSpec.Property)
        {
            if (!memberInfo.HasAccessibleGetter && memberInfo.GetterAccessorName != null)
                return $"PrivateAccessor.{memberInfo.GetterAccessorName}({type.MaybeRef()}{instance})";
        }
        else if (memberInfo.Kind == MemberKindSpec.Field)
        {
            if (!memberInfo.IsAccessible && memberInfo.GetterAccessorName != null)
                return $"PrivateAccessor.{memberInfo.GetterAccessorName}({type.MaybeRef()}{instance})";
        }

        return $"{instance}.{memberInfo.Name}";
    }

    protected string ReadValue(TypeSpec type, DataContractSpec? dataContract, string name, string ns, bool isGetOnlyCollection)
    {
        var value = LocalName("__value");
        AppendLine($"var {value} = default({type.FullyQualifiedName});");

        var nullables = 0;
        var unwrappedType = type;

        while (unwrappedType.IsNullableValueType && unwrappedType.ElementType != null)
        {
            nullables++;
            unwrappedType = unwrappedType.ElementType;
        }

        var primitiveContract = dataContract as PrimitiveDataContractSpec;
        if ((primitiveContract != null && primitiveContract.UnderlyingType.SpecialType != SpecialTypeKind.Object) || nullables != 0 || unwrappedType.IsValueType)
        {
            AppendLine("context.ReadAttributes(xmlReader);");
            AppendLine();

            var objectId = LocalName("__objectId");
            AppendLine($"var {objectId} = context.ReadIfNullOrRef(xmlReader, typeof({unwrappedType.FullyQualifiedName}), {SymbolDisplay.FormatPrimitive(unwrappedType.IsTypeSerializable, true, false)});");
            AppendLine($"if ({objectId} is null)");
            using (Block())
            {
                if (nullables != 0 || !unwrappedType.IsValueType)
                    AppendLine($"{value} = null;");
                else
                    AppendLine($"ThrowValidationException({SymbolDisplay.FormatLiteral($"Value type '{unwrappedType.FullyQualifiedName}' cannot be null", true)});");
            }

            AppendLine("else");
            using (Block())
            {
                var originalValue = default(string);
                if (nullables != 0)
                {
                    originalValue = value;
                    value = LocalName("__value");
                    AppendLine($"var {value} = default({unwrappedType.FullyQualifiedName});");
                }

                AppendLine($"if ({objectId}.Length == 0)");
                using (Block())
                {
                    AppendLine($"{objectId} = context.GetObjectId();");
                    if (unwrappedType.IsValueType)
                    {
                        AppendLine($"if (!string.IsNullOrEmpty({objectId}))");
                        AppendLine($"    ThrowValidationException({SymbolDisplay.FormatLiteral($"Value type '{unwrappedType.FullyQualifiedName}' cannot have ID", true)});");
                    }

                    if (primitiveContract != null && primitiveContract.UnderlyingType.SpecialType != SpecialTypeKind.Object)
                    {
                        AppendLine($"{value} = xmlReader.{primitiveContract.ReadMethodName}();");
                        if (!unwrappedType.IsValueType)
                            AppendLine($"context.AddNewObject({value});");
                    }
                    else
                    {
                        AppendLine($"{value} = context.Read<{unwrappedType.FullyQualifiedName}>(xmlReader, {SymbolDisplay.FormatLiteral(name, true)}, {SymbolDisplay.FormatLiteral(ns, true)}, {dataContract?.GeneratedName ?? "null"});");
                    }
                }

                AppendLine("else");
                using (Block())
                {
                    if (unwrappedType.IsValueType)
                        AppendLine($"ThrowValidationException({SymbolDisplay.FormatLiteral($"Value type '{unwrappedType.FullyQualifiedName}' cannot have ref", true)});");
                    else
                        AppendLine($"{value} = context.GetExistingObject<{unwrappedType.FullyQualifiedName}>({objectId}, {SymbolDisplay.FormatLiteral(name, true)}, {SymbolDisplay.FormatLiteral(ns, true)});");
                }

                if (originalValue != null)
                {
                    AppendLine();
                    AppendLine($"{originalValue} = {value};");
                    value = originalValue;
                }
            }

            AppendLine();
        }
        else
        {
            AppendLine($"{value} = context.Read<{unwrappedType.FullyQualifiedName}>(xmlReader, {SymbolDisplay.FormatLiteral(name, true)}, {SymbolDisplay.FormatLiteral(ns, true)}, {dataContract?.GeneratedName ?? "null"});");
        }

        return value;
    }

    protected void WriteValue(string memberValue, TypeSpec memberType, DataContractSpec memberContract, bool writeXsiType, string? memberTypeLocal = null)
    {
        // TODO: Maybe handle pointer?

        if (memberType.IsValueType && !memberType.IsNullableValueType)
        {
            if (memberContract is PrimitiveDataContractSpec primitiveContract && !writeXsiType)
            {
                WritePrimitive(primitiveContract, memberValue);
            }
            else
            {
                if (memberTypeLocal == null)
                {
                    memberTypeLocal = LocalName("__memberType");
                    AppendLine($"var {memberTypeLocal} = typeof({memberType.FullyQualifiedName}).TypeHandle;");
                }

                AppendLine($"context.InternalSerialize(xmlWriter, {memberValue}, {memberValue}.GetType().TypeHandle.Equals({memberTypeLocal}), {SymbolDisplay.FormatPrimitive(writeXsiType, true, false)}, {memberTypeLocal});");
            }
        }
        else
        {
            if (memberType.IsNullableValueType)
            {
                var elementType = memberType.ElementType!;
                AppendLine($"if ({memberValue} == null)");
                using (Block())
                    AppendLine($"context.WriteNull(xmlWriter, typeof({elementType.FullyQualifiedName}), {SymbolDisplay.FormatPrimitive(elementType.IsTypeSerializable, true, false)});");

                AppendLine("else");
                using (Block())
                {
                    var actualValue = LocalName("__value");
                    AppendLine($"var {actualValue} = {memberValue}.GetValueOrDefault();");
                    if (memberContract is PrimitiveDataContractSpec primitiveContract && primitiveContract.UnderlyingType.SpecialType != SpecialTypeKind.Object && !writeXsiType)
                        WritePrimitive(primitiveContract, actualValue);
                    else
                        AppendLine($"context.InternalSerialize(xmlWriter, {actualValue}, true, {SymbolDisplay.FormatPrimitive(writeXsiType, true, false)}, typeof({elementType.FullyQualifiedName}).TypeHandle);");
                }
            }
            else
            {
                if (memberTypeLocal == null)
                {
                    memberTypeLocal = LocalName("__memberType");
                    AppendLine($"var {memberTypeLocal} = typeof({memberType.FullyQualifiedName}).TypeHandle;");
                }

                AppendLine($"if ({memberValue} is null)");
                AppendLine($"    context.WriteNull(xmlWriter, typeof({memberType.FullyQualifiedName}), {SymbolDisplay.FormatPrimitive(memberType.IsTypeSerializable, true, false)});");

                AppendLine("else");
                if (memberContract is PrimitiveDataContractSpec primitiveContract && primitiveContract.UnderlyingType.SpecialType != SpecialTypeKind.Object && !writeXsiType)
                    WritePrimitive(primitiveContract, memberValue, "    ");
                else
                    AppendLine($"    context.InternalSerializeReference(xmlWriter, {memberValue}, {memberValue}.GetType().TypeHandle.Equals({memberTypeLocal}), {SymbolDisplay.FormatPrimitive(writeXsiType, true, false)}, {memberTypeLocal});");
            }
        }
    }

    protected void WritePrimitive(PrimitiveDataContractSpec primitiveContract, string memberValue, string indent = "")
    {
        if (primitiveContract.UnderlyingType.IsValueType)
            AppendLine($"{indent}xmlWriter.{primitiveContract.WriteMethodName}({memberValue});");
        else
            AppendLine($"{indent}context.{primitiveContract.WriteMethodName}(xmlWriter, {memberValue});");

    }

    protected void WritePrimitive(PrimitiveDataContractSpec primitiveContract, string memberValue, string ns, string name)
    {
        if (primitiveContract.UnderlyingType.IsValueType)
            AppendLine($"xmlWriter.{primitiveContract.WriteMethodName}({memberValue}, {ns}, {name});");
        else
            AppendLine($"context.{primitiveContract.WriteMethodName}(xmlWriter, {memberValue}, {ns}, {name});");

    }

    protected void WriteStartElement(TypeSpec type, string ns, string? memberNamesLocal, string namespaceLocal, string? nameLocal, int nameIndex)
    {
        var needsPrefix = NeedsPrefix(type, ns);
        if (memberNamesLocal != null)
            nameLocal ??= $"{memberNamesLocal}[{SymbolDisplay.FormatPrimitive(nameIndex, true, false)}]";

        if (needsPrefix)
            AppendLine($"xmlWriter.WriteStartElement(\"q\", {nameLocal}, {namespaceLocal});");
        else
            AppendLine($"xmlWriter.WriteStartElement({nameLocal}, {namespaceLocal});");
    }

    private static bool NeedsPrefix(TypeSpec type, string? ns)
    {
        return type.FullyQualifiedName == "global::System.Xml.XmlQualifiedName" && string.IsNullOrEmpty(ns);
    }

    protected DataContractSpec? GetContract(int id)
    {
        if (id < 0 || id >= ContractSet.Contracts.Length)
            return null;

        return ContractSet.Contracts[id];
    }
    
    public struct WriterDisposable: IDisposable
    {
        private SpecContractGenerator _generator;
        public WriterDisposable(SpecContractGenerator generator)
        {
            _generator = generator;
        }

        public void Dispose()
        {
            _generator._writer = _generator._writer.Parent!;
        }
    }
}