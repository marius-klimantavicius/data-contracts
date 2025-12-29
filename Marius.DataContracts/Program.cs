using System.Numerics;
using System.Runtime.Serialization;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Xml.Linq;
using Marius.DataContracts.Runtime;

namespace Marius.DataContracts;

class Program
{
    static async Task Main(string[] args)
    {
        var value = new SimpleContract
        {
            Names = ["hello", "world", "2025"],
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
            InnerTypes =
            [
                new LocalName.InnerType<decimal> { Price = 12.34M },
                new LocalName.InnerType<decimal> { Price = 56.78M },
                new LocalName.InnerType<decimal> { Price = 70.50M },
            ],
            Status = Status.Paused,
        };

        var localSerializer = new Marius.DataContracts.Runtime.DataContractSerializer(new DataContractProvider(Marius.DataContracts.Runtime.DataContractContext.DataContracts, Runtime.DataContractContext.TypeDataContracts), typeof(SimpleContract));
        var netSerializer = new global::System.Runtime.Serialization.DataContractSerializer(typeof(SimpleContract), new Type[] { typeof(uint[]) });

        // Serialize with System.Runtime.Serialization.DataContractSerializer
        var netStream = new MemoryStream();
        netSerializer.WriteObject(netStream, value);
        netStream.Position = 0;
        var netXml = await new StreamReader(netStream).ReadToEndAsync();

        // Serialize with Marius.DataContracts.Runtime.DataContractSerializer
        var localStream = new MemoryStream();
        localSerializer.WriteObject(localStream, value);
        localStream.Position = 0;
        var localXml = await new StreamReader(localStream).ReadToEndAsync();

        // Parse both XMLs to XElement for semantic comparison
        var netXElement = XElement.Parse(netXml);
        var localXElement = XElement.Parse(localXml);

        Console.WriteLine("=== System.Runtime.Serialization.DataContractSerializer XML ===");
        Console.WriteLine(netXElement.ToString(SaveOptions.None));
        Console.WriteLine();
        Console.WriteLine("=== Marius.DataContracts.Runtime.DataContractSerializer XML ===");
        Console.WriteLine(localXElement.ToString(SaveOptions.None));
        Console.WriteLine();

        // Check if XMLs are semantically equivalent
        var areEquivalent = AreXmlSemanticallyEquivalent(netXElement, localXElement);
        Console.WriteLine($"=== Semantic Equivalence Check ===");
        Console.WriteLine($"XMLs are semantically equivalent: {areEquivalent}");

        // Deserialize back to verify round-trip
        netStream.Position = 0;
        localStream.Position = 0;
        var readObject = (SimpleContract)localSerializer.ReadObject(netStream)!;
        Console.WriteLine();
        Console.WriteLine("=== Deserialized object (from .NET serialized XML) ===");
        Console.WriteLine(JsonSerializer.Serialize(readObject, JsonContext.Default.SimpleContract));
    }

    /// <summary>
    /// Checks if two XElements are semantically equivalent.
    /// This compares element names (including namespace), attributes, and content,
    /// ignoring differences in namespace prefix names and attribute order.
    /// </summary>
    static bool AreXmlSemanticallyEquivalent(XElement elem1, XElement elem2)
    {
        // Compare element names (includes namespace)
        if (elem1.Name != elem2.Name)
        {
            Console.WriteLine($"Element name mismatch: {elem1.Name} vs {elem2.Name}");
            return false;
        }

        // Compare attributes (ignoring namespace declaration attributes and order)
        var attrs1 = elem1.Attributes()
            .Where(a => !a.IsNamespaceDeclaration)
            .OrderBy(a => a.Name.NamespaceName)
            .ThenBy(a => a.Name.LocalName)
            .ToList();
        var attrs2 = elem2.Attributes()
            .Where(a => !a.IsNamespaceDeclaration)
            .OrderBy(a => a.Name.NamespaceName)
            .ThenBy(a => a.Name.LocalName)
            .ToList();

        if (attrs1.Count != attrs2.Count)
        {
            Console.WriteLine($"Attribute count mismatch in {elem1.Name}: {attrs1.Count} vs {attrs2.Count}");
            return false;
        }

        for (var i = 0; i < attrs1.Count; i++)
        {
            if (attrs1[i].Name != attrs2[i].Name || attrs1[i].Value != attrs2[i].Value)
            {
                Console.WriteLine($"Attribute mismatch: {attrs1[i].Name}='{attrs1[i].Value}' vs {attrs2[i].Name}='{attrs2[i].Value}'");
                return false;
            }
        }

        // Compare text content (trimmed) if there are no child elements
        var elements1 = elem1.Elements().ToList();
        var elements2 = elem2.Elements().ToList();

        if (elements1.Count == 0 && elements2.Count == 0)
        {
            // Compare text content
            var text1 = elem1.Value.Trim();
            var text2 = elem2.Value.Trim();
            if (text1 != text2)
            {
                Console.WriteLine($"Text content mismatch in {elem1.Name}: '{text1}' vs '{text2}'");
                return false;
            }

            return true;
        }

        // Compare child elements
        if (elements1.Count != elements2.Count)
        {
            Console.WriteLine($"Child element count mismatch in {elem1.Name}: {elements1.Count} vs {elements2.Count}");
            return false;
        }

        for (var i = 0; i < elements1.Count; i++)
        {
            if (!AreXmlSemanticallyEquivalent(elements1[i], elements2[i]))
            {
                return false;
            }
        }

        return true;
    }
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
    public required List<string> Names { get; set; }

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