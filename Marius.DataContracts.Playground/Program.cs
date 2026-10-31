using System.Numerics;
using System.Runtime.Serialization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Xml;
using System.Xml.Linq;
using Marius.DataContracts.Runtime;
using DataContractSerializer = System.Runtime.Serialization.DataContractSerializer;

namespace Marius.DataContracts.Playground;

class Program
{
    static Task<int> Main(string[] args) => FixedCasesShowcase.RunAsync(args);

    internal static async Task RunOriginalDemoAsync()
    {
        var value = new SimpleContract
        {
            Names = ["hello", "wor\x01ld", "2025"],
            VV = [22, 33, 44],
            LocalName = new LocalName
            {
                Key = Guid.NewGuid().ToString(),
                Id = 337,
            },
            Range = new IpRange<int, decimal>
            {
                From = 0x1234,
                To = 0x7FFF,
            },
            Target = new Uri("http://google.lt"),
            KeyedNames =
            {
                { "First", new LocalName { Id = 1, Key = "One" } },
                { "Second", new LocalName { Id = 2, Key = "Two" } },
            },
            Data = ("hello"u8).ToArray(),
            Raw = XElement.Parse("<div><span class='i'></span> Hello</div>"),
            InnerTypes =
            [
                new LocalName.InnerType<decimal> { Price = 12.34M },
                new LocalName.InnerType<decimal> { Price = 56.78M },
                new LocalName.InnerType<decimal> { Price = 70.50M },
            ],
            Status = Status.Paused,
        };

        var localSerializer = DataContractContext.GetSerializer<SimpleContract>();
        var netSerializer = new DataContractSerializer(typeof(SimpleContract), new Type[] { typeof(uint[]) });

        // Serialize with System.Runtime.Serialization.DataContractSerializer
        using var netStream = new MemoryStream();
        netSerializer.WriteObject(netStream, value);
        netStream.Position = 0;
        var netXml = await new StreamReader(netStream).ReadToEndAsync();

        // Serialize with Marius.DataContracts.Runtime.DataContractSerializer
        using var localStream = new MemoryStream();
        localSerializer.WriteObject(localStream, value);
        localStream.Position = 0;
        var localXml = await new StreamReader(localStream).ReadToEndAsync();

        // Parse both XMLs to XElement for semantic comparison
        var readerSettings = new XmlReaderSettings { CheckCharacters = false };

        using var netReader = XmlReader.Create(new StringReader(netXml), readerSettings);
        var netXElement = XElement.Load(netReader);

        using var localReader = XmlReader.Create(new StringReader(localXml), readerSettings);
        var localXElement = XElement.Load(localReader);

        Console.WriteLine("=== System.Runtime.Serialization.DataContractSerializer XML ===");
        Console.WriteLine(XElementToString(netXElement));
        Console.WriteLine();
        Console.WriteLine("=== Marius.DataContracts.Runtime.DataContractSerializer XML ===");
        Console.WriteLine(XElementToString(localXElement));
        Console.WriteLine();

        // Check if XMLs are semantically equivalent
        var areEquivalent = AreXmlSemanticallyEquivalent(netXElement, localXElement);
        Console.WriteLine($"=== Semantic Equivalence Check ===");
        Console.WriteLine($"XMLs are semantically equivalent: {areEquivalent}");
        ShowcaseChecks.Check(areEquivalent, "The original demo's XML differs from .NET.");

        // Deserialize back to verify round-trip
        netStream.Position = 0;
        localStream.Position = 0;
        var readObject = (SimpleContract)localSerializer.ReadObject(netStream)!;
        Console.WriteLine();
        Console.WriteLine("=== Deserialized object (from .NET serialized XML) ===");
        Console.WriteLine(JsonSerializer.Serialize(readObject, JsonContext.Default.SimpleContract));
    }

    static string XElementToString(XElement element, SaveOptions options = SaveOptions.None)
    {
        var sb = new StringBuilder();
        var writerSettings = new XmlWriterSettings
        {
            CheckCharacters = false,
            OmitXmlDeclaration = true,
            Indent = (options & SaveOptions.DisableFormatting) == 0,
        };
        using var writer = XmlWriter.Create(sb, writerSettings);
        element.WriteTo(writer);
        writer.Flush();
        return sb.ToString();
    }

    static bool AreXmlSemanticallyEquivalent(XElement left, XElement right) => XmlEquivalence.AreEqual(left, right);
}

[JsonSerializable(typeof(SimpleContract))]
public partial class JsonContext : JsonSerializerContext
{
}

[DataContract]
public struct LocalName
{
    [DataContract]
    public class InnerType<T>
    {
        [DataMember]
        public required T Price;
    }

    [DataMember]
    public int Id { get; set; }

    [DataMember]
    public string Key { get; set; }
}

[DataContract]
public class IpRange<T, TT>
    where T : struct
    where TT : struct, INumber<TT>
{
    [DataMember]
    public T From { get; init; }

    [DataMember]
    public required TT To { get; init; }
}

[DataContract]
public class Another<TK, TV>
    where TK : struct
    where TV : struct, INumber<TV>
{
    [DataMember]
    private TK Key;

    [DataMember]
    public required TV Value { get; init; }

    public required TK KeySetter
    {
        get => Key;
        set => Key = value;
    }
}

[DataContract(Name = "S", Namespace = "SourceGenerator.Tests")]
[KnownType(typeof(uint[]))]
public class SimpleContract
{
    [DataMember]
    public required string[] Names { get; set; }

    [DataMember]
    public int[]? Inties { get; set; }

    [DataMember]
    public short[]? VV { get; set; }

    [DataMember]
    public Uri? Target { get; set; }

    [DataMember]
    public int? NullableInt { get; set; }

    [DataMember]
    public LocalName? LocalName { get; set; }

    [DataMember]
    public IpRange<int, decimal>? Range { get; set; }

    [DataMember]
    public Another<int, decimal>? Another { get; set; }

    [DataMember]
    public Another<short, double>? Another2 { get; set; }

    [DataMember]
    [JsonIgnore]
    public XElement? Raw { get; set; }

    private SortedDictionary<string, LocalName>? _keyedNames;

    [DataMember]
    public SortedDictionary<string, LocalName> KeyedNames => _keyedNames ??= new SortedDictionary<string, LocalName>();

    [DataMember]
    public uint[]? Numbers { get; set; }

    [DataMember]
    public required byte[] Data { get; set; }

    [DataMember]
    public ICollection<LocalName.InnerType<decimal>> InnerTypes { get; set; } = [];

    [DataMember]
    public required Status Status { get; set; }
}

[DataContract(Namespace = "urn:helper")]
public enum Status
{
    [EnumMember(Value = "UNKNOWN")]
    Unknown = 0,

    [EnumMember(Value = "RUNNING")]
    Running = 1,

    [EnumMember(Value = "STOPPED")]
    Stopped = 2,

    [EnumMember(Value = "PAUSED")]
    Paused = 3,
}