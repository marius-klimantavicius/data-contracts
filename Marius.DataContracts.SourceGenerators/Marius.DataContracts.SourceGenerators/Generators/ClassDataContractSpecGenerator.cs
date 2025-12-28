using System.Diagnostics;
using Microsoft.CodeAnalysis.CSharp;
using Marius.DataContracts.SourceGenerators.Specs;

namespace Marius.DataContracts.SourceGenerators;

internal class ClassDataContractSpecGenerator : SpecContractGenerator
{
    public ClassDataContractSpec ClassContract { get; }

    public ClassDataContractSpecGenerator(CodeWriter writer, DataContractSetSpec contractSet, ClassDataContractSpec classContract)
        : base(writer, contractSet)
    {
        ClassContract = classContract;
    }

    public override void DeclareDataContract()
    {
        if (ClassContract.UnderlyingType.IsOpenGenericType)
            return; // Skip open generic types

        AppendLine($"private static global::Marius.DataContracts.Runtime.ClassDataContract<{ClassContract.UnderlyingType.FullyQualifiedName}> {ClassContract.GeneratedName};");
    }

    public override (string, string?) GenerateDataContract(string xmlDictionary)
    {
        if (ClassContract.UnderlyingType.IsOpenGenericType)
            return ("", null); // Skip open generic types

        var classTypeName = ClassContract.UnderlyingType.FullyQualifiedName;

        AppendLine();
        AppendLine($"{ClassContract.GeneratedName} = new global::Marius.DataContracts.Runtime.ClassDataContract<{classTypeName}>");
        using (Block(end: "};"))
        {
            AppendLine($"Id = {SymbolDisplay.FormatPrimitive(ClassContract.Id, false, false)},");
            AppendLine($"UnderlyingType = typeof({classTypeName}),");
            AppendLine($"OriginalUnderlyingType = typeof({ClassContract.OriginalUnderlyingType.FullyQualifiedName}),");
            AppendLine($"Name = {xmlDictionary}.Add({SymbolDisplay.FormatLiteral(ClassContract.Name, true)}),");
            AppendLine($"Namespace = {xmlDictionary}.Add({SymbolDisplay.FormatLiteral(ClassContract.Namespace, true)}),");
            AppendLine($"XmlName = new global::System.Xml.XmlQualifiedName({SymbolDisplay.FormatLiteral(ClassContract.XmlName, true)}, {SymbolDisplay.FormatLiteral(ClassContract.XmlNamespace, true)}),");
            AppendLine($"IsPrimitive = {(ClassContract.IsPrimitive ? "true" : "false")},");
            AppendLine($"IsReference = {(ClassContract.IsReference ? "true" : "false")},");
            AppendLine($"IsISerializable = {(ClassContract.IsISerializable ? "true" : "false")},");
            AppendLine($"HasRoot = {(ClassContract.HasRoot ? "true" : "false")},");
            AppendLine($"CanContainReferences = {(ClassContract.CanContainReferences ? "true" : "false")},");
            AppendLine($"IsBuiltInDataContract = {(ClassContract.IsBuiltInDataContract ? "true" : "false")},");

            if (ClassContract.TopLevelElementName == null)
                AppendLine("TopLevelElementName = null,");
            else
                AppendLine($"TopLevelElementName = {xmlDictionary}.Add({SymbolDisplay.FormatLiteral(ClassContract.TopLevelElementName, true)}),");

            if (ClassContract.TopLevelElementNamespace == null)
                AppendLine("TopLevelElementNamespace = null,");
            else
                AppendLine($"TopLevelElementNamespace = {xmlDictionary}.Add({SymbolDisplay.FormatLiteral(ClassContract.TopLevelElementNamespace, true)}),");

            if (ClassContract.MemberNames.Length > 0)
            {
                AppendLine("MemberNames = global::System.Runtime.InteropServices.ImmutableCollectionsMarshal.AsImmutableArray(");
                using (Block("[", "]),"))
                {
                    foreach (var item in ClassContract.MemberNames)
                        AppendLine($"{xmlDictionary}.Add({SymbolDisplay.FormatLiteral(item, quote: true)}),");
                }
            }
            else
            {
                AppendLine("MemberNames = global::System.Collections.Immutable.ImmutableArray<global::System.Xml.XmlDictionaryString>.Empty,");
            }

            if (ClassContract.MemberNamespaces.Length > 0)
            {
                AppendLine("MemberNamespaces = global::System.Runtime.InteropServices.ImmutableCollectionsMarshal.AsImmutableArray(");
                using (Block("[", "]),"))
                {
                    foreach (var item in ClassContract.MemberNamespaces)
                        AppendLine($"{xmlDictionary}.Add({SymbolDisplay.FormatLiteral(item, quote: true)}),");
                }
            }
            else
            {
                AppendLine("MemberNamespaces = global::System.Collections.Immutable.ImmutableArray<global::System.Xml.XmlDictionaryString>.Empty,");
            }

            if (ClassContract.ChildElementNamespaces.Length > 0)
            {
                AppendLine("ChildElementNamespaces = global::System.Runtime.InteropServices.ImmutableCollectionsMarshal.AsImmutableArray<global::System.Xml.XmlDictionaryString>(");
                using (Block("[", "]),"))
                {
                    foreach (var item in ClassContract.ChildElementNamespaces)
                    {
                        if (item == null)
                            AppendLine("null,");
                        else
                            AppendLine($"{xmlDictionary}.Add({SymbolDisplay.FormatLiteral(item, quote: true)}),");
                    }
                }
            }
            else
            {
                AppendLine("ChildElementNamespaces = global::System.Collections.Immutable.ImmutableArray<global::System.Xml.XmlDictionaryString>.Empty,");
            }

            if (ClassContract.ContractNamespaces.Length > 0)
            {
                AppendLine("ContractNamespaces = global::System.Runtime.InteropServices.ImmutableCollectionsMarshal.AsImmutableArray(");
                using (Block("[", "]),"))
                {
                    foreach (var item in ClassContract.ContractNamespaces)
                        AppendLine($"{xmlDictionary}.Add({SymbolDisplay.FormatLiteral(item, quote: true)}),");
                }
            }
            else
            {
                AppendLine("ContractNamespaces = global::System.Collections.Immutable.ImmutableArray<global::System.Xml.XmlDictionaryString>.Empty,");
            }


            AppendLine("Read = static (xmlReader, context, memberNames, memberNamespaces) =>");
            using (Block(end: "},"))
            {
                // Check if the type can be instantiated (not abstract class or interface)
                if (!ClassContract.UnderlyingType.CanBeInstantiated)
                {
                    var errorMessage = ClassContract.UnderlyingType.TypeKind == TypeKindSpec.Interface
                        ? $"Interface type '{ClassContract.UnderlyingType.FullyQualifiedName}' cannot be created. Consider replacing with a non-interface serializable type."
                        : $"Abstract type '{ClassContract.UnderlyingType.FullyQualifiedName}' cannot be created. Consider using a non-abstract derived type.";
                    AppendLine($"context.ThrowSerializationException({SymbolDisplay.FormatLiteral(errorMessage, true)});");
                    AppendLine($"return default({classTypeName})!; // unreachable");
                }
                else
                {
                    CreateObject();
                    if (ClassContract.UnderlyingType.IsValueType)
                    {
                        var boxed = LocalName("__boxed");
                        AppendLine($"if (context.TryAddNewObject(__instance, out var {boxed}))");
                        AppendLine($"    __instance = ref global::System.Runtime.CompilerServices.Unsafe.Unbox<{ClassContract.UnderlyingType.FullyQualifiedName}>({boxed});");
                    }
                    else
                    {
                        AppendLine("context.AddNewObject(__instance);");
                    }

                    InvokeOnDeserializing(ClassContract);

                    var objectId = default(string);
                    if (ClassContract.HasFactoryMethod)
                    {
                        objectId = LocalName("__objectId");
                        AppendLine();
                        AppendLine($"var {objectId} = context.GetObjectId();");
                    }

                    AppendLine();
                    if (ClassContract.IsISerializable)
                        ReadISerializable();
                    else
                        ReadClass();

                    AppendLine();
                    var isFactoryMethod = InvokeFactoryMethod(objectId);
                    if (ClassContract.HasDeserializationCallback)
                        AppendLine($"((global::System.Runtime.Serialization.IDeserializationCallback)__instance).OnDeserialization(null);");

                    InvokeOnDeserialized(ClassContract);
                    if (objectId == null || !isFactoryMethod)
                    {
                        // Note: Adapters (DateTimeOffset, MemoryStream) are not yet implemented
                    }

                    AppendLine("return __instance;");
                }
            }

            AppendLine("Write = static (xmlWriter, context, obj) =>");
            using (Block(end: "},"))
            {
                if (ClassContract.IsReadOnlyContract)
                {
                    AppendLine("if (!context.SerializeReadOnlyTypes)");
                    AppendLine($"    throw new global::System.Runtime.Serialization.InvalidDataContractException({SymbolDisplay.FormatLiteral(ClassContract.SerializationExceptionMessage ?? $"Cannot serialize read-only type {ClassContract.UnderlyingType.Name}", true)});");
                }

                WriteClass();
            }
        }

        return (ClassContract.GeneratedName, null);
    }

