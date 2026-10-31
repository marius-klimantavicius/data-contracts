using System.Collections;
using System.Runtime.Serialization;
using System.Xml;
using Marius.DataContracts.Playground.Cases.First;

namespace Marius.DataContracts.Playground.Cases
{
    [DataContract]
    internal sealed class SurrogatePerson
    {
        [DataMember] public string Name { get; set; } = "";
        [DataMember] public int Number { get; set; }
    }

    [DataContract]
    internal sealed class PersonWire
    {
        [DataMember] public string Label { get; set; } = "";
        [DataMember] public int Code { get; set; }
    }

    [DataContract]
    internal sealed class AlternativePersonWire
    {
        [DataMember] public string Label { get; set; } = "";
        [DataMember] public int Code { get; set; }
        [DataMember] public int Version { get; set; } = 2;
    }

    [DataContract]
    internal sealed class SurrogateExample
    {
        [DataMember] public SurrogatePerson First { get; set; } = null!;
        [DataMember] public SurrogatePerson Second { get; set; } = null!;
        [DataMember] public List<SurrogatePerson> People { get; set; } = [];
    }

    // No DataContract attribute or static contract member reaches this implicit type.
    // Mentioning it only in provider code deliberately leaves it outside generated metadata.
    public sealed class OutsideSurrogateWire
    {
        public string Label { get; set; } = "";
        public int Code { get; set; }
    }

    internal sealed class PersonSurrogate : ISerializationSurrogateProvider
    {
        public bool UseAlternative { get; set; }
        public bool UseOutside { get; set; }

        public Type GetSurrogateType(Type type) => type != typeof(SurrogatePerson) ? type :
            UseOutside ? typeof(OutsideSurrogateWire) :
            UseAlternative ? typeof(AlternativePersonWire) : typeof(PersonWire);

        public object GetObjectToSerialize(object obj, Type targetType) => obj is not SurrogatePerson source ? obj :
            UseOutside ? new OutsideSurrogateWire { Label = source.Name, Code = source.Number } :
            UseAlternative ? new AlternativePersonWire { Label = source.Name, Code = source.Number } :
            new PersonWire { Label = source.Name, Code = source.Number };

        public object GetDeserializedObject(object obj, Type targetType) => obj switch
        {
            PersonWire wire => new SurrogatePerson { Name = wire.Label, Number = wire.Code },
            AlternativePersonWire wire => new SurrogatePerson { Name = wire.Label, Number = wire.Code },
            OutsideSurrogateWire wire => new SurrogatePerson { Name = wire.Label, Number = wire.Code },
            _ => obj,
        };
    }

    [DataContract, KnownType(typeof(DateTimeOffset)), KnownType(typeof(MemoryStream))]
    internal sealed class AdapterExample
    {
        [DataMember] public DateTimeOffset Timestamp { get; set; }
        [DataMember] public DateTimeOffset? Optional { get; set; }
        [DataMember] public List<DateTimeOffset> Values { get; set; } = [];
        [DataMember] public object? Boxed { get; set; }
        [DataMember] public MemoryStream? Stream { get; set; }
    }

    [DataContract]
    internal class CallbackBase
    {
        [DataMember] public int Value { get; set; }
        public bool Completed { get; private set; }
        public string? Marker { get; protected set; }

        [OnSerializing] private void BeforeWrite(StreamingContext context)
        {
            CallbackExample.Trace.Add($"base:serializing:{Value}:{Describe(context)}");
            Value++;
        }

        [OnSerialized] private void AfterWrite(StreamingContext context)
        {
            CallbackExample.Trace.Add($"base:serialized:{Value}");
            Completed = true;
        }

        [OnDeserializing] private void BeforeRead(StreamingContext context)
        {
            CallbackExample.Trace.Add($"base:deserializing:{Value}:{Describe(context)}");
            Value = -1;
            Marker = "initialized";
        }

        [OnDeserialized] private void AfterRead(StreamingContext context)
        {
            CallbackExample.Trace.Add($"base:deserialized:{Value}");
            Marker = "complete";
        }

        private static string Describe(StreamingContext context)
        {
#pragma warning disable SYSLIB0050 // Observe the callback context supplied by both serializers.
            return $"{context.State}:{context.Context is null}";
#pragma warning restore SYSLIB0050
        }
    }

    [DataContract]
    internal sealed class CallbackExample : CallbackBase
    {
        internal static List<string> Trace { get; } = [];
        [OnSerializing] private void BeforeWrite(StreamingContext context) => Trace.Add($"derived:serializing:{Value}");
        [OnSerialized] private void AfterWrite(StreamingContext context) => Trace.Add($"derived:serialized:{Value}");
        [OnDeserializing] private void BeforeRead(StreamingContext context) => Trace.Add($"derived:deserializing:{Value}");
        [OnDeserialized] private void AfterRead(StreamingContext context) => Trace.Add($"derived:deserialized:{Value}");
    }

    [DataContract]
    internal class HiddenBase
    {
        [DataMember] public int Value { get; set; }
        [DataMember] public int Field;
    }

    [DataContract]
    internal sealed class HiddenDerived : HiddenBase
    {
        [DataMember] public new int Value { get; set; }
        [DataMember] public new int Field;
    }

    [DataContract]
    internal class Animal
    {
        [DataMember] public string Name { get; set; } = "";
    }

    [DataContract]
    internal sealed class Dog : Animal
    {
        [DataMember] public int Age { get; set; }
    }

