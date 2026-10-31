using System.Runtime.Serialization;
using System.Xml;
using Marius.DataContracts.Runtime;
using Marius.DataContracts.Runtime.Adapters;
using static Marius.DataContracts.Playground.ShowcaseChecks;
using GeneratedSerializer = Marius.DataContracts.Runtime.DataContractSerializer;

namespace Marius.DataContracts.Playground;

internal static class TypedReadShowcase
{
    public static void Run(ShowcaseChecks checks)
    {
        checks.Run("Typed reader and explicit replacement", "Read returns the original typed value; an out result carries an unrelated replacement before callbacks finish on the original.", () =>
        {
            var plainXml = Write(true, new TypedReadValue { Number = 42 });
            var value = ReadOriginal<TypedReadValue>(plainXml, out var absent);
            Check(value.Number == 42 && !absent.HasReplacement, "Ordinary reader must return T without an object replacement.");

            TypedReadReplacement.Trace.Clear();

            var xml = Write(true, new TypedReadReplacement { Number = 7 });
            var original = ReadOriginal<TypedReadReplacement>(xml, out var replacement);
            Check(original.Number == 117 && replacement.HasReplacement && replacement.Value is string text && text == "resolved:7", "Replacement or later original-instance mutation differs.");
            Check(TypedReadReplacement.Trace.SequenceEqual(["resolve:7", "interface:7", "after:17"]), "Replacement/callback lifecycle order differs.");

            var serializer = new GeneratedSerializer(DataContractContext.DataContractProvider, typeof(TypedReadReplacement));
            using var reader = XmlReader.Create(new StringReader(xml));
            Check(serializer.ReadObject(reader) is string resolved && resolved == "resolved:7", "The object boundary must select the replacement.");

            // This demonstrates the representation, not an IObjectReference null return,
            // which is rejected by the serializer to match the framework.
            var explicitNull = new ReadReplacement(true, null);
            Check(explicitNull.HasReplacement && explicitNull.Value is null && !absent.HasReplacement, "Presence must be independent of the replacement's null value.");
            Console.WriteLine($"  Typed original: {value.Number}; original after callbacks: {original.Number}; replacement: {replacement.Value}");
            Console.WriteLine("  default(ReadReplacement) is absent; new ReadReplacement(true, null) is explicitly present.");
        });

        checks.Run("Typed adapter contracts", "Wire readers and adapter conversions stay typed for DateTimeOffset and MemoryStream; ordinary member/collection reads use the validated typed context route.", () =>
        {
            var date = new DateTimeOffset(2026, 10, 24, 12, 34, 56, TimeSpan.FromHours(5.5)).AddTicks(789);
            var restored = ReadAdapted<DateTimeOffsetAdapter, DateTimeOffset>(Write(true, date));
            Check(restored.EqualsExact(date), "Typed adapter conversion lost ticks/offset.");

            using var stream = new MemoryStream([1, 2, 3, 4]);
            stream.Position = 2;

            using var reconstructed = ReadAdapted<MemoryStreamAdapter, MemoryStream>(Write(true, stream));
            Check(reconstructed.ToArray().SequenceEqual(stream.ToArray()) && reconstructed.Position == stream.Position, "Typed MemoryStream adapter conversion lost state.");
            checks.Parity(new TypedReadEnvelope { Value = new TypedReadValue { Number = 9 }, Date = date, Dates = [date] }, result => Check(result.Value.Number == 9 && result.Date.EqualsExact(date) && result.Dates.Single().EqualsExact(date), "Generated member/collection consumption lost typed values."));
            Console.WriteLine("  ClassDataContract<DateTimeOffsetAdapter, DateTimeOffset>: typed wire Read + typed ReadAdapter.");
            Console.WriteLine("  ReadObject still returns object and boxes value roots; allocation evidence uses separate warmed probes.");
        });
    }

    private static TValue ReadAdapted<TWire, TValue>(string xml)
    {
        var contract = (ClassDataContract<TWire, TValue>)((IDataContractProvider)DataContractContext.DataContractProvider).GetDataContract(typeof(TValue));
        var serializer = new GeneratedSerializer(DataContractContext.DataContractProvider, typeof(TValue));
        var context = XmlObjectSerializerReadContext.CreateContext(serializer, contract);
        
        using var reader = XmlReader.Create(new StringReader(xml));
        reader.MoveToContent();
        // This is the validated typed route used by generated members/items.
        return context.Read<TValue>(new XmlReaderDelegator(reader), reader.LocalName, reader.NamespaceURI, contract)!;
    }

    // Low-level generated reader demonstration: the caller supplies the entered
    // element and initialized context. Normal members use context.Read<T>, which
    // also handles xsi:type, nil/ref validation and known-type scopes.
    private static T ReadOriginal<T>(string xml, out ReadReplacement replacement)
    {
        var contract = (ClassDataContract<T>)((IDataContractProvider)DataContractContext.DataContractProvider).GetDataContract(typeof(T));
        var serializer = new GeneratedSerializer(DataContractContext.DataContractProvider, typeof(T));
        var context = XmlObjectSerializerReadContext.CreateContext(serializer, contract);
        
        using var reader = XmlReader.Create(new StringReader(xml));
        reader.MoveToContent();

        var delegator = new XmlReaderDelegator(reader);
        context.ReadAttributes(delegator);
        delegator.Read();

        var original = contract.Read(delegator, context, contract.MemberNames, contract.MemberNamespaces, out replacement);
        delegator.ReadEndElement();
        return original;
    }
}

[DataContract(Namespace = "urn:typed-read-playground")]
internal struct TypedReadValue
{
    [DataMember] public int Number;
}

[DataContract(Namespace = "urn:typed-read-playground")]
internal sealed class TypedReadEnvelope
{
    [DataMember] public TypedReadValue Value;
    [DataMember] public DateTimeOffset Date;
    [DataMember] public List<DateTimeOffset> Dates = [];
}

#pragma warning disable SYSLIB0050 // Deliberate framework-compatible replacement/callback example.
[DataContract(Namespace = "urn:typed-read-playground")]
internal sealed class TypedReadReplacement : IObjectReference, IDeserializationCallback
{
    internal static readonly List<string> Trace = [];
    [DataMember] public int Number;

    public object GetRealObject(StreamingContext context)
    {
        Trace.Add($"resolve:{Number}");
        return $"resolved:{Number}";
    }

    public void OnDeserialization(object? sender)
    {
        Trace.Add($"interface:{Number}");
        Number += 10;
    }

    [OnDeserialized]
    private void OnRead(StreamingContext context)
    {
        Trace.Add($"after:{Number}");
        Number += 100;
    }
}
#pragma warning restore SYSLIB0050