    public override void GenerateDependencies(string xmlDictionary)
    {
        if (ClassContract.KnownDataContracts.Length > 0)
        {
            var dictionary = LocalName("__knownTypes");

            AppendLine($"var {dictionary} = new global::System.Collections.Generic.Dictionary<global::System.Xml.XmlQualifiedName, global::Marius.DataContracts.Runtime.DataContract>();");
            foreach (var item in ClassContract.KnownDataContracts)
            {
                var knownContract = GetContract(item.ContractId);
                Debug.Assert(knownContract != null);

                AppendLine($"{dictionary}.TryAdd({knownContract.GeneratedName}.XmlName, {knownContract.GeneratedName});");
            }

            AppendLine($"{ClassContract.GeneratedName}.KnownDataContracts = global::System.Collections.Frozen.FrozenDictionary.ToFrozenDictionary({dictionary});");
        }

        if (ClassContract.BaseContractId >= 0)
        {
            AppendLine($"{ClassContract.GeneratedName}.BaseContract = {GetContract(ClassContract.BaseContractId)!.GeneratedName};");
            AppendLine($"{ClassContract.GeneratedName}.BaseClassContract = {GetContract(ClassContract.BaseContractId)!.GeneratedName};");
        }
    }

    private void CreateObject()
    {
        if (ClassContract.IsDbNull)
        {
            AppendLine("var __instance = global::System.DBNull.Value;");
        }
        else
        {
            var classTypeName = ClassContract.UnderlyingType.FullyQualifiedName;
            if (ClassContract.IsNonAttributedType)
            {
                if (ClassContract.UnderlyingType.IsValueType)
                {
                    var localInstance = LocalName("__instance");
                    AppendLine($"var {localInstance} = default({classTypeName});");
                    AppendLine($"ref var __instance = ref {localInstance};");
                }
                else
                {
                    if (!ClassContract.HasParameterlessConstructor)
                        throw new InvalidOperationException($"Type '{classTypeName}' does not have a parameterless constructor.");

                    if (ClassContract.ConstructorAccessorName != null)
                        AppendLine($"var __instance = PrivateAccessors.{ClassContract.ConstructorAccessorName}();");
                    else
                        AppendLine($"var __instance = new {classTypeName}();");
                }
            }
            else
            {
                if (ClassContract.UnderlyingType.IsValueType)
                {
                    var localInstance = LocalName("__instance");
                    AppendLine($"var {localInstance} = default({classTypeName});");
                    AppendLine($"ref var __instance = ref {localInstance};");
                }
                else
                {
                    AppendLine($"var __instance = ({classTypeName})global::System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof({classTypeName}));");
                }
            }
        }
    }

