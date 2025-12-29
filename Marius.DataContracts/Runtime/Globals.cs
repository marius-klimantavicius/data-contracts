// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license

using System.Collections;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;

namespace Marius.DataContracts.Runtime;

public static partial class Globals
{
    /// <SecurityNote>
    /// Review - changes to const could affect code generation logic; any changes should be reviewed.
    /// </SecurityNote>
    internal const BindingFlags ScanAllMembers = BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

    private static XmlQualifiedName? _idQualifiedName;
    public static XmlQualifiedName IdQualifiedName => _idQualifiedName ??= new XmlQualifiedName(IdLocalName, SerializationNamespace);
    private static XmlQualifiedName? _refQualifiedName;
    public static XmlQualifiedName RefQualifiedName => _refQualifiedName ??= new XmlQualifiedName(RefLocalName, SerializationNamespace);
    private static Type? _typeOfObject;
    public static Type TypeOfObject => _typeOfObject ??= typeof(object);
    private static Type? _typeOfValueType;
    public static Type TypeOfValueType => _typeOfValueType ??= typeof(ValueType);
    private static Type? _typeOfArray;
    public static Type TypeOfArray => _typeOfArray ??= typeof(Array);
    private static Type? _typeOfString;
    public static Type TypeOfString => _typeOfString ??= typeof(string);
    private static Type? _typeOfInt;
    public static Type TypeOfInt => _typeOfInt ??= typeof(int);
    private static Type? _typeOfULong;
    public static Type TypeOfULong => _typeOfULong ??= typeof(ulong);
    private static Type? _typeOfVoid;
    public static Type TypeOfVoid => _typeOfVoid ??= typeof(void);
    private static Type? _typeOfByteArray;
    public static Type TypeOfByteArray => _typeOfByteArray ??= typeof(byte[]);
    private static Type? _typeOfTimeSpan;
    public static Type TypeOfTimeSpan => _typeOfTimeSpan ??= typeof(TimeSpan);
    private static Type? _typeOfGuid;
    public static Type TypeOfGuid => _typeOfGuid ??= typeof(Guid);
    private static Type? _typeOfDateTimeOffset;
    public static Type TypeOfDateTimeOffset => _typeOfDateTimeOffset ??= typeof(DateTimeOffset);
    private static Type? _typeOfDateOnly;
    public static Type TypeOfDateOnly => _typeOfDateOnly ??= typeof(DateOnly);
    private static Type? _typeOfTimeOnly;
    public static Type TypeOfTimeOnly => _typeOfTimeOnly ??= typeof(TimeOnly);
    private static Type? _typeOfMemoryStream;
    public static Type TypeOfMemoryStream => _typeOfMemoryStream ??= typeof(MemoryStream);
    private static Type? _typeOfUri;
    public static Type TypeOfUri => _typeOfUri ??= typeof(Uri);
    private static Type? _typeOfTypeEnumerable;
    public static Type TypeOfTypeEnumerable => _typeOfTypeEnumerable ??= typeof(IEnumerable<Type>);
    private static Type? _typeOfStreamingContext;
    public static Type TypeOfStreamingContext => _typeOfStreamingContext ??= typeof(StreamingContext);
    private static Type? _typeOfISerializable;
    public static Type TypeOfISerializable => _typeOfISerializable ??= typeof(ISerializable);
    private static Type? _typeOfIDeserializationCallback;
    public static Type TypeOfIDeserializationCallback => _typeOfIDeserializationCallback ??= typeof(IDeserializationCallback);
#pragma warning disable SYSLIB0050 // IObjectReference is obsolete
    private static Type? _typeOfIObjectReference;
    public static Type TypeOfIObjectReference => _typeOfIObjectReference ??= typeof(IObjectReference);
#pragma warning restore SYSLIB0050
    private static Type? _typeOfKnownTypeAttribute;
    public static Type TypeOfKnownTypeAttribute => _typeOfKnownTypeAttribute ??= typeof(KnownTypeAttribute);
    private static Type? _typeOfDataContractAttribute;
    public static Type TypeOfDataContractAttribute => _typeOfDataContractAttribute ??= typeof(DataContractAttribute);
    private static Type? _typeOfDataMemberAttribute;
    public static Type TypeOfDataMemberAttribute => _typeOfDataMemberAttribute ??= typeof(DataMemberAttribute);
    private static Type? _typeOfEnumMemberAttribute;
    public static Type TypeOfEnumMemberAttribute => _typeOfEnumMemberAttribute ??= typeof(EnumMemberAttribute);
    private static Type? _typeOfCollectionDataContractAttribute;
    public static Type TypeOfCollectionDataContractAttribute => _typeOfCollectionDataContractAttribute ??= typeof(CollectionDataContractAttribute);
    private static Type? _typeOfOptionalFieldAttribute;
    public static Type TypeOfOptionalFieldAttribute => _typeOfOptionalFieldAttribute ??= typeof(OptionalFieldAttribute);
    private static Type? _typeOfObjectArray;
    public static Type TypeOfObjectArray => _typeOfObjectArray ??= typeof(object[]);
    private static Type? _typeOfOnSerializingAttribute;
    public static Type TypeOfOnSerializingAttribute => _typeOfOnSerializingAttribute ??= typeof(OnSerializingAttribute);
    private static Type? _typeOfOnSerializedAttribute;
    public static Type TypeOfOnSerializedAttribute => _typeOfOnSerializedAttribute ??= typeof(OnSerializedAttribute);
    private static Type? _typeOfOnDeserializingAttribute;
    public static Type TypeOfOnDeserializingAttribute => _typeOfOnDeserializingAttribute ??= typeof(OnDeserializingAttribute);
    private static Type? _typeOfOnDeserializedAttribute;
    public static Type TypeOfOnDeserializedAttribute => _typeOfOnDeserializedAttribute ??= typeof(OnDeserializedAttribute);
    private static Type? _typeOfFlagsAttribute;
    public static Type TypeOfFlagsAttribute => _typeOfFlagsAttribute ??= typeof(FlagsAttribute);
    private static Type? _typeOfIXmlSerializable;
    public static Type TypeOfIXmlSerializable => _typeOfIXmlSerializable ??= typeof(IXmlSerializable);
    private static Type? _typeOfXmlSchemaProviderAttribute;
    public static Type TypeOfXmlSchemaProviderAttribute => _typeOfXmlSchemaProviderAttribute ??= typeof(XmlSchemaProviderAttribute);
    private static Type? _typeOfXmlRootAttribute;
    public static Type TypeOfXmlRootAttribute => _typeOfXmlRootAttribute ??= typeof(XmlRootAttribute);
    private static Type? _typeOfXmlQualifiedName;
    public static Type TypeOfXmlQualifiedName => _typeOfXmlQualifiedName ??= typeof(XmlQualifiedName);
    private static Type? _typeOfXmlSchemaType;
    public static Type TypeOfXmlSchemaType => _typeOfXmlSchemaType ??= typeof(XmlSchemaType);
    private static Type? _typeOfIExtensibleDataObject;
    public static Type TypeOfIExtensibleDataObject => _typeOfIExtensibleDataObject ??= typeof(IExtensibleDataObject);
    private static Type? _typeOfExtensionDataObject;
    public static Type TypeOfExtensionDataObject => _typeOfExtensionDataObject ??= typeof(ExtensionDataObject);
    private static Type? _typeOfNullable;
    public static Type TypeOfNullable => _typeOfNullable ??= typeof(Nullable<>);
    private static Type? _typeOfReflectionPointer;
    public static Type TypeOfReflectionPointer => _typeOfReflectionPointer ??= typeof(Pointer);
    private static Type? _typeOfIDictionaryGeneric;
    public static Type TypeOfIDictionaryGeneric => _typeOfIDictionaryGeneric ??= typeof(IDictionary<,>);
    private static Type? _typeOfIDictionary;
    public static Type TypeOfIDictionary => _typeOfIDictionary ??= typeof(IDictionary);
    private static Type? _typeOfIListGeneric;
    public static Type TypeOfIListGeneric => _typeOfIListGeneric ??= typeof(IList<>);
    private static Type? _typeOfIList;
    public static Type TypeOfIList => _typeOfIList ??= typeof(IList);
    private static Type? _typeOfICollectionGeneric;
    public static Type TypeOfICollectionGeneric => _typeOfICollectionGeneric ??= typeof(ICollection<>);
    private static Type? _typeOfICollection;
    public static Type TypeOfICollection => _typeOfICollection ??= typeof(ICollection);
    private static Type? _typeOfIEnumerableGeneric;
    public static Type TypeOfIEnumerableGeneric => _typeOfIEnumerableGeneric ??= typeof(IEnumerable<>);
    private static Type? _typeOfIEnumerable;
    public static Type TypeOfIEnumerable => _typeOfIEnumerable ??= typeof(IEnumerable);
    private static Type? _typeOfIEnumeratorGeneric;
    public static Type TypeOfIEnumeratorGeneric => _typeOfIEnumeratorGeneric ??= typeof(IEnumerator<>);
    private static Type? _typeOfIEnumerator;
    public static Type TypeOfIEnumerator => _typeOfIEnumerator ??= typeof(IEnumerator);
    private static Type? _typeOfKeyValuePair;
    public static Type TypeOfKeyValuePair => _typeOfKeyValuePair ??= typeof(KeyValuePair<,>);
    private static Type? _typeOfKeyValue;
    public static Type TypeOfKeyValue => _typeOfKeyValue ??= typeof(KeyValue<,>);
    private static Type? _typeOfIDictionaryEnumerator;
    public static Type TypeOfIDictionaryEnumerator => _typeOfIDictionaryEnumerator ??= typeof(IDictionaryEnumerator);
    private static Type? _typeOfDictionaryGeneric;
    public static Type TypeOfDictionaryGeneric => _typeOfDictionaryGeneric ??= typeof(Dictionary<,>);
    private static Type? _typeOfHashtable;
    public static Type TypeOfHashtable => _typeOfHashtable ??= typeof(Dictionary<object, object>);
    private static Type? _typeOfXmlElement;
    public static Type TypeOfXmlElement => _typeOfXmlElement ??= typeof(XmlElement);
    private static Type? _typeOfXmlNodeArray;
    public static Type TypeOfXmlNodeArray => _typeOfXmlNodeArray ??= typeof(XmlNode[]);
    private static Type? _typeOfDbNull;
    public static Type TypeOfDbNull => _typeOfDbNull ??= typeof(DBNull);

