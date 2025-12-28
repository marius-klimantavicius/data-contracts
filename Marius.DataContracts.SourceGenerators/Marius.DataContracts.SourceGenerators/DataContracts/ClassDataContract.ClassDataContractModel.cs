using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.Serialization;
using System.Xml;
using Microsoft.CodeAnalysis;

namespace Marius.DataContracts.SourceGenerators.DataContracts;

internal partial class ClassDataContract
{
    private sealed class ClassDataContractModel : DataContractModel
    {
        private ClassDataContract? _baseContract;
        private List<DataMember> _members;
        private IMethodSymbol? _onSerializing, _onSerialized;
        private IMethodSymbol? _onDeserializing, _onDeserialized;
        private IMethodSymbol? _extensionDataSetMethod;
        private bool _isMethodChecked;

        private bool _hasDataContract;

        private bool _isKnownTypeAttributeChecked;
        private ImmutableArray<DataContract> _knownDataContracts = ImmutableArray<DataContract>.Empty;

        internal readonly string[]? ContractNamespaces;
        internal readonly string[]? MemberNames;
        internal readonly string[]? MemberNamespaces;

        internal bool IsNonAttributedType { get; private set; }
        internal bool HasExtensionData { get; }
        internal List<DataMember> Members => _members;

        internal string? SerializationExceptionMessage { get; private set; }
        internal string? DeserializationExceptionMessage => SerializationExceptionMessage == null ? null : SR.Format(SR.ReadOnlyClassDeserialization, SerializationExceptionMessage);

        internal IMethodSymbol? OnSerializing
        {
            get
            {
                EnsureMethodsImported();
                return _onSerializing;
            }
        }

        internal IMethodSymbol? OnSerialized
        {
            get
            {
                EnsureMethodsImported();
                return _onSerialized;
            }
        }

        internal IMethodSymbol? OnDeserializing
        {
            get
            {
                EnsureMethodsImported();
                return _onDeserializing;
            }
        }

        internal IMethodSymbol? OnDeserialized
        {
            get
            {
                EnsureMethodsImported();
                return _onDeserialized;
            }
        }

        internal IMethodSymbol? ExtensionDataSetMethod
        {
            get
            {
                EnsureMethodsImported();
                return _extensionDataSetMethod;
            }
        }

        internal ClassDataContract? BaseClassContract
        {
            get => _baseContract;
            set
            {
                _baseContract = value;
                if (_baseContract != null && IsValueType)
                    DataContractContext.ThrowInvalidDataContractException(SR.Format(SR.ValueTypeCannotHaveBaseType, XmlName.Name, XmlName.Namespace, _baseContract.XmlName.Name, _baseContract.XmlName.Namespace));
            }
        }

        public override ImmutableArray<DataContract> KnownDataContracts
        {
            get
            {
                if (!_isKnownTypeAttributeChecked)
                {
                    if (!_isKnownTypeAttributeChecked)
                    {
                        _knownDataContracts = Context.ImportKnownTypeAttributes(UnderlyingType);
                        _isKnownTypeAttributeChecked = true;
                    }
                }

                return _knownDataContracts;
            }

            set => _knownDataContracts = value;
        }