    private void InvokeOnDeserializing(ClassDataContractSpec classContract)
    {
        if (classContract.BaseClassContractId >= 0)
        {
            var baseContract = GetContract(classContract.BaseClassContractId) as ClassDataContractSpec;
            if (baseContract != null)
                InvokeOnDeserializing(baseContract);
        }

        // Note: OnDeserializing callback invocation is not yet implemented
        // Diagnostic DCS4002 is reported during parsing if the callback exists
    }

    private void InvokeOnDeserialized(ClassDataContractSpec classContract)
    {
        if (classContract.BaseClassContractId >= 0)
        {
            var baseContract = GetContract(classContract.BaseClassContractId) as ClassDataContractSpec;
            if (baseContract != null)
                InvokeOnDeserialized(baseContract);
        }

        // Note: OnDeserialized callback invocation is not yet implemented
        // Diagnostic DCS4003 is reported during parsing if the callback exists
    }

    private void InvokeOnSerializing(ClassDataContractSpec classContract)
    {
        if (classContract.BaseClassContractId >= 0)
        {
            var baseContract = GetContract(classContract.BaseClassContractId) as ClassDataContractSpec;
            if (baseContract != null)
                InvokeOnSerializing(baseContract);
        }

        // Note: OnDeserializing callback invocation is not yet implemented
        // Diagnostic DCS4004 is reported during parsing if the callback exists
    }