    private static Type? _typeOfISerializableDataNode;
    public static Type TypeOfISerializableDataNode => _typeOfISerializableDataNode ??= typeof(ISerializableDataNode);
    private static Type? _typeOfClassDataNode;
    public static Type TypeOfClassDataNode => _typeOfClassDataNode ??= typeof(ClassDataNode);
    private static Type? _typeOfCollectionDataNode;
    public static Type TypeOfCollectionDataNode => _typeOfCollectionDataNode ??= typeof(CollectionDataNode);
    private static Type? _typeOfXmlDataNode;
    public static Type TypeOfXmlDataNode => _typeOfXmlDataNode ??= typeof(XmlDataNode);

    private static Uri? _dataContractXsdBaseNamespaceUri;
    public static Uri DataContractXsdBaseNamespaceUri => _dataContractXsdBaseNamespaceUri ??= new Uri(DataContractXsdBaseNamespace);
    
    public const bool DefaultIsRequired = false;
    public const bool DefaultEmitDefaultValue = true;
    public const int DefaultOrder = 0;

    public const bool DefaultIsReference = false;

    // The value string.Empty aids comparisons (can do simple length checks
    //     instead of string comparison method calls in IL.)
    public static readonly string NewObjectId = string.Empty;
    public const string? NullObjectId = null;
    public const string FullSrsInternalsVisiblePattern = @"^[\s]*System\.Runtime\.Serialization[\s]*,[\s]*PublicKey[\s]*=[\s]*(?i:00240000048000009400000006020000002400005253413100040000010001008d56c76f9e8649383049f383c44be0ec204181822a6c31cf5eb7ef486944d032188ea1d3920763712ccb12d75fb77e9811149e6148e5d32fbaab37611c1878ddc19e20ef135d0cb2cff2bfec3d115810c3d9069638fe4be215dbf795861920e5ab6f7db2e2ceef136ac23d5dd2bf031700aec232f6c6b1c785b4305c123b37ab)[\s]*$";