        public ClassDataContractModel(DataContractContext context, ITypeSymbol type)
            : base(context, type)
        {
            var xmlName = GetXmlNameAndSetHasDataContract(type);
            var cmp = SymbolEqualityComparer.Default;
            if (context.KnownSymbols.IsDbNull(type))
            {
                XmlName = xmlName;
                _members = new List<DataMember>();
                Name = XmlName.Name;
                Namespace = XmlName.Namespace;
                ContractNamespaces = MemberNames = MemberNamespaces = Array.Empty<string>();
                EnsureMethodsImported();
                return;
            }

            var baseType = type.BaseType;
            IsISerializable = context.KnownSymbols.IsISerializable(type);
            SetIsNonAttributedType(type);
            if (IsISerializable)
            {
                if (_hasDataContract)
                    throw new InvalidDataContractException(SR.Format(SR.ISerializableCannotHaveDataContract, DataContractContext.GetClrTypeFullName(type)));

                if (baseType != null && !(baseType.IsSerializable && context.KnownSymbols.IsISerializable(baseType)))
                    baseType = null;
            }

            IsValueType = type.IsValueType;
            if (baseType != null && !cmp.Equals(context.KnownSymbols.ObjectType, baseType) && !cmp.Equals(context.KnownSymbols.ValueTypeType, baseType) && !cmp.Equals(context.KnownSymbols.UriType, baseType))
            {
                var baseContract = context.GetDataContract(baseType);
                if (baseContract is CollectionDataContract collectionDC)
                    BaseClassContract = collectionDC.SharedTypeContract as ClassDataContract;
                else
                    BaseClassContract = baseContract as ClassDataContract;

                if (BaseClassContract != null && BaseClassContract.IsNonAttributedType && !IsNonAttributedType)
                    throw new InvalidDataContractException(SR.Format(SR.AttributedTypesCannotInheritFromNonAttributedSerializableTypes, DataContractContext.GetClrTypeFullName(type), DataContractContext.GetClrTypeFullName(baseType)));
            }
            else
            {
                BaseClassContract = null;
            }

            HasExtensionData = context.KnownSymbols.IsIExtensibleDataObject(type);
            if (HasExtensionData && !_hasDataContract && !IsNonAttributedType)
                throw new InvalidDataContractException(SR.Format(SR.OnlyDataContractTypesCanHaveExtensionData, DataContractContext.GetClrTypeFullName(type)));

            if (IsISerializable)
            {
                SetDataContractName(xmlName);
                _members = new List<DataMember>();
            }
            else
            {
                XmlName = xmlName;
                ImportDataMembers();
                Name = XmlName.Name;
                Namespace = XmlName.Namespace;

                var baseMemberCount = 0;
                var baseContractCount = 0;
                if (BaseClassContract == null)
                {
                    MemberNames = new string[Members.Count];
                    MemberNamespaces = new string[Members.Count];
                    ContractNamespaces = new string[1];
                }
                else
                {
                    if (BaseClassContract.IsReadOnlyContract)
                        SerializationExceptionMessage = BaseClassContract.SerializationExceptionMessage;

                    baseMemberCount = BaseClassContract.MemberNames!.Length;
                    MemberNames = new string[Members.Count + baseMemberCount];
                    Array.Copy(BaseClassContract.MemberNames, MemberNames, baseMemberCount);
                    MemberNamespaces = new string[Members.Count + baseMemberCount];
                    Array.Copy(BaseClassContract.MemberNamespaces!, MemberNamespaces, baseMemberCount);
                    baseContractCount = BaseClassContract.ContractNamespaces!.Length;
                    ContractNamespaces = new string[1 + baseContractCount];
                    Array.Copy(BaseClassContract.ContractNamespaces, ContractNamespaces, baseContractCount);
                }

                ContractNamespaces[baseContractCount] = Namespace;
                for (var i = 0; i < Members.Count; i++)
                {
                    MemberNames[i + baseMemberCount] = Members[i].Name;
                    MemberNamespaces[i + baseMemberCount] = Namespace;
                }
            }

            EnsureMethodsImported();
        }

        public ClassDataContractModel(DataContractContext context, ITypeSymbol type, string ns, string[] memberNames) : base(context, type)
        {
            XmlName = new XmlQualifiedName(GetXmlNameAndSetHasDataContract(type).Name, ns);
            ImportDataMembers();
            Name = XmlName.Name;
            Namespace = ns;
            ContractNamespaces = [Namespace];
            MemberNames = new string[Members.Count];
            MemberNamespaces = new string[Members.Count];
            for (var i = 0; i < Members.Count; i++)
            {
                Members[i].Name = memberNames[i];
                MemberNames[i] = Members[i].Name;
                MemberNamespaces[i] = Namespace;
            }

            EnsureMethodsImported();
        }

