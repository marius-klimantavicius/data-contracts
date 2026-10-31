using System.Diagnostics;
using System.Runtime.Serialization;
using System.Xml.Linq;
using Marius.DataContracts.Playground.Cases;
using Marius.DataContracts.Runtime;
using static Marius.DataContracts.Playground.ShowcaseChecks;

namespace Marius.DataContracts.Playground;

internal static class FixedCasesShowcase
{
    public static async Task<int> RunAsync(string[] args)
    {
        string[] options = ["--xml", "--original", "--verification-tests", "--help"];
        if (args.Contains("--help"))
        {
            Console.WriteLine("DataContract fixed-case showcase\n" +
                "  --xml                 Print both serializers' XML for successful examples.\n" +
                "  --original            Also run the original mixed-contract demo.\n" +
                "  --verification-tests  Also run the actual compiler-caching and equivalence-oracle regressions.\n" +
                "  --help                Show these options.");
            return 0;
        }

        if (args.Any(arg => !options.Contains(arg)))
        {
            await Console.Error.WriteLineAsync("Unknown option. Use --help for usage.");
            return 2;
        }

        Console.WriteLine("DataContract fixed-case showcase (.NET versus source-generated serializer)");
        Console.WriteLine("Successful examples compare XML and check both readers against both writers.");
        var checks = new ShowcaseChecks(args.Contains("--xml"));
        ShowMemberBinding(checks);
        ShowKnownTypes(checks);
        ShowDefaults(checks);
        ShowGeneratedCode(checks);
        ShowQuotas(checks);
        ShowGraphAndOracle(checks);
        FeatureShowcase.Run(checks);
        TypedReadShowcase.Run(checks);
        checks.Run("Unsupported resolver settings", "An unsupported resolver is rejected explicitly instead of silently ignored.", () =>
        {
            var exception = Throws<NotSupportedException>(() => _ = new Runtime.DataContractSerializer(DataContractContext.DataContractProvider, typeof(OracleState), new DataContractSerializerSettings { DataContractResolver = new UnsupportedResolver() }));
            Check(exception.Message.Contains("DataContractResolver", StringComparison.Ordinal), "Exception must identify the unsupported setting.");
        });

        var extraFailures = 0;
        if (args.Contains("--original"))
        {
            Console.WriteLine("\nOriginal mixed-contract demo");
            try
            {
                await Program.RunOriginalDemoAsync();
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine(exception);
                extraFailures++;
            }
        }

        if (args.Contains("--verification-tests"))
        {
            try
            {
                if (!await RunVerificationTestsAsync())
                    extraFailures++;
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine(exception);
                extraFailures++;
            }
        }
        else
        {
            Console.WriteLine(
                """
                Compiler caching (TypeSpec equality) is a build-time fix. Use --verification-tests
                to demonstrate cached output after unrelated edits, regeneration after contract edits,
                and the actual equivalence oracle's negative controls.
                """);
        }

        Console.WriteLine($"\nShowcase: {checks.Passed} passed, {checks.Failed} failed; additional checks: {extraFailures} failed.");
        return checks.Failed + extraFailures == 0 ? 0 : 1;
    }

    private static void ShowMemberBinding(ShowcaseChecks checks) =>
        checks.Run("1. Hidden inherited members", "Base property/field = 11/33; derived property/field = 22/44 must survive independently.", () =>
        {
            var value = new HiddenDerived { Value = 22, Field = 44 };
            ((HiddenBase)value).Value = 11;
            ((HiddenBase)value).Field = 33;
            checks.Parity(value, result => Check(((HiddenBase)result).Value == 11 && ((HiddenBase)result).Field == 33 && result.Value == 22 && result.Field == 44, "A hidden base member was overwritten or lost."));
        });

    private static void ShowKnownTypes(ShowcaseChecks checks)
    {
        checks.Run("2a. Generated availability versus known types", "Unlisted Dog is rejected on write/read; explicitly registering it permits interoperability.", () =>
        {
            var value = new AnimalEnvelope { Value = new Dog { Name = "Rex", Age = 4 } };
            Check(DataContractContext.DataContractProvider.TryGetDataContract(typeof(Dog), out _), "Dog must have generated code.");
            Check(DataContractContext.DataContractProvider.GetPrimitiveDataContract(typeof(Dog)) is null, "Dog must not be treated as primitive.");
            Check(DataContractContext.DataContractProvider.GetPrimitiveDataContract(typeof(int)) is not null, "Built-in primitive must remain available.");

            var settings = new DataContractSerializerSettings { KnownTypes = [typeof(Dog)] };
            var xml = Write(false, value, settings);
            foreach (var generated in new[] { false, true })
            {
                Throws<SerializationException>(() => Write(generated, value));
                Throws<SerializationException>(() => Read<AnimalEnvelope>(generated, xml));
            }

            checks.Parity(value, result => Check(result.Value is Dog { Name: "Rex", Age: 4 }, "Known Dog state differs."), settings);
        });

        checks.Run("2b. Scoped QName reuse", "Two sibling known-type scopes use the same XML type name for different CLR contracts.", () =>
        {
            var value = new SeparateScopes
            {
                First = new NumberEnvelope { Value = new ScopedNumber { Value = 17 } },
                Second = new TextEnvelope { Value = new ScopedText { Value = "separate scope" } },
            };
            checks.Parity(value, result => Check(result.First.Value is ScopedNumber { Value: 17 } && result.Second.Value is ScopedText { Value: "separate scope" }, "An unrelated generated QName shadowed the scoped contract."));
        });
    }

