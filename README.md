# Source generator based DataContractSerializer

Usage:

```
using Marius.DataContracts.Runtime;

...
// Use DataContractSerializer from Marius.DataContracts.Runtime
var serializer = new DataContractSerializer(new DataContractProvider(DataContractContext.DataContracts, DataContractContext.TypeDataContracts), typeof(RootContract));
serializer.WriteObject(...);

// or

serializer.ReadObject(...);
```

Notes:

* All contract types must be statically discoverable via attributes - add `KnownType` attributes for any dynamic contract
* Some features are not supported (i.e. OnSerializing callbacks, surrogate provider, data contract resolvers, etc), 
only the ones that are needed for my purposes are implemented or tested