        [MemberNotNull(nameof(Members))]
        [MemberNotNull(nameof(_members))]
        private void ImportDataMembers()
        {
            var type = UnderlyingType;
            EnsureIsReferenceImported(type);
            var tempMembers = new List<DataMember>();
            var memberNamesTable = new Dictionary<string, DataMember>();

            var cmp = SymbolEqualityComparer.Default;
            var memberInfos = IsNonAttributedType
                ? type.GetMembers().Where(m => cmp.Equals(m.ContainingType, type) && m.DeclaredAccessibility == Accessibility.Public && !m.IsStatic).ToArray()
                : type.GetMembers().Where(m => cmp.Equals(m.ContainingType, type) && !m.IsStatic).ToArray();

            for (var i = 0; i < memberInfos.Length; i++)
            {
                var member = memberInfos[i];
                if (_hasDataContract)
                {
                    var memberAttribute = DataContractContext.GetDataMemberAttribute(member);
                    if (memberAttribute != null)
                    {
                        var memberContract = new DataMember(Context, member);
                        if (member is IPropertySymbol property)
                        {
                            var getMethod = property.GetMethod;
                            if (getMethod != null && IsMethodOverriding(getMethod))
                                continue;

                            var setMethod = property.SetMethod;
                            if (setMethod != null && IsMethodOverriding(setMethod))
                                continue;

                            if (getMethod == null)
                                DataContractContext.ThrowInvalidDataContractException(SR.Format(SR.NoGetMethodForProperty, property.ContainingType, property.Name));

                            if (setMethod == null)
                            {
                                if (!SetIfGetOnlyCollection(memberContract, skipIfReadOnlyContract: false))
                                    SerializationExceptionMessage = SR.Format(SR.NoSetMethodForProperty, property.ContainingType, property.Name);
                            }

                            if (getMethod.Parameters.Length > 0)
                                DataContractContext.ThrowInvalidDataContractException(SR.Format(SR.IndexedPropertyCannotBeSerialized, property.ContainingType, property.Name));
                        }
                        else if (member is not IFieldSymbol)
                        {
                            DataContractContext.ThrowInvalidDataContractException(SR.Format(SR.InvalidMember, DataContractContext.GetClrTypeFullName(type), member.Name));
                        }

                        if (memberAttribute.IsNameSetExplicitly)
                        {
                            if (string.IsNullOrEmpty(memberAttribute.Name))
                                DataContractContext.ThrowInvalidDataContractException(SR.Format(SR.InvalidDataMemberName, member.Name, DataContractContext.GetClrTypeFullName(type)));

                            memberContract.Name = memberAttribute.Name;
                        }
                        else
                        {
                            memberContract.Name = member.Name;
                        }

                        memberContract.Name = DataContractContext.EncodeLocalName(memberContract.Name);
                        memberContract.IsNullable = Context.IsTypeNullable(memberContract.MemberType);
                        memberContract.IsRequired = memberAttribute.IsRequired;
                        if (memberAttribute.IsRequired && IsReference)
                        {
                            DataContractContext.ThrowInvalidDataContractException(
                                SR.Format(SR.IsRequiredDataMemberOnIsReferenceDataContractType,
                                    DataContractContext.GetClrTypeFullName(member.ContainingType!),
                                    member.Name, true));
                        }

                        memberContract.EmitDefaultValue = memberAttribute.EmitDefaultValue;
                        memberContract.Order = memberAttribute.Order;
                        CheckAndAddMember(tempMembers, memberContract, memberNamesTable);
                    }
                }
                else if (IsNonAttributedType)
                {
                    var field = member as IFieldSymbol;
                    var property = member as IPropertySymbol;
                    if ((field == null && property == null) || (field != null && field.IsConst))
                        continue;

                    var ignoreDataMemberAttribute = DataContractContext.GetIgnoreDataMemberAttribute(member);
                    if (ignoreDataMemberAttribute != null)
                        continue;

                    var memberContract = new DataMember(Context, member);
                    if (property != null)
                    {
                        var getMethod = property.GetMethod;
                        if (getMethod == null || IsMethodOverriding(getMethod) || getMethod.Parameters.Length > 0)
                            continue;

                        var setMethod = property.SetMethod;
                        if (setMethod == null)
                        {
                            if (!SetIfGetOnlyCollection(memberContract, skipIfReadOnlyContract: true))
                                continue;
                        }
                        else
                        {
                            if (setMethod.DeclaredAccessibility != Accessibility.Public || IsMethodOverriding(setMethod))
                                continue;
                        }

                        //skip ExtensionData member of type ExtensionDataObject if IExtensibleDataObject is implemented in non-attributed type
                        if (HasExtensionData && Context.KnownSymbols.IsExtensionDataObject(memberContract.MemberType)
                            && member.Name == KnownTypeSymbols.ExtensionDataObjectPropertyName)
                            continue;
                    }

                    memberContract.Name = DataContractContext.EncodeLocalName(member.Name);
                    memberContract.IsNullable = Context.IsTypeNullable(memberContract.MemberType);
                    CheckAndAddMember(tempMembers, memberContract, memberNamesTable);
                }
                else
                {
                    if (member is IFieldSymbol field)
                    {
                        var memberContract = new DataMember(Context, member)
                        {
                            Name = DataContractContext.EncodeLocalName(member.Name),
                        };

                        var optionalFields = DataContractContext.GetOptionalFieldAttribute(field);
                        if (optionalFields == null)
                        {
                            if (IsReference)
                            {
                                DataContractContext.ThrowInvalidDataContractException(
                                    SR.Format(SR.NonOptionalFieldMemberOnIsReferenceSerializableType,
                                        DataContractContext.GetClrTypeFullName(member.ContainingType!),
                                        member.Name, true));
                            }

                            memberContract.IsRequired = true;
                        }

                        memberContract.IsNullable = Context.IsTypeNullable(memberContract.MemberType);
                        CheckAndAddMember(tempMembers, memberContract, memberNamesTable);
                    }
                }
            }

            if (tempMembers.Count > 1)
                tempMembers.Sort(DataMemberComparer.Singleton);

            SetIfMembersHaveConflict(tempMembers);

            Interlocked.MemoryBarrier();
            _members = tempMembers;
            Debug.Assert(Members != null);
        }

