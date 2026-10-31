using System.Runtime.Serialization;
using Marius.DataContracts.Playground.Cases;
using static Marius.DataContracts.Playground.ShowcaseChecks;

namespace Marius.DataContracts.Playground;

internal static class FeatureShowcase
{
    public static void Run(ShowcaseChecks checks)
    {
        checks.Run("Serialization callbacks", "Private inherited callbacks mutate the original and run in framework order for each individual operation.", () =>
        {
            var framework = WriteCallbacks(false);
            var generated = WriteCallbacks(true);
            Check(framework.Trace.SequenceEqual(generated.Trace), "Write callback order differs from .NET.");
            Check(framework.Value == 41 && generated.Value == framework.Value, "OnSerializing must mutate the original before writing members.");
            Check(framework.Completed && generated.Completed, "OnSerialized did not run on the original.");
            checks.CompareXml(framework.Xml, generated.Xml);

            foreach (var xml in new[] { framework.Xml, generated.Xml })
            {
                var expected = ReadCallbacks(false, xml);
                var actual = ReadCallbacks(true, xml);
                Check(expected.Trace.SequenceEqual(actual.Trace), "Read callback order differs from .NET.");
                Check(expected.Value == 41 && actual.Value == expected.Value && expected.Marker == actual.Marker && actual.Marker == "complete", "OnDeserializing initialization or OnDeserialized state differs.");
            }
        });

        checks.Run("DateTimeOffset adapter", "The framework DateTime/OffsetMinutes contract preserves ticks and offsets in roots, nullable members, lists and object positions.", () =>
        {
            var value = new DateTimeOffset(2026, 10, 24, 12, 34, 56, TimeSpan.FromHours(5.5)).AddTicks(789);
            foreach (var item in new[] { value, default, DateTimeOffset.MinValue, DateTimeOffset.MaxValue })
                checks.Parity(item, result => Check(result.Ticks == item.Ticks && result.Offset == item.Offset, "DateTimeOffset root lost ticks or offset."));

            checks.Parity(new AdapterExample { Timestamp = value, Optional = value, Values = [value, default], Boxed = value }, result =>
                Check(result.Timestamp.EqualsExact(value) && result.Optional is { } optional && optional.EqualsExact(value) &&
                    result.Values.Count == 2 && result.Values[0].EqualsExact(value) && result.Values[1].EqualsExact(default) &&
                    result.Boxed is DateTimeOffset boxed && boxed.EqualsExact(value), "DateTimeOffset member/collection/polymorphic state differs."));

            checks.Parity(new AdapterExample(), result => Check(result.Optional is null && result.Timestamp.EqualsExact(default), "Nullable/default DateTimeOffset state differs."));
        });

        checks.Run("MemoryStream adapter", "Stream XML and reconstructed bytes, length, capacity, position, writable and exposable state follow .NET, including a sliced buffer.", () =>
        {
            foreach (var create in new Func<MemoryStream>[]
                     {
                         () =>
                         {
                             var stream = new MemoryStream(32);
                             stream.Write([1, 2, 3, 4]);
                             stream.Position = 2;
                             return stream;
                         },
                         () => new MemoryStream([1, 2, 3, 4], writable: false),
                         () => new MemoryStream([90, 1, 2, 3, 91], 1, 3, writable: true, publiclyVisible: true) { Position = 1 },
                     })
            {
                using var original = create();
                using var baseline = Read<MemoryStream>(false, Write(false, original));
                var expected = StreamState(baseline);

                checks.Parity(original, result => Check(StreamState(result) == expected, "Reconstructed MemoryStream state differs from .NET."));
                checks.Parity(new AdapterExample { Stream = original }, result => Check(result.Stream is { } stream && StreamState(stream) == expected, "MemoryStream member state differs from .NET."));
            }
        });

        checks.Run("Surrogate provider: runtime selection", "The provider chooses between two generated wire contracts at runtime; both readers rebuild the source type.", () =>
        {
            var provider = new PersonSurrogate();
            foreach (var alternative in new[] { false, true })
            {
                provider.UseAlternative = alternative;
                checks.Parity(new SurrogatePerson { Name = "Ada", Number = 42 }, result => Check(result.Name == "Ada" && result.Number == 42, "Surrogate source state differs."), surrogate: provider);
            }
        });

        checks.Run("Surrogate provider: replacement identity", "Two members and collection entries resolve to one replacement object when references are preserved.", () =>
        {
            var shared = new SurrogatePerson { Name = "shared", Number = 7 };
            checks.Parity(new SurrogateExample { First = shared, Second = shared, People = [shared, shared] }, result =>
                Check(result.First.Name == "shared" && result.First.Number == 7 && ReferenceEquals(result.First, result.Second) &&
                    result.People.Count == 2 && ReferenceEquals(result.First, result.People[0]) && ReferenceEquals(result.First, result.People[1]),
                    "Surrogate replacement aliases differ."), new DataContractSerializerSettings { PreserveObjectReferences = true }, new PersonSurrogate());
        });

        checks.Run("Surrogate provider: generated-set boundary", "A valid implicit wire class stays outside generated metadata and is rejected explicitly.", () =>
        {
            Check(!Runtime.DataContractContext.DataContractProvider.TryGetDataContract(typeof(OutsideSurrogateWire), out _), "The unsupported example must really be outside the generated set.");

            var provider = new PersonSurrogate { UseOutside = true };
            var original = new SurrogatePerson { Name = "outside", Number = 9 };
            var frameworkXml = Write(false, original, surrogate: provider);
            var frameworkResult = Read<SurrogatePerson>(false, frameworkXml, surrogate: provider);
            Check(frameworkResult.Name == "outside" && frameworkResult.Number == 9, "The implicit contract must be valid for the framework.");

            var error = Throws<InvalidDataContractException>(() => Write(true, original, surrogate: provider));
            Check(error.Message.Contains(nameof(OutsideSurrogateWire), StringComparison.Ordinal) && error.Message.Contains("generated", StringComparison.OrdinalIgnoreCase), "Rejection must identify the missing generated contract.");

            var readError = Throws<InvalidDataContractException>(() => Read<SurrogatePerson>(true, frameworkXml, surrogate: provider));
            Check(readError.Message.Contains(nameof(OutsideSurrogateWire), StringComparison.Ordinal), "Read must also reject an unknown replacement contract.");
        });
    }

    private static (string Xml, string[] Trace, int Value, bool Completed) WriteCallbacks(bool generated)
    {
        CallbackExample.Trace.Clear();
        var value = new CallbackExample { Value = 40 };
        var xml = Write(generated, value);
        return (xml, CallbackExample.Trace.ToArray(), value.Value, value.Completed);
    }

    private static (string[] Trace, int Value, string? Marker) ReadCallbacks(bool generated, string xml)
    {
        CallbackExample.Trace.Clear();
        var value = Read<CallbackExample>(generated, xml);
        return (CallbackExample.Trace.ToArray(), value.Value, value.Marker);
    }

    // Observe without reading, moving, writing or disposing streams still used by another comparison.
    private static (string Bytes, long Length, int Capacity, long Position, bool Writable, bool Exposable, int BufferOffset, int BufferCount) StreamState(MemoryStream value)
    {
        var exposable = value.TryGetBuffer(out var buffer);
        return (Convert.ToHexString(value.ToArray()), value.Length, value.Capacity, value.Position, value.CanWrite, exposable, exposable ? buffer.Offset : -1, exposable ? buffer.Count : -1);
    }
}