    [GeneratedRegex(FullSrsInternalsVisiblePattern)]
    private static partial Regex GetFullSrsInternalsVisibleRegex();

    public static Regex FullSrsInternalsVisibleRegex { get; } = GetFullSrsInternalsVisibleRegex();
    public const char SpaceChar = ' ';
    public const char OpenBracketChar = '[';
    public const char CloseBracketChar = ']';
    public const char CommaChar = ',';
    public const string Space = " ";
    public const string XsiPrefix = "i";
    public const string XsdPrefix = "x";
    public const string SerPrefix = "z";
    public const string SerPrefixForSchema = "ser";
    public const string ElementPrefix = "q";
    public const string DataContractXsdBaseNamespace = "http://schemas.datacontract.org/2004/07/";
    public const string DataContractXmlNamespace = DataContractXsdBaseNamespace + "System.Xml";
    public const string SchemaInstanceNamespace = "http://www.w3.org/2001/XMLSchema-instance";
    public const string SchemaNamespace = "http://www.w3.org/2001/XMLSchema";
    public const string XsiNilLocalName = "nil";
    public const string XsiTypeLocalName = "type";
    public const string TnsPrefix = "tns";
    public const string OccursUnbounded = "unbounded";
    public const string AnyTypeLocalName = "anyType";
    public const string StringLocalName = "string";
    public const string IntLocalName = "int";
    public const string True = "true";
    public const string False = "false";
    public const string ArrayPrefix = "ArrayOf";
    public const string XmlnsNamespace = "http://www.w3.org/2000/xmlns/";
    public const string XmlnsPrefix = "xmlns";
    public const string SchemaLocalName = "schema";
    public const string CollectionsNamespace = "http://schemas.microsoft.com/2003/10/Serialization/Arrays";
    public const string DefaultClrNamespace = "GeneratedNamespace";
    public const string DefaultTypeName = "GeneratedType";
    public const string DefaultGeneratedMember = "GeneratedMember";
    public const string DefaultFieldSuffix = "Field";
    public const string DefaultPropertySuffix = "Property";
    public const string DefaultMemberSuffix = "Member";
    public const string NameProperty = "Name";
    public const string NamespaceProperty = "Namespace";
    public const string OrderProperty = "Order";
    public const string IsReferenceProperty = "IsReference";
    public const string IsRequiredProperty = "IsRequired";
    public const string EmitDefaultValueProperty = "EmitDefaultValue";
    public const string ClrNamespaceProperty = "ClrNamespace";
    public const string ItemNameProperty = "ItemName";
    public const string KeyNameProperty = "KeyName";
    public const string ValueNameProperty = "ValueName";
    public const string SerializationInfoPropertyName = "SerializationInfo";
    public const string SerializationInfoFieldName = "info";
    public const string NodeArrayPropertyName = "Nodes";
    public const string NodeArrayFieldName = "nodesField";
    public const string ExportSchemaMethod = "ExportSchema";
    public const string IsAnyProperty = "IsAny";
    public const string ContextFieldName = "context";
    public const string GetObjectDataMethodName = "GetObjectData";
    public const string GetEnumeratorMethodName = "GetEnumerator";
    public const string MoveNextMethodName = "MoveNext";
    public const string AddValueMethodName = "AddValue";
    public const string CurrentPropertyName = "Current";
    public const string ValueProperty = "Value";
    public const string EnumeratorFieldName = "enumerator";
    public const string SerializationEntryFieldName = "entry";
    public const string ExtensionDataSetMethod = "set_ExtensionData";
    public const string ExtensionDataSetExplicitMethod = "System.Runtime.Serialization.IExtensibleDataObject.set_ExtensionData";
    public const string ExtensionDataObjectPropertyName = "ExtensionData";
    public const string ExtensionDataObjectFieldName = "extensionDataField";
    public const string AddMethodName = "Add";