        private void SetIfMembersHaveConflict(List<DataMember> members)
        {
            if (BaseClassContract == null)
                return;

            var baseTypeIndex = 0;
            var membersInHierarchy = new List<Member>();
            foreach (var member in members)
                membersInHierarchy.Add(new Member(member, XmlName.Namespace, baseTypeIndex));

            var currContract = BaseClassContract;
            while (currContract != null)
            {
                baseTypeIndex++;

                foreach (var member in currContract.Members)
                    membersInHierarchy.Add(new Member(member, currContract.XmlName.Namespace, baseTypeIndex));

                currContract = currContract.BaseClassContract;
            }

            var comparer = DataMemberConflictComparer.Singleton;
            membersInHierarchy.Sort(comparer);

            for (var i = 0; i < membersInHierarchy.Count - 1; i++)
            {
                var startIndex = i;
                var endIndex = i;
                var hasConflictingType = false;
                while (endIndex < membersInHierarchy.Count - 1
                       && membersInHierarchy[endIndex]._member.Name == membersInHierarchy[endIndex + 1]._member.Name
                       && membersInHierarchy[endIndex]._ns == membersInHierarchy[endIndex + 1]._ns)
                {
                    membersInHierarchy[endIndex]._member.ConflictingMember = membersInHierarchy[endIndex + 1]._member;
                    if (!hasConflictingType)
                    {
                        if (membersInHierarchy[endIndex + 1]._member.HasConflictingNameAndType)
                            hasConflictingType = true;
                        else
                            hasConflictingType = !SymbolEqualityComparer.Default.Equals(membersInHierarchy[endIndex]._member.MemberType, membersInHierarchy[endIndex + 1]._member.MemberType);
                    }

                    endIndex++;
                }

                if (hasConflictingType)
                {
                    for (var j = startIndex; j <= endIndex; j++)
                        membersInHierarchy[j]._member.HasConflictingNameAndType = true;
                }

                i = endIndex + 1;
            }
        }

        private static bool IsMethodOverriding(IMethodSymbol method)
        {
            return method.IsVirtual && method.IsOverride;
        }

        private bool SetIfGetOnlyCollection(DataMember memberContract, bool skipIfReadOnlyContract)
        {
            //OK to call IsCollection here since the use of surrogated collection types is not supported in get-only scenarios
            if (CollectionDataContract.IsCollection(Context, memberContract.MemberType, false /*isConstructorRequired*/, skipIfReadOnlyContract) && !memberContract.MemberType.IsValueType)
            {
                memberContract.IsGetOnlyCollection = true;
                return true;
            }

            return false;
        }