    private void InvokeOnSerialized(ClassDataContractSpec classContract)
    {
        if (classContract.BaseClassContractId >= 0)
        {
            var baseContract = GetContract(classContract.BaseClassContractId) as ClassDataContractSpec;
            if (baseContract != null)
                InvokeOnSerialized(baseContract);
        }

        // Note: OnDeserialized callback invocation is not yet implemented
        // Diagnostic DCS4005 is reported during parsing if the callback exists
    }

    private void ReadISerializable()
    {
        if (ClassContract.ISerializableConstructorAccessorName == null)
        {
            // DbNull does not have a serialization constructor (why?).
            if (ClassContract.IsDbNull)
                return;

            // Diagnostic DCS3001 is reported during parsing
            AppendLine($"throw new global::System.Runtime.Serialization.InvalidDataContractException(\"Type '{ClassContract.UnderlyingType.FullyQualifiedName}' implements ISerializable but does not have a serialization constructor.\");");
            return;
        }

        AppendLine($"PrivateAccessor.{ClassContract.ISerializableConstructorAccessorName}({ClassContract.UnderlyingType.MaybeRef()}__instance, context.ReadSerializationInfo(xmlReader, typeof({ClassContract.UnderlyingType.FullyQualifiedName})), context.GetStreamingContext());");
    }

    private void ReadClass()
    {
        // Diagnostic DCS3002 is reported during parsing
        if (ClassContract.HasExtensionData)
            AppendLine($"throw new global::System.NotSupportedException(\"ExtensionData reading is not yet implemented for type '{ClassContract.UnderlyingType.FullyQualifiedName}'.\");");
        else
            ReadMembers(null);
    }

    private void ReadMembers(string? extensionDataLocal)
    {
        var memberCount = ClassContract.MemberNames.Length;
        AppendLine($"context.IncrementItemCount({SymbolDisplay.FormatPrimitive(memberCount, true, false)});");
        AppendLine();

        AppendLine("var __memberIndex = -1;");

        var requiredMembers = GetRequiredMembers(ClassContract, out var firstRequiredMember);
        var hasRequiredMembers = (firstRequiredMember < memberCount);
        if (hasRequiredMembers)
            AppendLine($"var __requiredIndex = {SymbolDisplay.FormatPrimitive(firstRequiredMember, true, false)};");

        AppendLine();
        AppendLine("while (true)");
        using (Block())
        {
            AppendLine("if (!xmlReader.MoveToNextElement())");
            AppendLine("    break;");
            AppendLine();

            var index = LocalName("__index");
            if (hasRequiredMembers)
                AppendLine($"var {index} = context.GetMemberIndexWithRequiredMembers(xmlReader, memberNames, memberNamespaces, __memberIndex, __requiredIndex, {extensionDataLocal ?? "null"});");
            else
                AppendLine($"var {index} = context.GetMemberIndex(xmlReader, memberNames, memberNamespaces, __memberIndex, {extensionDataLocal ?? "null"});");

            if (requiredMembers.Length > 0)
            {
                AppendLine($"switch ({index})");
                using (Block())
                {
                    ReadMembers(ClassContract, requiredMembers, "__memberIndex", "__requiredIndex");
                }
            }
        }

        if (hasRequiredMembers)
        {
            AppendLine();
            AppendLine($"if (__requiredIndex < {SymbolDisplay.FormatPrimitive(memberCount, true, false)})");
            AppendLine("    global::Marius.DataContracts.Runtime.XmlObjectSerializerReadContext.ThrowRequiredMemberMissingException(xmlReader, __memberIndex, __requiredIndex, memberNames);");
        }
    }

