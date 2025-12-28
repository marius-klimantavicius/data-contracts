using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Marius.DataContracts.SourceGenerators.DataContracts;
using Marius.DataContracts.SourceGenerators.Generators;
using Marius.DataContracts.SourceGenerators.Specs;

namespace Marius.DataContracts.SourceGenerators;

[Generator]
public class DataContractGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var knownSymbols = context
            .CompilationProvider
            .Select((compilation, _) => new KnownTypeSymbols(compilation));

        var provider = context
            .SyntaxProvider
            .ForAttributeWithMetadataName(
                "System.Runtime.Serialization.DataContractAttribute",
                (_, _) => true,
                (s, m) => s.TargetSymbol
            )
            .Collect();

        var combined = knownSymbols.Combine(provider);

        // Transform: Parse Roslyn symbols into immutable Spec models
        var contractSetProvider = combined.Select(static (tuple, cancellationToken) =>
        {
            var (knownSymbols, array) = tuple;
            var dataContractContext = new DataContractContext(knownSymbols);
            var errors = new List<(ISymbol Symbol, Exception Exception)>();

            // Add all primitive contracts
            dataContractContext.GetDataContract(knownSymbols.ObjectType);
            dataContractContext.GetDataContract(knownSymbols.StringType);
            dataContractContext.GetDataContract(knownSymbols.BooleanType);
            dataContractContext.GetDataContract(knownSymbols.ByteType);
            dataContractContext.GetDataContract(knownSymbols.SByteType);
            dataContractContext.GetDataContract(knownSymbols.Int16Type);
            dataContractContext.GetDataContract(knownSymbols.UInt16Type);
            dataContractContext.GetDataContract(knownSymbols.Int32Type);
            dataContractContext.GetDataContract(knownSymbols.UInt32Type);
            dataContractContext.GetDataContract(knownSymbols.Int64Type);
            dataContractContext.GetDataContract(knownSymbols.UInt64Type);
            dataContractContext.GetDataContract(knownSymbols.SingleType);
            dataContractContext.GetDataContract(knownSymbols.DoubleType);
            dataContractContext.GetDataContract(knownSymbols.DecimalType);
            dataContractContext.GetDataContract(knownSymbols.CharType);
            dataContractContext.GetDataContract(knownSymbols.DateTimeType);
            dataContractContext.GetDataContract(knownSymbols.GuidType!);
            dataContractContext.GetDataContract(knownSymbols.TimeSpanType!);
            dataContractContext.GetDataContract(knownSymbols.UriType!);
            dataContractContext.GetDataContract(knownSymbols.ByteArrayType!);

            foreach (var symbol in array.OrderBy(s => s.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)))
            {
                if (symbol is not INamedTypeSymbol type)
                    continue;

                try
                {
                    dataContractContext.GetDataContract(type);
                }
                catch (Exception ex) when (!ExceptionUtility.IsFatal(ex))
                {
                    errors.Add((symbol, ex));
                }
            }

            var visited = new HashSet<DataContract>(ReferenceEqualityComparer.Instance);
            while (true)
            {
                if (!ResolveContractDependencies(dataContractContext, visited, errors))
                    break;
            }

            var parser = new DataContractParser(dataContractContext);
            foreach (var (symbol, ex) in errors)
            {
                parser.ReportDiagnostic(
                    DiagnosticDescriptors.InvalidDataContract,
                    symbol,
                    ex.Message);
            }

            return parser.Parse();
        });

        context.RegisterSourceOutput(contractSetProvider, GenerateSource);
    }

    /// <summary>
    /// Force resolution of all dependent contracts to ensure they are included in the output.
    /// This is necessary because some contracts (like KeyValue for dictionaries) are lazily created.
    /// </summary>
    private static bool ResolveContractDependencies(DataContractContext context, HashSet<DataContract> visited, List<(ISymbol Symbol, Exception Exception)> errors)
    {
        var anyFound = false;
        for (var i = 0; i < context.DataContracts.Length; i++)
        {
            var item = context.DataContracts[i];
            if (item == null)
                continue;

            if (ResolveContractDependenciesCore(context, item, visited, errors))
                anyFound = true;
        }

        return anyFound;
    }

    private static bool ResolveContractDependenciesCore(DataContractContext context, DataContract contract, HashSet<DataContract> visited, List<(ISymbol Symbol, Exception Exception)> errors)
    {
        if (!visited.Add(contract))
            return false;

        if (contract is ClassDataContract classDataContract)
        {
            // Ensure member contracts are resolved
            foreach (var item in classDataContract.Members)
            {
                try
                {
                    // Force resolution of MemberTypeContract
                    var memberContract = item.MemberTypeContract;
                    ResolveContractDependenciesCore(context, memberContract, visited, errors);
                }
                catch (Exception ex) when (!ExceptionUtility.IsFatal(ex))
                {
                    errors.Add((item.MemberInfo, ex));
                }
            }
        }
        else if (contract is CollectionDataContract collectionDataContract)
        {
            try
            {
                // Force resolution of ItemContract - this creates KeyValue contracts for dictionaries
                var itemContract = collectionDataContract.ItemContract;
                ResolveContractDependenciesCore(context, itemContract, visited, errors);
            }
            catch (Exception ex) when (!ExceptionUtility.IsFatal(ex))
            {
                errors.Add((collectionDataContract.UnderlyingType, ex));
            }

            try
            {
                if (contract.UnderlyingType.TypeKind == TypeKind.Interface)
                    AddCommonCollectionImplementations(context, collectionDataContract);
            }
            catch (Exception ex) when (!ExceptionUtility.IsFatal(ex))
            {
                errors.Add((collectionDataContract.UnderlyingType, ex));
            }
        }

        foreach (var item in contract.KnownDataContracts)
            ResolveContractDependenciesCore(context, item, visited, errors);

        return true;
    }

    /// <summary>
    /// Adds common collection implementations when a collection's underlying type is an interface.
    /// For generic collections (IList&lt;T&gt;, ICollection&lt;T&gt;, IEnumerable&lt;T&gt;), adds List&lt;T&gt; and T[].
    /// For generic dictionaries (IDictionary&lt;K, V&gt;), adds Dictionary&lt;K, V&gt;.
    /// For non-generic collections (IList, ICollection, IEnumerable), adds ArrayList and object[].
    /// For non-generic dictionaries (IDictionary), adds Hashtable.
    /// </summary>
    private static void AddCommonCollectionImplementations(DataContractContext dataContractContext, CollectionDataContract collectionDataContract)
    {
        var knownSymbols = dataContractContext.KnownSymbols;

        var underlyingType = collectionDataContract.UnderlyingType;
        switch (collectionDataContract.Kind)
        {
            case CollectionKind.GenericDictionary:
            {
                // For IDictionary<K, V>, add Dictionary<K, V>
                if (underlyingType is INamedTypeSymbol namedType && knownSymbols.DictionaryOfTKeyTValueType != null)
                {
                    var typeArgs = namedType.TypeArguments;
                    if (typeArgs.Length == 2)
                    {
                        var dictionaryType = knownSymbols.DictionaryOfTKeyTValueType.Construct(typeArgs[0], typeArgs[1]);
                        dataContractContext.GetDataContract(dictionaryType);
                    }
                }

                break;
            }
            case CollectionKind.Dictionary:
            {
                // For IDictionary, add Hashtable
                if (knownSymbols.HashtableType != null)
                    dataContractContext.GetDataContract(knownSymbols.HashtableType);
                break;
            }
            case CollectionKind.GenericList:
            case CollectionKind.GenericCollection:
            case CollectionKind.GenericEnumerable:
            {
                // For IList<T>, ICollection<T>, IEnumerable<T>, add List<T> and T[]
                if (underlyingType is INamedTypeSymbol namedType)
                {
                    var typeArgs = namedType.TypeArguments;
                    if (typeArgs.Length == 1)
                    {
                        var elementType = typeArgs[0];

                        // Add List<T>
                        if (knownSymbols.ListOfTType != null)
                        {
                            var listType = knownSymbols.ListOfTType.Construct(elementType);
                            dataContractContext.GetDataContract(listType);
                        }

                        // Add T[]
                        var arrayType = knownSymbols.Compilation.CreateArrayTypeSymbol(elementType);
                        dataContractContext.GetDataContract(arrayType);
                    }
                }

                break;
            }
            case CollectionKind.List:
            case CollectionKind.Collection:
            case CollectionKind.Enumerable:
            {
                // For IList, ICollection, IEnumerable, add ArrayList and object[]
                if (knownSymbols.ArrayListType != null)
                    dataContractContext.GetDataContract(knownSymbols.ArrayListType);

                // Add object[]
                dataContractContext.GetDataContract(knownSymbols.ObjectArrayType);
                break;
            }
        }
    }

    private static void GenerateSource(SourceProductionContext context, DataContractSetSpec contractSet)
    {
        // Report any diagnostics collected during parsing
        foreach (var diagnostic in contractSet.Diagnostics)
            context.ReportDiagnostic(diagnostic.CreateDiagnostic());

        var writer = new CodeWriter();
        var generators = new List<SpecContractGenerator>();
        foreach (var contract in contractSet.Contracts)
        {
            switch (contract)
            {
                case ClassDataContractSpec classContract:
                {
                    var generator = new ClassDataContractSpecGenerator(writer, contractSet, classContract);
                    generators.Add(generator);
                    break;
                }
                case CollectionDataContractSpec collectionContract:
                {
                    var generator = new CollectionDataContractSpecGenerator(writer, contractSet, collectionContract);
                    generators.Add(generator);
                    break;
                }
                case EnumDataContractSpec enumContract:
                {
                    var generator = new EnumDataContractSpecGenerator(writer, contractSet, enumContract);
                    generators.Add(generator);
                    break;
                }
                case PrimitiveDataContractSpec primitiveContract:
                {
                    var generator = new PrimitiveDataContractSpecGenerator(writer, contractSet, primitiveContract);
                    generators.Add(generator);
                    break;
                }
                case XmlDataContractSpec xmlContract:
                {
                    var generator = new XmlDataContractSpecGenerator(writer, contractSet, xmlContract);
                    generators.Add(generator);
                    break;
                }
            }
        }

        writer.AppendLine("// <auto-generated />");
        writer.AppendLine("namespace Marius.DataContracts.Runtime;");
        writer.AppendLine();

        writer.AppendLine($"public partial class DataContractContext");
        using (writer.Block())
        {
            foreach (var item in generators)
                item.DeclareDataContract();

            writer.AppendLine();
            writer.AppendLine("public static readonly global::Marius.DataContracts.Runtime.DataContract[] DataContracts;");
            writer.AppendLine("public static readonly global::System.Collections.Frozen.FrozenDictionary<global::System.Type, global::Marius.DataContracts.Runtime.DataContract> TypeDataContracts;");

            writer.AppendLine();
            writer.AppendLine("static DataContractContext()");
            using (writer.Block())
            {
                writer.AppendLine($"DataContracts = new global::Marius.DataContracts.Runtime.DataContract[{SymbolDisplay.FormatPrimitive(contractSet.Contracts.Length, false, false)}];");
                writer.AppendLine();

                var xmlDictionary = writer.LocalName("__xmlDictionary");
                writer.AppendLine($"var {xmlDictionary} = new global::System.Xml.XmlDictionary();");

                var contractLocals = new List<(string local, string? underlyingTypeOverride)>();
                foreach (var item in generators)
                {
                    var local = item.GenerateDataContract(xmlDictionary);
                    if (!string.IsNullOrEmpty(local.localName))
                        contractLocals.Add(local);
                }

                writer.AppendLine();

                var dictionary = writer.LocalName("__dictionary");
                writer.AppendLine($"var {dictionary} = new global::System.Collections.Generic.Dictionary<global::System.Type, global::Marius.DataContracts.Runtime.DataContract>();");
                foreach (var (local, underlyingTypeOverride) in contractLocals)
                {
                    if (string.IsNullOrEmpty(underlyingTypeOverride))
                        writer.AppendLine($"DataContracts[{local}.Id] = {dictionary}[{local}.UnderlyingType] = {local};");
                    else
                        writer.AppendLine($"DataContracts[{local}.Id] = {dictionary}[{local}.{underlyingTypeOverride}] = {local};");
                }

                writer.AppendLine();
                writer.AppendLine($"TypeDataContracts = global::System.Collections.Frozen.FrozenDictionary.ToFrozenDictionary({dictionary});");

                writer.AppendLine();
                foreach (var item in generators)
                    item.GenerateDependencies(xmlDictionary);
            }

            writer.AppendLine();
            writer.AppendLine("[global::System.Diagnostics.CodeAnalysis.DoesNotReturn]");
            writer.AppendLine("[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]");
            writer.AppendLine("private static void ThrowValidationException(string msg) => throw new global::System.Runtime.Serialization.SerializationException(msg);");

            writer.AppendLine();
            writer.AppendLine("[global::System.Diagnostics.CodeAnalysis.DoesNotReturn]");
            writer.AppendLine("[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]");
            writer.AppendLine("private static void ThrowUnexpectedStateException(global::System.Xml.XmlNodeType expectedState, global::Marius.DataContracts.Runtime.XmlReaderDelegator xmlReader) => throw global::Marius.DataContracts.Runtime.XmlObjectSerializerReadContext.CreateUnexpectedStateException(expectedState, xmlReader);");

            PrivateAccessorSpecGenerator.GeneratePrivateAccessors(writer, contractSet);
        }

        context.AddSource($"DataContractContext.g.cs", writer.GetString());
    }
}