        private void EnsureIsReferenceImported(ITypeSymbol type)
        {
            var isReference = false;
            var hasDataContractAttribute = DataContractContext.TryGetDCAttribute(type, out var dataContractAttribute);

            if (BaseClassContract != null)
            {
                if (hasDataContractAttribute && dataContractAttribute!.IsReferenceSetExplicitly)
                {
                    var baseIsReference = BaseClassContract.IsReference;
                    if ((baseIsReference && !dataContractAttribute.IsReference) ||
                        (!baseIsReference && dataContractAttribute.IsReference))
                    {
                        DataContractContext.ThrowInvalidDataContractException(
                            SR.Format(SR.InconsistentIsReference,
                                DataContractContext.GetClrTypeFullName(type),
                                dataContractAttribute.IsReference,
                                DataContractContext.GetClrTypeFullName(BaseClassContract.UnderlyingType),
                                BaseClassContract.IsReference));
                    }
                    else
                    {
                        isReference = dataContractAttribute.IsReference;
                    }
                }
                else
                {
                    isReference = BaseClassContract.IsReference;
                }
            }
            else if (hasDataContractAttribute)
            {
                if (dataContractAttribute!.IsReference)
                    isReference = dataContractAttribute.IsReference;
            }

            if (isReference && type.IsValueType)
            {
                DataContractContext.ThrowInvalidDataContractException(
                    SR.Format(SR.ValueTypeCannotHaveIsReference,
                        DataContractContext.GetClrTypeFullName(type),
                        true,
                        false));
                return;
            }

            IsReference = isReference;
        }

        private XmlQualifiedName GetXmlNameAndSetHasDataContract(ITypeSymbol type)
        {
            return Context.GetXmlName(type, out _hasDataContract);
        }

        private void SetIsNonAttributedType(ITypeSymbol type)
        {
            IsNonAttributedType = !Context.KnownSymbols.IsSerializable(type) && !_hasDataContract && Context.IsNonAttributedTypeValidForSerialization(type);
        }

        private void EnsureMethodsImported()
        {
            if (!_isMethodChecked && UnderlyingType != null!)
            {
                lock (this)
                {
                    if (!_isMethodChecked)
                    {
                        var type = UnderlyingType;
                        var methods = type.GetMembers().OfType<IMethodSymbol>().Where(s => SymbolEqualityComparer.Default.Equals(type, s.ContainingType) && !s.IsStatic).ToList();
                        for (var i = 0; i < methods.Count; i++)
                        {
                            var method = methods[i];
                            var parameters = method.Parameters;
                            if (HasExtensionData && IsValidExtensionDataSetMethod(method, parameters))
                            {
                                if (method.Name == KnownTypeSymbols.ExtensionDataSetExplicitMethod || method.DeclaredAccessibility != Accessibility.Public)
                                    _extensionDataSetMethod = Context.KnownSymbols.GetExtensionDataSetExplicitMethod();
                                else
                                    _extensionDataSetMethod = method;
                            }

                            string? prevAttributeType = null;
                            if (IsValidCallback(method, parameters, KnownTypeSymbols.HasOnSerializingAttribute, _onSerializing, ref prevAttributeType))
                                _onSerializing = method;
                            if (IsValidCallback(method, parameters, KnownTypeSymbols.HasOnSerializedAttribute, _onSerialized, ref prevAttributeType))
                                _onSerialized = method;
                            if (IsValidCallback(method, parameters, KnownTypeSymbols.HasOnDeserializingAttribute, _onDeserializing, ref prevAttributeType))
                                _onDeserializing = method;
                            if (IsValidCallback(method, parameters, KnownTypeSymbols.HasOnDeserializedAttribute, _onDeserialized, ref prevAttributeType))
                                _onDeserialized = method;
                        }

                        Interlocked.MemoryBarrier();
                        _isMethodChecked = true;
                    }
                }
            }
        }

