# Source generator based DataContractSerializer

Usage:

```
using Marius.DataContracts.Runtime;

...
// Use DataContractSerializer from Marius.DataContracts.Runtime
var serializer = new DataContractSerializer(DataContractContext.DataContractProvider, typeof(RootContract));
serializer.WriteObject(...);

// or

serializer.ReadObject(...);
```

Notes:

* Contract metadata is generated from `[DataContract]` types and their statically reachable dependencies. Polymorphic contracts also need the framework's ordinary known-type scope, through `[KnownType]` or serializer settings. Having generated metadata does not itself grant that scope.
* Attribute callbacks (`OnSerializing`, `OnSerialized`, `OnDeserializing`, `OnDeserialized`) use generated calls, including private and inherited callbacks and callbacks declared on closed generic contract types. Generic callback methods are explicitly unsupported (`DCS1007`). The supplied streaming context follows the framework serializer.
* `DateTimeOffset` and `MemoryStream` use the framework adapter XML contracts. A reconstructed stream follows framework state, which can discard original spare capacity and expandability. Closed streams and invalid adapter payloads follow framework failure behavior.
* Runtime `ISerializationSurrogateProvider` implementations are supported within the generated contract set. Configure a serializer with `serializer.SetSerializationSurrogateProvider(provider)`; retrieve it with `GetSerializationSurrogateProvider()`, or remove it by setting `null`. These instance methods correspond to the framework extension API. Configure the provider before resolving the serializer's lazily cached root contract. Provider decisions execute at runtime.
* Surrogate source, wire, returned object types, dependencies and closed generic contracts must already be generated. A valid implicit class mentioned only inside a provider is not automatically generated. Unknown types throw `InvalidDataContractException` naming the missing generated contract; serializer known types do not generate metadata dynamically. A `[DataContract]` anchor with members of required closed generic types can make those dependencies statically reachable.
* Custom `DataContractResolver`, extension data and method-based `[KnownType]` remain unsupported. A custom resolver setting throws `NotSupportedException` explicitly. Surrogate providers do not enable custom resolvers.
* Preserved adapter references have a framework quirk: later references can expose a wire adapter through `object`, or fail casts through a typed stream member. This implementation follows that behavior, but leaked generated adapters have different CLR identities from the framework's internal adapters. Exact identity parity for these leaked objects and their subsequent use remains unsupported. Standalone `KeyValuePair<TKey,TValue>` serialization also has an existing data-loss gap outside this phase.

Generated class readers return their declared `T` and expose object replacement separately:

```csharp
T original = contract.Read(reader, context, contract.MemberNames, contract.MemberNamespaces, out ReadReplacement replacement);

// Only at an object-valued boundary (which boxes value-type originals):
object? result = replacement.HasReplacement ? replacement.Value : original;
```

This is a low-level reader call with an entered element and initialized context; ordinary generated members/items use `context.Read<T>` for validation and element handling.

`Read` is a `ClassReader<T>` delegate with the final `out ReadReplacement` parameter. `default(ReadReplacement)` means no replacement; `new ReadReplacement(true, null)` means an explicit null replacement. Check `HasReplacement`, never use null as the sentinel. A framework-style `IObjectReference` null return is still rejected. Replacement resolution runs before the later deserialization callbacks, which target the original instance.

Adapters use `ClassDataContract<TWire,TValue>`, with a `TWire`-returning `Read`, `Func<TWire,TValue> ReadAdapter` and `Func<TValue,TWire> WriteAdapter`. Generated DateTimeOffset/MemoryStream readers read the wire type and convert once after its callbacks. Eligible statically resolved value members and collection items use a validated typed context path. Polymorphic values, active surrogate providers and custom context/contract subclasses retain object dispatch. Public `ReadObject()` returns `object?` and boxes value roots; reference-cache identities and object-based replacement/provider APIs can also require boxing. See [the typed-read evidence](docs/verification/typed-read-phase-results.md) for measured allocation boundaries.

Verification:

* `dotnet run --project Marius.DataContracts.Playground` demonstrates the fixed serialization cases against .NET. Add `-- --xml --verification-tests` to inspect XML and run the compiler-caching/oracle checks. See the [Playground guide](Marius.DataContracts.Playground/README.md).
* `dotnet test Marius.DataContracts.SourceGenerators/Marius.DataContracts.SourceGenerators.Tests/Marius.DataContracts.SourceGenerators.Tests.csproj` runs the parity suite on .NET 9 and .NET 10.

The native gate uses framework DataContractSerializer only in separate managed oracle processes. Feature-specific evidence, tested boundaries and adapter reference caveats are recorded in [the phase verification report](docs/verification/features-phase-results.md).