    private static void ShowDefaults(ShowcaseChecks checks)
    {
        checks.Run("3a. EmitDefaultValue=false", "Struct without != compiles; misleading reference inequality cannot omit a non-null value.", () =>
        {
            checks.Parity(new Defaults { Point = new Point { X = 3 }, Reference = new UnusualEquality { Value = 5 }, Optional = 0 }, result => Check(result.Point.X == 3 && result.Reference is { Value: 5 } && result.Optional == 0, "Populated member was omitted."));

            var xml = checks.Parity(new Defaults(), result => Check(result.Point.X == 0 && result.Reference is null && result.Optional is null, "Default state differs."));
            Check(!XElement.Parse(xml).Elements().Any(), "Default-valued members should be omitted.");
        });

        checks.Run("3b. Required default failure", "IsRequired=true combined with EmitDefaultValue=false rejects a default value in both writers.", () =>
        {
            foreach (var generated in new[] { false, true })
                Throws<SerializationException>(() => Write(generated, new RequiredDefault()));

            checks.Parity(new RequiredDefault { Value = 7 }, result => Check(result.Value == 7, "Required value lost."));
        });
    }

    private static void ShowGeneratedCode(ShowcaseChecks checks)
    {
        checks.Run("4. Generated-name collisions", "Two Model types, generic arguments, and Model[] versus ModelArray all coexist.", () =>
        {
            checks.Parity(new CollisionEnvelope
                {
                    First = new Cases.First.Model
                    {
                        Value = 1,
                    },
                    Second = new Cases.Second.Model
                    {
                        Value = "two",
                    },
                    Generic = new Box<Cases.First.Model>
                    {
                        Value = new Cases.First.Model
                        {
                            Value = 3,
                        },
                    },
                    Array =
                    [
                        new Cases.First.Model
                        {
                            Value = 4,
                        },
                    ],
                    NamedLikeArray = new ModelArray
                    {
                        Value = 5,
                    },
                },
                result => Check(result.First.Value == 1 && result.Second.Value == "two" && result.Generic.Value.Value == 3 && result.Array.Length == 1 && result.Array[0].Value == 4 && result.NamedLikeArray.Value == 5, "Collision contract state differs."));
        });

        checks.Run("5. Internal DTO and private state", "Internal type, private readonly field, public field, and init-only property compile and roundtrip.", () =>
        {
            checks.Parity(new InternalState(9) { PublicField = 12, Name = "internal" }, result => Check(result.Secret == 9 && result.PublicField == 12 && result.Name == "internal", "Private/field/init state differs."));
        });

        checks.Run("6. Escaped C# identifiers", "@namespace, @class, @event and @return remain unescaped in their XML names.", () =>
        {
            var xml = checks.Parity(new Cases.@namespace.@class { @event = 5, @return = "return value" }, result => Check(result.@event == 5 && result.@return == "return value", "Escaped member state differs."));
            var root = XElement.Parse(xml);
            Check(root.Name.LocalName == "class" && root.Name.NamespaceName.EndsWith(".namespace", StringComparison.Ordinal) &&
                root.Elements().Any(e => e.Name.LocalName == "event") && root.Elements().Any(e => e.Name.LocalName == "return"),
                "C# escaping leaked into the XML contract.");
        });
    }

    private static void ShowQuotas(ShowcaseChecks checks)
    {
        Quota(checks, "List<int>", new ListQuota { Values = [1, 2, 3] }, 5, r => Check(r.Values.SequenceEqual([1, 2, 3]), "List state differs."));
        Quota(checks, "int[]", new ArrayQuota { Values = [1, 2, 3] }, 5, r => Check(r.Values.SequenceEqual([1, 2, 3]), "Array state differs."));
        Quota(checks, "count-free IEnumerable<int>", new EnumerableQuota { Values = new IntSequence { 1, 2, 3 } }, 5, r => Check(r.Values.SequenceEqual([1, 2, 3]), "Enumerable state differs."));
        Quota(checks, "Dictionary<string,int>", new DictionaryQuota { Values = { ["one"] = 1, ["two"] = 2 } }, 8, r => Check(r.Values.Count == 2 && r.Values["one"] == 1 && r.Values["two"] == 2, "Dictionary state differs."));
    }