    private int ReadMembers(ClassDataContractSpec classContract, bool[] requiredMembers, string memberIndexLocal, string requiredIndexLocal)
    {
        var memberCount = 0;
        if (classContract.BaseClassContractId >= 0)
        {
            var baseContract = GetContract(classContract.BaseClassContractId) as ClassDataContractSpec;
            if (baseContract != null)
                memberCount = ReadMembers(baseContract, requiredMembers, memberIndexLocal, requiredIndexLocal);
        }

        for (var i = 0; i < classContract.Members.Length; i++, memberCount++)
        {
            var dataMember = classContract.Members[i];
            var memberType = dataMember.MemberType;
            AppendLine($"case {SymbolDisplay.FormatPrimitive(memberCount, true, false)}:");
            using (Block())
            {
                if (dataMember.IsRequired)
                {
                    var nextRequiredIndex = memberCount + 1;
                    for (; nextRequiredIndex < requiredMembers.Length; nextRequiredIndex++)
                        if (requiredMembers[nextRequiredIndex])
                            break;

                    AppendLine($"{requiredIndexLocal} = {nextRequiredIndex};");
                }

                var memberContract = GetContract(dataMember.MemberTypeContractId);
                var value = default(string);
                if (dataMember.IsGetOnlyCollection)
                {
                    value = LocalName("__value");
                    AppendLine($"var {value} = {GetMemberValue("__instance", ClassContract.UnderlyingType, dataMember.MemberInfo)};");
                    AppendLine($"context.StoreCollectionMemberInfo({value});");
                    ReadValue(memberType, memberContract, dataMember.Name, classContract.XmlNamespace, true);
                }
                else
                {
                    AppendLine("context.ResetCollectionMemberInfo();");
                    value = ReadValue(memberType, memberContract, dataMember.Name, ClassContract.XmlNamespace, false);

                    if (dataMember.MemberInfo.Kind == MemberKindSpec.Property)
                    {
                        if (!dataMember.MemberInfo.HasAccessibleSetter || dataMember.MemberInfo.IsSetterInitOnly)
                        {
                            if (dataMember.MemberInfo.SetterAccessorName != null)
                                AppendLine($"PrivateAccessor.{dataMember.MemberInfo.SetterAccessorName}({ClassContract.UnderlyingType.MaybeRef()}__instance, {value});");
                            else // Diagnostic DCS3003 is reported during parsing
                                AppendLine($"throw new global::System.Runtime.Serialization.InvalidDataContractException(\"Property '{dataMember.MemberInfo.Name}' on type '{ClassContract.UnderlyingType.FullyQualifiedName}' cannot be set.\");");
                        }
                        else
                        {
                            AppendLine($"__instance.{dataMember.MemberInfo.Name} = {value};");
                        }
                    }
                    else if (dataMember.MemberInfo.Kind == MemberKindSpec.Field)
                    {
                        if (!dataMember.MemberInfo.HasAccessibleSetter)
                        {
                            if (dataMember.MemberInfo.SetterAccessorName != null)
                                AppendLine($"PrivateAccessor.{dataMember.MemberInfo.SetterAccessorName}({ClassContract.UnderlyingType.MaybeRef()}__instance) = {value};");
                            else // Diagnostic DCS3003 is reported during parsing
                                AppendLine($"throw new global::System.Runtime.Serialization.InvalidDataContractException(\"Field '{dataMember.MemberInfo.Name}' on type '{ClassContract.UnderlyingType.FullyQualifiedName}' cannot be set.\");");
                        }
                        else
                        {
                            AppendLine($"__instance.{dataMember.MemberInfo.Name} = {value};");
                        }
                    }
                    else
                    {
                        // Diagnostic DCS3004 is reported during parsing
                        AppendLine($"throw new global::System.NotSupportedException(\"Unsupported member kind '{dataMember.MemberInfo.Kind}' for member '{dataMember.MemberInfo.Name}'.\");");
                    }
                }

                AppendLine($"{memberIndexLocal} = {SymbolDisplay.FormatPrimitive(memberCount, true, false)};");
                AppendLine("break;");
            }
        }

        return memberCount;
    }

    private bool InvokeFactoryMethod(string? objectId)
    {
        if (ClassContract.HasFactoryMethod)
        {
            AppendLine($"__instance = ({ClassContract.UnderlyingType.FullyQualifiedName})context.GetRealObject((global::System.Runtime.Serialization.IObjectReference)__instance, {objectId});");
            return true;
        }

        return false;
    }

    private bool[] GetRequiredMembers(ClassDataContractSpec contract, out int firstRequiredMember)
    {
        var memberCount = contract.MemberNames.Length;
        var requiredMembers = new bool[memberCount];
        GetRequiredMembers(contract, requiredMembers);
        for (firstRequiredMember = 0; firstRequiredMember < memberCount; firstRequiredMember++)
        {
            if (requiredMembers[firstRequiredMember])
                break;
        }

        return requiredMembers;
    }

