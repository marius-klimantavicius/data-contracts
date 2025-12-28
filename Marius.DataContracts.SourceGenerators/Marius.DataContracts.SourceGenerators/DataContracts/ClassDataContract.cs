using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;

namespace Marius.DataContracts.SourceGenerators.DataContracts;

internal partial class ClassDataContract : DataContract
{
    private string?[]? _childElementNamespaces;

    private new ClassDataContractModel Model => Unsafe.As<ClassDataContractModel>(base.Model);

    public string[]? ContractNamespaces => Model.ContractNamespaces;
    public string[]? MemberNames => Model.MemberNames;
    public string[]? MemberNamespaces => Model.MemberNamespaces;

    public string? SerializationExceptionMessage => Model.SerializationExceptionMessage;
    public string? DeserializationExceptionMessage => Model.DeserializationExceptionMessage;

    public bool IsReadOnlyContract => DeserializationExceptionMessage != null;

    public bool IsNonAttributedType => Model.IsNonAttributedType;
    public bool HasExtensionData => Model.HasExtensionData;

    public override DataContract? BaseContract => BaseClassContract;

    public ClassDataContract? BaseClassContract => Model.BaseClassContract;
    public List<DataMember> Members => Model.Members;

    public string?[]? ChildElementNamespaces
    {
        get
        {
            _childElementNamespaces ??= CreateChildElementNamespaces();
            return _childElementNamespaces;
        }
    }

    public IMethodSymbol? OnSerializing => Model.OnSerializing;
    public IMethodSymbol? OnSerialized => Model.OnSerialized;
    public IMethodSymbol? OnDeserializing => Model.OnDeserializing;
    public IMethodSymbol? OnDeserialized => Model.OnDeserialized;
    public IMethodSymbol? ExtensionDataSetMethod => Model.ExtensionDataSetMethod;

    public ClassDataContract(DataContractContext context, ITypeSymbol type)
        : base(new ClassDataContractModel(context, type))
    {
    }

    private ClassDataContract(DataContractContext context, ITypeSymbol type, string ns, string[] memberNames)
        : base(new ClassDataContractModel(context, type, ns, memberNames))
    {
    }

    internal static ClassDataContract CreateClassDataContractForKeyValue(DataContractContext context, ITypeSymbol type, string ns, string[] memberNames)
    {
        return new ClassDataContract(context, type, ns, memberNames);
    }

    internal static void CheckAndAddMember(List<DataMember> members, DataMember memberContract, Dictionary<string, DataMember> memberNamesTable)
    {
        if (memberNamesTable.TryGetValue(memberContract.Name, out var existingMemberContract))
        {
            var declaringType = memberContract.MemberInfo.ContainingType;
            DataContractContext.ThrowInvalidDataContractException(
                SR.Format(declaringType.TypeKind == TypeKind.Enum ? SR.DupEnumMemberValue : SR.DupMemberName,
                    existingMemberContract.MemberInfo.Name,
                    memberContract.MemberInfo.Name,
                    DataContractContext.GetClrTypeFullName(declaringType),
                    memberContract.Name));
        }

        memberNamesTable.Add(memberContract.Name, memberContract);
        members.Add(memberContract);
    }

    internal static string? GetChildNamespaceToDeclare(DataContract dataContract, ITypeSymbol childType)
    {
        var context = dataContract.Model.Context;

        childType = DataContractContext.UnwrapNullableType(childType);
        if (childType.TypeKind != TypeKind.Enum && !context.KnownSymbols.IsIXmlSerializable(childType)
            && context.GetBuiltInDataContract(childType) == null && !context.KnownSymbols.IsDbNull(childType))
        {
            var ns = context.GetXmlName(childType).Namespace;
            if (ns.Length > 0 && ns != dataContract.Namespace)
                return ns;
        }

        return null;
    }

    private string?[] CreateChildElementNamespaces()
    {
        var baseChildElementNamespaces = BaseClassContract?.ChildElementNamespaces;
        var baseChildElementNamespaceCount = baseChildElementNamespaces?.Length ?? 0;
        var childElementNamespaces = new string?[Members.Count + baseChildElementNamespaceCount];
        if (baseChildElementNamespaceCount > 0)
            Array.Copy(baseChildElementNamespaces!, childElementNamespaces, baseChildElementNamespaces!.Length);

        for (var i = 0; i < Members.Count; i++)
            childElementNamespaces[i + baseChildElementNamespaceCount] = GetChildNamespaceToDeclare(this, Members[i].MemberType);

        return childElementNamespaces;
    }
}