    private static void Quota<T>(ShowcaseChecks checks, string shape, T value, int minimum, Action<T> state) =>
        checks.Run($"7. Quota: {shape}", $"Read/write fail at {minimum - 1} items and succeed at {minimum} and {minimum + 1}, with references on and off.", () =>
        {
            foreach (var preserve in new[] { false, true })
            {
                var xml = Write(false, value, new DataContractSerializerSettings { PreserveObjectReferences = preserve });
                var tooSmall = new DataContractSerializerSettings { MaxItemsInObjectGraph = minimum - 1, PreserveObjectReferences = preserve };
                foreach (var generated in new[] { false, true })
                {
                    Throws<SerializationException>(() => Write(generated, value, tooSmall));
                    Throws<SerializationException>(() => Read<T>(generated, xml, tooSmall));
                }

                foreach (var quota in new[] { minimum, minimum + 1 })
                    checks.Parity(value, state, new DataContractSerializerSettings { MaxItemsInObjectGraph = quota, PreserveObjectReferences = preserve });
            }
        });

    private static void ShowGraphAndOracle(ShowcaseChecks checks)
    {
        checks.Run("8a. Contract-state oracle", "Significant whitespace and UTC kind survive; non-contract state follows the framework roundtrip.", () =>
        {
            var value = new OracleState { Text = "  keep these spaces  ", Timestamp = new DateTime(638000000000000000, DateTimeKind.Utc), Ignored = 99 };
            var baseline = Read<OracleState>(false, Write(false, value));
            Check(baseline.Ignored == 0, "Non-contract state should be absent after framework deserialization.");
            checks.Parity(value, result => Check(result.Text == baseline.Text && result.Timestamp.Ticks == baseline.Timestamp.Ticks && result.Timestamp.Kind == baseline.Timestamp.Kind && result.Ignored == baseline.Ignored, "Contract state differs from the framework roundtrip."));
        });

        checks.Run("8b. XML comparison controls", "Spaces/mixed text and namespace bindings matter; xsi:type prefix spelling does not.", () =>
        {
            Check(!XmlEquivalence.AreEqual("<r> value </r>", "<r>value</r>"), "Whitespace difference was hidden.");
            Check(!XmlEquivalence.AreEqual("<r>before<c/>after</r>", "<r>changed<c/>after</r>"), "Mixed text difference was hidden.");

            const string left = "<r xmlns:i='http://www.w3.org/2001/XMLSchema-instance' xmlns:a='urn:x' i:type='a:Model'/>";
            const string equivalent = "<r xmlns:i='http://www.w3.org/2001/XMLSchema-instance' xmlns:b='urn:x' i:type='b:Model'/>";

            Check(XmlEquivalence.AreEqual(left, equivalent), "Equivalent xsi:type prefixes must compare equal.");
            Check(!XmlEquivalence.AreEqual(left, left.Replace("urn:x", "urn:y", StringComparison.Ordinal)), "Different QName bindings must differ.");
            Check(XmlEquivalence.AreEqual("<r>a<![CDATA[b]]>c</r>", "<r>abc</r>"), "CDATA and text have equivalent character content.");
        });

        checks.Run("Dictionary reference IDs, aliases and cycles", "Dictionary entries share one node with the root member; that node points to itself.", () =>
        {
            var shared = new GraphNode { Value = 42 };
            shared.Next = shared;
            checks.Parity(new DictionaryGraph { Shared = shared, Items = { ["first"] = shared, ["second"] = shared } }, result =>
            {
                Check(result.Items.Count == 2 && result.Shared.Value == 42 && ReferenceEquals(result.Shared, result.Shared.Next) && ReferenceEquals(result.Shared, result.Items["first"]) && ReferenceEquals(result.Shared, result.Items["second"]), "Dictionary values, aliases or self-cycle were lost.");
            }, new DataContractSerializerSettings { PreserveObjectReferences = true });
        });
    }

    private static async Task<bool> RunVerificationTestsAsync()
    {
        Console.WriteLine("\n9. Actual TypeSpec/caching regressions and equivalence-oracle negative controls");
        const string relativeProject = "Marius.DataContracts.SourceGenerators/Marius.DataContracts.SourceGenerators.Tests/Marius.DataContracts.SourceGenerators.Tests.csproj";

        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, relativeProject)))
            directory = directory.Parent;

        if (directory is null)
            throw new InvalidOperationException("--verification-tests requires running from a source checkout containing the tests project.");

        var start = new ProcessStartInfo("dotnet") { UseShellExecute = false, WorkingDirectory = directory.FullName };
        foreach (var argument in (string[])
                 [
                     "test", Path.Combine(directory.FullName, relativeProject), "--framework", "net9.0", "--filter",
                     "FullyQualifiedName~ReviewTypeSpecEqualityTests|FullyQualifiedName~EquivalenceOracleRegressionTests", "--logger", "console;verbosity=normal",
                 ])
            start.ArgumentList.Add(argument);

        using var process = Process.Start(start) ?? throw new InvalidOperationException("Could not start the focused verification tests.");
        await process.WaitForExitAsync();
        return process.ExitCode == 0;
    }
}