    [DataContract]
    internal sealed class AnimalEnvelope
    {
        // Dog is generated, but intentionally not a KnownType here.
        [DataMember] public Animal Value { get; set; } = null!;
    }

    [DataContract(Name = "Shared", Namespace = "urn:playground:scope")]
    internal sealed class ScopedNumber
    {
        [DataMember] public int Value { get; set; }
    }

    [DataContract(Name = "Shared", Namespace = "urn:playground:scope")]
    internal sealed class ScopedText
    {
        [DataMember] public string Value { get; set; } = "";
    }

    [DataContract, KnownType(typeof(ScopedNumber))]
    internal sealed class NumberEnvelope
    {
        [DataMember] public object Value { get; set; } = null!;
    }

    [DataContract, KnownType(typeof(ScopedText))]
    internal sealed class TextEnvelope
    {
        [DataMember] public object Value { get; set; } = null!;
    }

    [DataContract]
    internal sealed class SeparateScopes
    {
        [DataMember] public NumberEnvelope First { get; set; } = null!;
        [DataMember] public TextEnvelope Second { get; set; } = null!;
    }

    [DataContract]
    // ReSharper disable once DefaultStructEqualityIsUsed.Global
    internal struct Point
    {
        [DataMember] public int X { get; set; }
    }

    [DataContract]
    internal sealed class UnusualEquality
    {
        [DataMember] public int Value { get; set; }

        // Deliberately misleading operators: serialization must not use these
        // to decide whether a reference is null.
        public static bool operator ==(UnusualEquality? a, UnusualEquality? b) => true;
        public static bool operator !=(UnusualEquality? a, UnusualEquality? b) => false;
        public override bool Equals(object? obj) => true;
        public override int GetHashCode() => 0;
    }

    [DataContract]
    internal sealed class Defaults
    {
        [DataMember(EmitDefaultValue = false)] public Point Point { get; set; }
        [DataMember(EmitDefaultValue = false)] public UnusualEquality? Reference { get; set; }
        [DataMember(EmitDefaultValue = false)] public int? Optional { get; set; }
    }

    [DataContract]
    internal sealed class RequiredDefault
    {
        [DataMember(EmitDefaultValue = false, IsRequired = true)]
        public int Value { get; set; }
    }

    [DataContract]
    internal sealed class InternalState
    {
        [DataMember] private readonly int secret;
        [DataMember] public int PublicField;
        [DataMember] public string Name { get; init; } = "";
        public int Secret => secret;
        public InternalState(int secret) => this.secret = secret;
    }

    [DataContract]
    internal sealed class CollisionEnvelope
    {
        [DataMember] public Model First { get; set; } = new Model();
        [DataMember] public Second.Model Second { get; set; } = new Second.Model();
        [DataMember] public Box<Model> Generic { get; set; } = new Box<Model>();
        [DataMember] public Model[] Array { get; set; } = [];
        [DataMember] public ModelArray NamedLikeArray { get; set; } = new ModelArray();
    }

    [DataContract]
    internal sealed class Box<T>
    {
        [DataMember] public T Value { get; init; } = default!;
    }

    [DataContract]
    internal sealed class ModelArray
    {
        [DataMember] public int Value { get; set; }
    }

    [DataContract]
    internal sealed class ListQuota
    {
        [DataMember] public List<int> Values { get; set; } = [];
    }

    [DataContract]
    internal sealed class ArrayQuota
    {
        [DataMember] public int[] Values { get; set; } = [];
    }

    [DataContract]
    internal sealed class EnumerableQuota
    {
        [DataMember] public IntSequence Values { get; set; } = new IntSequence();
    }

    public sealed class IntSequence : IEnumerable<int>
    {
        private readonly List<int> items = [];
        public void Add(int value) => items.Add(value);
        public IEnumerator<int> GetEnumerator() => items.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    [DataContract]
    internal sealed class DictionaryQuota
    {
        [DataMember] public Dictionary<string, int> Values { get; set; } = [];
    }

    [DataContract]
    internal sealed class GraphNode
    {
        [DataMember] public int Value { get; set; }
        [DataMember] public GraphNode? Next { get; set; }
    }

    [DataContract]
    internal sealed class DictionaryGraph
    {
        [DataMember] public Dictionary<string, GraphNode> Items { get; set; } = [];
        [DataMember] public GraphNode Shared { get; set; } = null!;
    }

    [DataContract]
    internal sealed class OracleState
    {
        [DataMember] public string Text { get; set; } = "";
        [DataMember] public DateTime Timestamp { get; set; }
        public int Ignored { get; set; }
    }

    internal sealed class UnsupportedResolver : DataContractResolver
    {
        public override bool TryResolveType(Type type,
            Type? declaredType,
            DataContractResolver knownTypeResolver,
            out XmlDictionaryString? typeName,
            out XmlDictionaryString? typeNamespace)
        {
            typeName = typeNamespace = null;
            return false;
        }

        public override Type? ResolveName(string typeName,
            string? typeNamespace,
            Type? declaredType,
            DataContractResolver knownTypeResolver) => null;
    }
}

namespace Marius.DataContracts.Playground.Cases.First
{
    [DataContract] internal sealed class Model
    {
        [DataMember] public int Value { get; set; }
    }
}

namespace Marius.DataContracts.Playground.Cases.Second
{
    [DataContract] internal sealed class Model
    {
        [DataMember] public string Value { get; set; } = "";
    }
}

namespace Marius.DataContracts.Playground.Cases.@namespace
{
    [DataContract]
    internal sealed class @class
    {
        [DataMember] public int @event { get; set; }
        [DataMember] public string @return = "";
    }
}