    public const string GetCurrentMethodName = "get_Current";

    // NOTE: These values are used in schema below. If you modify any value, please make the same change in the schema.
    public const string SerializationNamespace = "http://schemas.microsoft.com/2003/10/Serialization/";
    public const string ClrTypeLocalName = "Type";
    public const string ClrAssemblyLocalName = "Assembly";
    public const string IsValueTypeLocalName = "IsValueType";
    public const string EnumerationValueLocalName = "EnumerationValue";
    public const string SurrogateDataLocalName = "Surrogate";
    public const string GenericTypeLocalName = "GenericType";
    public const string GenericParameterLocalName = "GenericParameter";
    public const string GenericNameAttribute = "Name";
    public const string GenericNamespaceAttribute = "Namespace";
    public const string GenericParameterNestedLevelAttribute = "NestedLevel";
    public const string IsDictionaryLocalName = "IsDictionary";
    public const string ActualTypeLocalName = "ActualType";
    public const string ActualTypeNameAttribute = "Name";
    public const string ActualTypeNamespaceAttribute = "Namespace";
    public const string DefaultValueLocalName = "DefaultValue";
    public const string EmitDefaultValueAttribute = "EmitDefaultValue";
    public const string IdLocalName = "Id";
    public const string RefLocalName = "Ref";
    public const string ArraySizeLocalName = "Size";
    public const string KeyLocalName = "Key";
    public const string ValueLocalName = "Value";
    public const string MscorlibAssemblyName = "0";
    public const string ParseMethodName = "Parse";
    public const string SafeSerializationManagerName = "SafeSerializationManager";
    public const string SafeSerializationManagerNamespace = "http://schemas.datacontract.org/2004/07/System.Runtime.Serialization";
    public const string ISerializableFactoryTypeLocalName = "FactoryType";
}