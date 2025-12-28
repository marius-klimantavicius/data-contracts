using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Xml;
using Microsoft.CodeAnalysis;

namespace Marius.DataContracts.SourceGenerators.DataContracts;

internal class EnumDataContract : DataContract
{
    public const string SignedByteLocalName = "byte";
    public const string UnsignedByteLocalName = "unsignedByte";
    public const string ShortLocalName = "short";
    public const string UnsignedShortLocalName = "unsignedShort";
    public const string IntLocalName = "int";
    public const string UnsignedIntLocalName = "unsignedInt";
    public const string LongLocalName = "long";
    public const string UnsignedLongLocalName = "unsignedLong";

    private new EnumDataContractModel Model => Unsafe.As<EnumDataContractModel>(base.Model);

    public override DataContract BaseContract => Model.BaseContract;
    public override bool CanContainReferences => false;

    public XmlQualifiedName BaseContractName => Model.BaseContractName;
    public List<DataMember> Members => Model.Members;
    public List<long>? Values => Model.Values;
    public bool IsFlags => Model.IsFlags;
    public bool IsULong => Model.IsULong;
    public string[]? ChildElementNames => Model.ChildElementNames;

    public EnumDataContract(DataContractContext context, INamedTypeSymbol type)
        : base(new EnumDataContractModel(context, type))
    {
    }

    private sealed class EnumDataContractModel : DataContractModel
    {
        private static readonly Dictionary<SpecialType, (XmlQualifiedName, Func<DataContractContext, DataContract>)> _specialTypeToName = new Dictionary<SpecialType, (XmlQualifiedName, Func<DataContractContext, DataContract>)>();

        private List<DataMember> _members;
        private readonly bool _hasDataContract;

        internal DataContract BaseContract { get; }

        internal XmlQualifiedName BaseContractName => BaseContract.XmlName;

        internal List<DataMember> Members
        {
            get => _members;
            set => _members = value;
        }

        internal List<long>? Values { get; set; }
        internal bool IsFlags { get; set; }
        internal bool IsULong { get; set; }
        internal string[]? ChildElementNames { get; set; }

        static EnumDataContractModel()
        {
            Add(SpecialType.System_SByte, SignedByteDataContract.LocalName, s => s.GetPrimitiveDataContract(s.KnownSymbols.SByteType)!); // "byte"
            Add(SpecialType.System_Byte, UnsignedByteDataContract.LocalName, s => s.GetPrimitiveDataContract(s.KnownSymbols.ByteType)!); // "unsignedByte"
            Add(SpecialType.System_Int16, ShortDataContract.LocalName, s => s.GetPrimitiveDataContract(s.KnownSymbols.Int32Type)!); // "short"
            Add(SpecialType.System_UInt16, UnsignedShortDataContract.LocalName, s => s.GetPrimitiveDataContract(s.KnownSymbols.UInt16Type)!); // "unsignedShort"
            Add(SpecialType.System_Int32, IntDataContract.LocalName, s => s.GetPrimitiveDataContract(s.KnownSymbols.Int32Type)!); // "int"
            Add(SpecialType.System_UInt32, UnsignedIntDataContract.LocalName, s => s.GetPrimitiveDataContract(s.KnownSymbols.UInt32Type)!); // "unsignedInt"
            Add(SpecialType.System_Int64, LongDataContract.LocalName, s => s.GetPrimitiveDataContract(s.KnownSymbols.Int64Type)!); // "long"
            Add(SpecialType.System_UInt64, UnsignedLongDataContract.LocalName, s => s.GetPrimitiveDataContract(s.KnownSymbols.UInt64Type)!); // "unsignedLong"

            static void Add(SpecialType specialType, string localName, Func<DataContractContext, DataContract> create)
            {
                var xmlName = DataContractContext.CreateQualifiedName(localName, DataContractContext.SchemaNamespace);
                _specialTypeToName.Add(specialType, (xmlName, create));
            }
        }

