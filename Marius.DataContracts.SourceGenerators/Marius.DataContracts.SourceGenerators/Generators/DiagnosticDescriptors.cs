using Microsoft.CodeAnalysis;

namespace Marius.DataContracts.SourceGenerators.Generators;

internal static class DiagnosticDescriptors
{
    public const string SourceGenerationName = "Marius.DataContracts.SourceGenerators";

    // Errors (DCS1xxx)
    public static DiagnosticDescriptor InvalidDataContract { get; } = DiagnosticDescriptorHelper.Create(
        id: "DCS1001",
        title: "Invalid data contract",
        messageFormat: "{0}",
        category: SourceGenerationName,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static DiagnosticDescriptor TypeNotSerializable { get; } = DiagnosticDescriptorHelper.Create(
        id: "DCS1002",
        title: "Type is not serializable",
        messageFormat: "Type '{0}' is not serializable. Consider marking it with [DataContract] attribute or ensuring it has a public parameterless constructor.",
        category: SourceGenerationName,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static DiagnosticDescriptor InvalidDataMember { get; } = DiagnosticDescriptorHelper.Create(
        id: "DCS1003",
        title: "Invalid data member",
        messageFormat: "Invalid data member '{0}' on type '{1}': {2}",
        category: SourceGenerationName,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static DiagnosticDescriptor DuplicateDataMemberName { get; } = DiagnosticDescriptorHelper.Create(
        id: "DCS1004",
        title: "Duplicate data member name",
        messageFormat: "The data member name '{0}' is already used on type '{1}'.",
        category: SourceGenerationName,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static DiagnosticDescriptor InvalidCollectionContract { get; } = DiagnosticDescriptorHelper.Create(
        id: "DCS1005",
        title: "Invalid collection contract",
        messageFormat: "{0}",
        category: SourceGenerationName,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static DiagnosticDescriptor InvalidEnumMember { get; } = DiagnosticDescriptorHelper.Create(
        id: "DCS1006",
        title: "Invalid enum member",
        messageFormat: "Invalid enum member '{0}' on type '{1}': {2}",
        category: SourceGenerationName,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static DiagnosticDescriptor InvalidCallback { get; } = DiagnosticDescriptorHelper.Create(
        id: "DCS1007",
        title: "Invalid serialization callback",
        messageFormat: "Invalid serialization callback '{0}' on type '{1}': {2}",
        category: SourceGenerationName,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static DiagnosticDescriptor UnsupportedType { get; } = DiagnosticDescriptorHelper.Create(
        id: "DCS1008",
        title: "Unsupported type",
        messageFormat: "Type '{0}' is not supported for data contract serialization.",
        category: SourceGenerationName,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static DiagnosticDescriptor InvalidNamespace { get; } = DiagnosticDescriptorHelper.Create(
        id: "DCS1009",
        title: "Invalid namespace",
        messageFormat: "{0}",
        category: SourceGenerationName,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static DiagnosticDescriptor SchemaProviderMethodNotFound { get; } = DiagnosticDescriptorHelper.Create(
        id: "DCS1010",
        title: "Schema provider method not found",
        messageFormat: "Method {0} as specified by XmlSchemaProviderAttribute was not found on {1}",
        category: SourceGenerationName,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static DiagnosticDescriptor InvalidReturnTypeOnGetSchemaMethod { get; } = DiagnosticDescriptorHelper.Create(
        id: "DCS1011",
        title: "Invalid return type on GetSchema method",
        messageFormat: System.SR.InvalidReturnTypeOnGetSchemaMethod,
        category: SourceGenerationName,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);
    
    public static DiagnosticDescriptor UnexpectedError { get; } = DiagnosticDescriptorHelper.Create(
        id: "DCS1099",
        title: "Unexpected error during source generation",
        messageFormat: "An unexpected error occurred while processing type '{0}': {1}",
        category: SourceGenerationName,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    // Warnings (DCS2xxx)
    public static DiagnosticDescriptor NoParameterlessConstructor { get; } = DiagnosticDescriptorHelper.Create(
        id: "DCS2001",
        title: "No parameterless constructor",
        messageFormat: "Type '{0}' does not have a parameterless constructor. Deserialization may fail.",
        category: SourceGenerationName,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public static DiagnosticDescriptor PropertyWithoutSetter { get; } = DiagnosticDescriptorHelper.Create(
        id: "DCS2002",
        title: "Property without setter",
        messageFormat: "Property '{0}' on type '{1}' does not have a setter. It will be serialized but not deserialized.",
        category: SourceGenerationName,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public static DiagnosticDescriptor NonPublicDataMember { get; } = DiagnosticDescriptorHelper.Create(
        id: "DCS2003",
        title: "Non-public data member",
        messageFormat: "Data member '{0}' on type '{1}' is not public. Using UnsafeAccessor for access.",
        category: SourceGenerationName,
        defaultSeverity: DiagnosticSeverity.Info,
        isEnabledByDefault: true);

    // Internal Errors (DCS3xxx) - Issues with source generation that should be reported
    public static DiagnosticDescriptor ISerializableConstructorNotFound { get; } = DiagnosticDescriptorHelper.Create(
        id: "DCS3001",
        title: "ISerializable constructor not found",
        messageFormat: "Type '{0}' implements ISerializable but does not have a serialization constructor. Assuming that one exists, the generated code will fail at runtime if the constructor is missing.",
        category: SourceGenerationName,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: false);

    public static DiagnosticDescriptor ExtensionDataNotSupported { get; } = DiagnosticDescriptorHelper.Create(
        id: "DCS3002",
        title: "ExtensionData not supported",
        messageFormat: "ExtensionData reading is not yet implemented for type '{0}'.",
        category: SourceGenerationName,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static DiagnosticDescriptor MemberCannotBeSet { get; } = DiagnosticDescriptorHelper.Create(
        id: "DCS3003",
        title: "Member cannot be set",
        messageFormat: "Member '{0}' on type '{1}' cannot be set. No accessible setter found.",
        category: SourceGenerationName,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static DiagnosticDescriptor UnsupportedMemberKind { get; } = DiagnosticDescriptorHelper.Create(
        id: "DCS3004",
        title: "Unsupported member kind",
        messageFormat: "Unsupported member kind '{0}' for member '{1}' on type '{2}'.",
        category: SourceGenerationName,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static DiagnosticDescriptor KeyValuePairContractNotFound { get; } = DiagnosticDescriptorHelper.Create(
        id: "DCS3005",
        title: "KeyValuePair contract not found",
        messageFormat: "Failed to get KeyValuePair contract for dictionary type '{0}'.",
        category: SourceGenerationName,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    // Warnings for not yet implemented features (DCS4xxx)
    public static DiagnosticDescriptor AdaptersNotImplemented { get; } = DiagnosticDescriptorHelper.Create(
        id: "DCS4001",
        title: "Adapters not implemented",
        messageFormat: "Adapters are not yet implemented for type '{0}'.",
        category: SourceGenerationName,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public static DiagnosticDescriptor OnDeserializingCallbackNotImplemented { get; } = DiagnosticDescriptorHelper.Create(
        id: "DCS4002",
        title: "OnDeserializing callback not implemented",
        messageFormat: "OnDeserializing callback is not yet implemented for type '{0}'.",
        category: SourceGenerationName,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public static DiagnosticDescriptor OnDeserializedCallbackNotImplemented { get; } = DiagnosticDescriptorHelper.Create(
        id: "DCS4003",
        title: "OnDeserialized callback not implemented",
        messageFormat: "OnDeserialized callback is not yet implemented for type '{0}'.",
        category: SourceGenerationName,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public static DiagnosticDescriptor OnSerializingCallbackNotImplemented { get; } = DiagnosticDescriptorHelper.Create(
        id: "DCS4004",
        title: "OnSerializing callback not implemented",
        messageFormat: "OnSerializing callback is not yet implemented for type '{0}'.",
        category: SourceGenerationName,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public static DiagnosticDescriptor OnSerializedCallbackNotImplemented { get; } = DiagnosticDescriptorHelper.Create(
        id: "DCS4005",
        title: "OnSerialized callback not implemented",
        messageFormat: "OnSerialized callback is not yet implemented for type '{0}'.",
        category: SourceGenerationName,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);
    
    public static DiagnosticDescriptor KnownTypeMethodNotSupported { get; } = DiagnosticDescriptorHelper.Create(
        id: "DCS4006",
        title: "KnownType method name not supported",
        messageFormat: "KnownType attribute with method name is not yet supported on type '{0}'.",
        category: SourceGenerationName,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    internal static partial class DiagnosticDescriptorHelper
    {
        public static DiagnosticDescriptor Create(
            string id,
            LocalizableString title,
            LocalizableString messageFormat,
            string category,
            DiagnosticSeverity defaultSeverity,
            bool isEnabledByDefault,
            LocalizableString? description = null,
            params string[] customTags)
        {
            return new DiagnosticDescriptor(id, title, messageFormat, category, defaultSeverity, isEnabledByDefault, description, null, customTags);
        }
    }
}