        private bool IsValidCallback(IMethodSymbol method, ImmutableArray<IParameterSymbol> parameters, in KnownTypeSymbols.HasAttributeCheck hasAttributeCheck, IMethodSymbol? currentCallback, ref string? prevAttributeType)
        {
            if (hasAttributeCheck.HasAttribute(method))
            {
                Debug.Assert(method.ContainingType != null);

                if (currentCallback != null)
                {
                    DataContractContext.ThrowInvalidDataContractException(SR.Format(SR.DuplicateCallback, method, currentCallback, DataContractContext.GetClrTypeFullName(method.ContainingType), hasAttributeCheck.FullAttributeName));
                }
                else if (prevAttributeType != null)
                {
                    DataContractContext.ThrowInvalidDataContractException(SR.Format(SR.DuplicateAttribute, prevAttributeType, hasAttributeCheck.FullAttributeName, DataContractContext.GetClrTypeFullName(method.ContainingType), method));
                }
                else if (method.IsVirtual)
                {
                    DataContractContext.ThrowInvalidDataContractException(SR.Format(SR.CallbacksCannotBeVirtualMethods, method, DataContractContext.GetClrTypeFullName(method.ContainingType), hasAttributeCheck.FullAttributeName));
                }
                else
                {
                    if (method.ReturnType.SpecialType != SpecialType.System_Void)
                        DataContractContext.ThrowInvalidDataContractException(SR.Format(SR.CallbackMustReturnVoid, DataContractContext.GetClrTypeFullName(method.ContainingType), method));
                    if (parameters == null || parameters.Length != 1 || !Context.KnownSymbols.IsStreamingContext(parameters[0].Type))
                        DataContractContext.ThrowInvalidDataContractException(SR.Format(SR.CallbackParameterInvalid, DataContractContext.GetClrTypeFullName(method.ContainingType), method, "System.Runtime.Serialization.StreamingContext"));

                    prevAttributeType = hasAttributeCheck.FullAttributeName;
                }

                return true;
            }

            return false;
        }

        private bool IsValidExtensionDataSetMethod(IMethodSymbol method, ImmutableArray<IParameterSymbol> parameters)
        {
            if (method.Name == KnownTypeSymbols.ExtensionDataSetExplicitMethod || method.Name == KnownTypeSymbols.ExtensionDataSetMethod)
            {
                Debug.Assert(method.ContainingType != null);

                if (_extensionDataSetMethod != null)
                    DataContractContext.ThrowInvalidDataContractException(SR.Format(SR.DuplicateExtensionDataSetMethod, method, _extensionDataSetMethod, DataContractContext.GetClrTypeFullName(method.ContainingType)));
                if (method.ReturnType.SpecialType == SpecialType.System_Void)
                    DataContractContext.ThrowInvalidDataContractException(SR.Format(SR.ExtensionDataSetMustReturnVoid, DataContractContext.GetClrTypeFullName(method.ContainingType), method));
                if (parameters == null || parameters.Length != 1 || !Context.KnownSymbols.IsExtensionDataObject(parameters[0].Type))
                    DataContractContext.ThrowInvalidDataContractException(SR.Format(SR.ExtensionDataSetParameterInvalid, DataContractContext.GetClrTypeFullName(method.ContainingType), method, "System.Runtime.Serialization.ExtensionDataObject"));
                return true;
            }

            return false;
        }
    }

    internal readonly struct Member
    {
        internal Member(DataMember member, string ns, int baseTypeIndex)
        {
            _member = member;
            _ns = ns;
            _baseTypeIndex = baseTypeIndex;
        }

        internal readonly DataMember _member;
        internal readonly string _ns;
        internal readonly int _baseTypeIndex;
    }

    internal sealed class DataMemberConflictComparer : IComparer<Member>
    {
        public int Compare(Member x, Member y)
        {
            var nsCompare = string.CompareOrdinal(x._ns, y._ns);
            if (nsCompare != 0)
                return nsCompare;

            var nameCompare = string.CompareOrdinal(x._member.Name, y._member.Name);
            if (nameCompare != 0)
                return nameCompare;

            return x._baseTypeIndex - y._baseTypeIndex;
        }

        internal static readonly DataMemberConflictComparer Singleton = new DataMemberConflictComparer();
    }

    internal sealed class DataMemberComparer : IComparer<DataMember>
    {
        public int Compare(DataMember? x, DataMember? y)
        {
            if (x == null && y == null)
                return 0;
            if (x == null || y == null)
                return -1;

            var orderCompare = (int)(x.Order - y.Order);
            if (orderCompare != 0)
                return orderCompare;

            return string.CompareOrdinal(x.Name, y.Name);
        }

        internal static readonly DataMemberComparer Singleton = new DataMemberComparer();
    }
}