    private int GetRequiredMembers(ClassDataContractSpec contract, bool[] requiredMembers)
    {
        var memberCount = 0;
        if (contract.BaseClassContractId >= 0)
        {
            var baseContract = GetContract(contract.BaseClassContractId) as ClassDataContractSpec;
            if (baseContract != null)
                memberCount = GetRequiredMembers(baseContract, requiredMembers);
        }

        var members = contract.Members;
        for (var i = 0; i < members.Length; i++, memberCount++)
            requiredMembers[memberCount] = members[i].IsRequired;

        return memberCount;
    }

    private void WriteClass()
    {
        InvokeOnSerializing(ClassContract);
        if (ClassContract.IsISerializable)
        {
            AppendLine("context.WriteISerializable(xmlWriter, obj);");
        }
        else
        {
            var contractNamespacesLocal = default(string?);
            if (ClassContract.ContractNamespaces.Length > 1)
            {
                contractNamespacesLocal = LocalName("__contractNamespaces");
                AppendLine($"var {contractNamespacesLocal} = {ClassContract.GeneratedName}.ContractNamespaces;");
            }

            var memberNamesLocal = LocalName("__memberNames");
            AppendLine($"var {memberNamesLocal} = {ClassContract.GeneratedName}.MemberNames;");

            var childElementNamespacesLocal = default(string);
            if (ClassContract.ChildElementNamespaces.Any(s => s is not null))
            {
                childElementNamespacesLocal = LocalName("__childElementNamespaces");
                AppendLine($"var {childElementNamespacesLocal} = {ClassContract.GeneratedName}.ChildElementNamespaces;");
            }

            if (ClassContract.HasExtensionData)
            {
                AppendLine($"throw new global::System.NotSupportedException(\"ExtensionData writing is not yet implemented for type '{ClassContract.UnderlyingType.FullyQualifiedName}'.\");");
            }
            else
            {
                var typeIndex = 1;
                var _childElementIndex = 0;
                WriteMembers(ClassContract, null, memberNamesLocal, contractNamespacesLocal, childElementNamespacesLocal, ref typeIndex, ref _childElementIndex);
            }
        }

        InvokeOnSerialized(ClassContract);
    }

    private int WriteMembers(ClassDataContractSpec classContract, ClassDataContractSpec? derivedMostClassContract, string memberNamesLocal, string? contractNamespacesLocal, string? childElementNamespacesLocal, ref int _typeIndex, ref int _childElementIndex)
    {
        var memberCount = 0;
        if (classContract.BaseClassContractId >= 0)
        {
            var baseContract = GetContract(classContract.BaseClassContractId) as ClassDataContractSpec;
            if (baseContract != null)
                memberCount = WriteMembers(baseContract, derivedMostClassContract, memberNamesLocal, contractNamespacesLocal, childElementNamespacesLocal, ref _typeIndex, ref _childElementIndex);
        }

        var namespaceLocal = LocalName("__namespace");
        if (contractNamespacesLocal is null)
            AppendLine($"var {namespaceLocal} = {classContract.GeneratedName}.Namespace;");
        else
            AppendLine($"var {namespaceLocal} = {contractNamespacesLocal}[{SymbolDisplay.FormatPrimitive(_typeIndex - 1, true, false)}];");

        AppendLine();

        var classMemberCount = classContract.Members.Length;
        AppendLine($"context.IncrementItemCount({SymbolDisplay.FormatPrimitive(classMemberCount, true, false)});");

        for (var i = 0; i < classMemberCount; i++, memberCount++)
        {
            AppendLine();

            var member = classContract.Members[i];
            var memberType = member.MemberType;

            if (member.IsGetOnlyCollection)
                AppendLine("context.StoreIsGetOnlyCollection();");
            else
                AppendLine("context.ResetIsGetOnlyCollection();");

            var memberValue = default(string);
            var ifNotDefaultBlock = default(CodeWriter.IndentDisposable?);
            if (!member.EmitDefaultValue)
            {
                memberValue = LoadMemberValue(member);
                AppendLine($"if ({memberValue} != default)");
                ifNotDefaultBlock = Block();
            }

            var writeXsiType = CheckIfMemberHasConflict(member, classContract, derivedMostClassContract);
            if (writeXsiType || !TryWritePrimitive(memberType, memberValue, member, ns: namespaceLocal, name: null, memberNamesLocal: memberNamesLocal, nameIndex: i + _childElementIndex))
            {
                WriteStartElement(memberType, classContract.Namespace, memberNamesLocal, namespaceLocal, nameLocal: null, nameIndex: i + _childElementIndex);
                if (classContract.ChildElementNamespaces[i + _childElementIndex] != null)
                    AppendLine($"xmlWriter.WriteNamespaceDecl({childElementNamespacesLocal}[{SymbolDisplay.FormatPrimitive(i + _childElementIndex, true, false)}]);");

                memberValue ??= LoadMemberValue(member);

                var memberContract = GetContract(member.MemberTypeContractId);
                WriteValue(memberValue, memberType, memberContract!, writeXsiType);
                AppendLine("xmlWriter.WriteEndElement();");
            }

            if (classContract.HasExtensionData)
            {
                // TODO: not supported
                //_ilg.Call(thisObj: _contextArg, XmlFormatGeneratorStatics.WriteExtensionDataMethod, _xmlWriterArg, extensionDataLocal, memberCount);
            }

            if (!member.EmitDefaultValue)
            {
                ifNotDefaultBlock.GetValueOrDefault().Dispose();
                if (member.IsRequired)
                {
                    AppendLine("else");
                    using (Block())
                    {
                        AppendLine($"global::Marius.DataContracts.Runtime.XmlObjectSerializerWriteContext.ThrowRequiredMemberMustBeEmitted({SymbolDisplay.FormatLiteral(member.Name, true)}, typeof({classContract.UnderlyingType.FullyQualifiedName}));");
                    }
                }
            }
        }

        _typeIndex++;
        _childElementIndex += classMemberCount;
        return memberCount;
    }

