namespace Marius.DataContracts.SourceGenerators.Tests;

public record SerializationTestResult
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public bool XmlEquivalent { get; init; }
    public bool DeserializedEqual { get; init; }
    public string? NetSerializerXml { get; init; }
    public string? CustomSerializerXml { get; init; }
    public string? DeserializationDetails { get; init; }
    public string? GeneratedText { get; set; }

    public static SerializationTestResult Failure(string errorMessage) =>
        new SerializationTestResult
        {
            Success = false,
            ErrorMessage = errorMessage,
            XmlEquivalent = false,
            DeserializedEqual = false,
        };

    public static SerializationTestResult Failure(
        string errorMessage,
        bool xmlEquivalent,
        bool deserializedEqual,
        string netXml,
        string customXml,
        string? details = null) =>
        new SerializationTestResult
        {
            Success = false,
            ErrorMessage = errorMessage,
            XmlEquivalent = xmlEquivalent,
            DeserializedEqual = deserializedEqual,
            NetSerializerXml = netXml,
            CustomSerializerXml = customXml,
            DeserializationDetails = details,
        };

    public static SerializationTestResult FromTestOutput(
        bool xmlEquivalent,
        bool deserializedEqual,
        string netXml,
        string customXml,
        string? details = null) =>
        new SerializationTestResult
        {
            Success = true,
            XmlEquivalent = xmlEquivalent,
            DeserializedEqual = deserializedEqual,
            NetSerializerXml = netXml,
            CustomSerializerXml = customXml,
            DeserializationDetails = details,
        };
}