        public EnumDataContractModel(DataContractContext context, INamedTypeSymbol type)
            : base(context, type)
        {
            XmlName = context.GetXmlName(type, out _hasDataContract);
            var baseType = type.EnumUnderlyingType!;
            var baseTypeName = GetBaseContractName(baseType, out var baseContract);
            BaseContract = baseContract;
            // Setting XmlName might be redundant. But I don't want to miss an edge case.
            BaseContract.XmlName = baseTypeName;
            ImportBaseType(baseType);
            IsFlags = DataContractContext.HasFlagsAttribute(type);
            ImportDataMembers();

            Name = XmlName.Name;
            Namespace = XmlName.Namespace;
            ChildElementNames = new string[Members.Count];
            for (var i = 0; i < Members.Count; i++)
                ChildElementNames[i] = Members[i].Name;

            if (DataContractContext.TryGetDCAttribute(type, out var dataContractAttribute))
            {
                if (dataContractAttribute.IsReference)
                {
                    DataContractContext.ThrowInvalidDataContractException(
                        SR.Format(SR.EnumTypeCannotHaveIsReference,
                            DataContractContext.GetClrTypeFullName(type),
                            dataContractAttribute.IsReference,
                            false));
                }
            }
        }

        private XmlQualifiedName GetBaseContractName(ITypeSymbol symbol, out DataContract baseContract)
        {
            if (_specialTypeToName.TryGetValue(symbol.SpecialType, out var item))
            {
                baseContract = item.Item2(Context);
                return item.Item1;
            }

            Debug.Fail("Enum underlying type should always be a special type.");
            baseContract = new IntDataContract(Context);
            return DataContractContext.CreateQualifiedName("int", DataContractContext.SchemaNamespace);
        }

        private void ImportBaseType(ITypeSymbol baseType)
        {
            IsULong = baseType.SpecialType == SpecialType.System_UInt64;
        }

        [MemberNotNull(nameof(_members))]
        private void ImportDataMembers()
        {
            var type = UnderlyingType;
            var fields = type.GetMembers().OfType<IFieldSymbol>().Where(s => s.IsStatic && s.DeclaredAccessibility == Accessibility.Public).ToArray();
            var memberValuesTable = new Dictionary<string, DataMember>();
            var tempMembers = new List<DataMember>(fields.Length);
            var tempValues = new List<long>(fields.Length);

            for (var i = 0; i < fields.Length; i++)
            {
                var field = fields[i];
                var enumMemberValid = false;
                if (_hasDataContract)
                {
                    var memberAttribute = DataContractContext.GetEnumMemberAttribute(field);
                    if (memberAttribute != null)
                    {
                        var memberContract = new DataMember(Context, field);
                        if (memberAttribute.IsValueSetExplicitly)
                        {
                            if (string.IsNullOrEmpty(memberAttribute.Value))
                                DataContractContext.ThrowInvalidDataContractException(SR.Format(SR.InvalidEnumMemberValue, field.Name, DataContractContext.GetClrTypeFullName(type)));

                            memberContract.Name = memberAttribute.Value;
                        }
                        else
                        {
                            memberContract.Name = field.Name;
                        }

                        memberContract.Order = IsULong ? (long)Convert.ToUInt64(field.ConstantValue) : Convert.ToInt64(field.ConstantValue);
                        ClassDataContract.CheckAndAddMember(tempMembers, memberContract, memberValuesTable);
                        enumMemberValid = true;
                    }

                    var dataMemberAttribute = DataContractContext.GetDataMemberAttribute(field);
                    if (dataMemberAttribute != null)
                        DataContractContext.ThrowInvalidDataContractException(SR.Format(SR.DataMemberOnEnumField, DataContractContext.GetClrTypeFullName(field.ContainingType!), field.Name));
                }
                else
                {
                    var memberContract = new DataMember(Context, field)
                    {
                        Name = field.Name,
                        Order = IsULong ? (long)Convert.ToUInt64(field.ConstantValue) : Convert.ToInt64(field.ConstantValue),
                    };

                    ClassDataContract.CheckAndAddMember(tempMembers, memberContract, memberValuesTable);
                    enumMemberValid = true;
                }

                if (enumMemberValid)
                {
                    var enumValue = field.ConstantValue;
                    if (IsULong)
                        tempValues.Add((long)Convert.ToUInt64(enumValue, null));
                    else
                        tempValues.Add(Convert.ToInt64(enumValue, null));
                }
            }

            Interlocked.MemoryBarrier();
            _members = tempMembers;
            Values = tempValues;
        }
    }
}