    private bool TryWritePrimitive(TypeSpec type, string? value, DataMemberSpec dataMember, string ns, string? name, string? memberNamesLocal, int nameIndex)
    {
        var memberContract = GetContract(dataMember.MemberTypeContractId);
        if (memberContract is not PrimitiveDataContractSpec primitiveContract || primitiveContract.UnderlyingType.SpecialType == SpecialTypeKind.Object || type.IsNullableValueType)
            return false;
        
        var writeValue = value ?? GetMemberValue("obj", ClassContract.UnderlyingType, dataMember.MemberInfo);
        var nameValue = name ?? $"{memberNamesLocal}[{SymbolDisplay.FormatPrimitive(nameIndex, false, false)}]";
        
        if (type.IsValueType)
            AppendLine($"xmlWriter.{primitiveContract.WriteMethodName}({writeValue}, {nameValue}, {ns});");
        else
            AppendLine($"context.{primitiveContract.WriteMethodName}(xmlWriter, {writeValue}, {nameValue}, {ns});");
        
        return true;
    }

    private string LoadMemberValue(DataMemberSpec member)
    {
        var memberValue = LocalName("__memberValue");
        AppendLine($"var {memberValue} = {GetMemberValue("obj", ClassContract.UnderlyingType, member.MemberInfo)};");
        return memberValue;
    }

    private bool CheckIfMemberHasConflict(DataMemberSpec member, ClassDataContractSpec classContract, ClassDataContractSpec? derivedMostClassContract)
    {
        // Check for conflict with base type members
        if (CheckIfConflictingMembersHaveDifferentTypes(member))
            return true;

        // Check for conflict with derived type members
        var name = member.Name;
        var ns = classContract.XmlNamespace;
        var currentContract = derivedMostClassContract;
        while (currentContract != null && currentContract != classContract)
        {
            if (ns == currentContract.XmlNamespace)
            {
                var members = currentContract.Members;
                for (var j = 0; j < members.Length; j++)
                {
                    if (name == members[j].Name)
                        return CheckIfConflictingMembersHaveDifferentTypes(members[j]);
                }
            }

            if (currentContract.BaseClassContractId >= 0)
                currentContract = GetContract(currentContract.BaseClassContractId) as ClassDataContractSpec;
            else
                currentContract = null;
        }

        return false;
    }

    private static bool CheckIfConflictingMembersHaveDifferentTypes(DataMemberSpec member)
    {
        while (member.ConflictingMember != null)
        {
            if (member.MemberType != member.ConflictingMember.MemberType)
                return true;

            member = member.ConflictingMember;
        }

        return false;
    }
}