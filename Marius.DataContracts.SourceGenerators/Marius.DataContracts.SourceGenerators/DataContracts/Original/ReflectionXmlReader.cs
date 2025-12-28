using System.Diagnostics;
using System.Runtime.Serialization;
using System.Xml;

namespace Marius.DataContracts.SourceGenerators.DataContracts;

internal sealed class ReflectionXmlReader : ReflectionReader
{
    protected override void ReflectionReadMembers(XmlReaderDelegator xmlReader, XmlObjectSerializerReadContext context, XmlDictionaryString[] memberNames, XmlDictionaryString[]? memberNamespaces, ClassDataContract classContract, ref object obj)
    {
        Debug.Assert(memberNamespaces != null);

        int memberCount = classContract.MemberNames!.Length;
        context.IncrementItemCount(memberCount);
        int memberIndex = -1;
        int firstRequiredMember;
        _ = GetRequiredMembers(classContract, out firstRequiredMember);
        bool hasRequiredMembers = (firstRequiredMember < memberCount);
        int requiredIndex = hasRequiredMembers ? firstRequiredMember : -1;
        DataMember[] members = new DataMember[memberCount];
        int reflectedMemberCount = ReflectionGetMembers(classContract, members);
        Debug.Assert(reflectedMemberCount == memberCount, "The value returned by ReflectionGetMembers() should equal to memberCount.");
        ExtensionDataObject? extensionData = null;

        if (classContract.HasExtensionData)
        {
            extensionData = new ExtensionDataObject();
            ((IExtensibleDataObject)obj).ExtensionData = extensionData;
        }

        while (true)
        {
            if (!XmlObjectSerializerReadContext.MoveToNextElement(xmlReader))
            {
                return;
            }

            if (hasRequiredMembers)
            {
                memberIndex = context.GetMemberIndexWithRequiredMembers(xmlReader, memberNames, memberNamespaces, memberIndex, requiredIndex, extensionData);
            }
            else
            {
                memberIndex = context.GetMemberIndex(xmlReader, memberNames, memberNamespaces, memberIndex, extensionData);
            }

            // GetMemberIndex returns memberNames.Length if member not found
            if (memberIndex < members.Length)
            {
                ReflectionReadMember(xmlReader, context, classContract, ref obj, memberIndex, members);
                requiredIndex = memberIndex + 1;
            }
        }
    }

    protected override string GetClassContractNamespace(ClassDataContract classContract)
    {
        return classContract.XmlName!.Namespace;
    }

    protected override string GetCollectionContractItemName(CollectionDataContract collectionContract)
    {
        return collectionContract.ItemName;
    }

    protected override string GetCollectionContractNamespace(CollectionDataContract collectionContract)
    {
        return collectionContract.XmlName.Namespace;
    }

    protected override object? ReflectionReadDictionaryItem(XmlReaderDelegator xmlReader, XmlObjectSerializerReadContext context, CollectionDataContract collectionContract)
    {
        Debug.Assert(collectionContract.Kind == CollectionKind.Dictionary || collectionContract.Kind == CollectionKind.GenericDictionary);
        context.ReadAttributes(xmlReader);
        return collectionContract.ItemContract.ReadXmlValue(xmlReader, context);
    }

    private static bool[] GetRequiredMembers(ClassDataContract contract, out int firstRequiredMember)
    {
        int memberCount = contract.MemberNames!.Length;
        bool[] requiredMembers = new bool[memberCount];
        GetRequiredMembers(contract, requiredMembers);
        for (firstRequiredMember = 0; firstRequiredMember < memberCount; firstRequiredMember++)
            if (requiredMembers[firstRequiredMember])
                break;

        return requiredMembers;
    }

    private static int GetRequiredMembers(ClassDataContract contract, bool[] requiredMembers)
    {
        int memberCount = (contract.BaseClassContract == null) ? 0 : GetRequiredMembers(contract.BaseClassContract, requiredMembers);
        List<DataMember> members = contract.Members!;
        for (int i = 0; i < members.Count; i++, memberCount++)
        {
            requiredMembers[memberCount] = members[i].IsRequired;
        }

        return memberCount;
    }
}