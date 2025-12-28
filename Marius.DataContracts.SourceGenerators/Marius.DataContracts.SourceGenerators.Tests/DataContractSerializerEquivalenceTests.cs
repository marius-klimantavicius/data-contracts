using System.Threading.Tasks;
using Xunit;

namespace Marius.DataContracts.SourceGenerators.Tests;

/// <summary>
/// Tests that verify the equivalence of XML serialization and deserialization
/// between System.Runtime.Serialization.DataContractSerializer and the 
/// source-generated Marius.DataContracts.Runtime.DataContractSerializer.
/// </summary>
public class DataContractSerializerEquivalenceTests
{
    /// <summary>
    /// Tests serialization and deserialization of a simple data contract with primitive properties.
    /// </summary>
    [Fact]
    public async Task SimplePrimitiveContract_SerializesEquivalently()
    {
        var dataContractCode = """
            using System;
            using System.Runtime.Serialization;

            namespace TestContracts;

            [DataContract(Name = "Person", Namespace = "http://test.contracts")]
            public class PersonContract
            {
                [DataMember(Order = 1)]
                public string Name { get; set; } = "";

                [DataMember(Order = 2)]
                public int Age { get; set; }

                [DataMember(Order = 3)]
                public bool IsActive { get; set; }

                [DataMember(Order = 4)]
                public decimal Salary { get; set; }
            }
            """;

        var testCode = """
            var original = new TestContracts.PersonContract
            {
                Name = "John Doe",
                Age = 42,
                IsActive = true,
                Salary = 75000.50m
            };

            return SerializationTestRunner.RunTest(original, typeof(TestContracts.PersonContract));
            """;

        var result = await RunSerializationTest(dataContractCode, testCode);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.True(result.XmlEquivalent, "Serialized XMLs should be semantically equivalent");
        Assert.True(result.DeserializedEqual, "Deserialized objects should be equal to originals");
    }

    /// <summary>
    /// Tests serialization and deserialization of a contract with nullable properties.
    /// </summary>
    [Fact]
    public async Task NullablePropertiesContract_SerializesEquivalently()
    {
        var dataContractCode = """
            using System;
            using System.Runtime.Serialization;

            namespace TestContracts;

            [DataContract(Name = "NullableTest", Namespace = "http://test.contracts")]
            public class NullableTestContract
            {
                [DataMember(Order = 1)]
                public string? NullableString { get; set; }

                [DataMember(Order = 2)]
                public int? NullableInt { get; set; }

                [DataMember(Order = 3)]
                public DateTime? NullableDateTime { get; set; }
            }
            """;

        var testCode = """
            var original = new TestContracts.NullableTestContract
            {
                NullableString = null,
                NullableInt = 42,
                NullableDateTime = null
            };

            return SerializationTestRunner.RunTest(original, typeof(TestContracts.NullableTestContract));
            """;

        var result = await RunSerializationTest(dataContractCode, testCode);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.True(result.XmlEquivalent, "Serialized XMLs should be semantically equivalent");
        Assert.True(result.DeserializedEqual, "Deserialized objects should be equal to originals");
    }

    /// <summary>
    /// Tests serialization and deserialization of a contract with collection properties.
    /// </summary>
    [Fact]
    public async Task CollectionContract_SerializesEquivalently()
    {
        var dataContractCode = """
            using System;
            using System.Collections.Generic;
            using System.Runtime.Serialization;

            namespace TestContracts;

            [DataContract(Name = "CollectionTest", Namespace = "http://test.contracts")]
            public class CollectionTestContract
            {
                [DataMember(Order = 1)]
                public List<string> Names { get; set; } = new();

                [DataMember(Order = 2)]
                public int[] Numbers { get; set; } = Array.Empty<int>();
            }
            """;

        var testCode = """
            var original = new TestContracts.CollectionTestContract
            {
                Names = new List<string> { "Alice", "Bob", "Charlie" },
                Numbers = new int[] { 1, 2, 3, 4, 5 }
            };

            return SerializationTestRunner.RunTest(original, typeof(TestContracts.CollectionTestContract));
            """;

        var result = await RunSerializationTest(dataContractCode, testCode);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.True(result.XmlEquivalent, "Serialized XMLs should be semantically equivalent");
        Assert.True(result.DeserializedEqual, "Deserialized objects should be equal to originals");
    }

    /// <summary>
    /// Tests serialization and deserialization of nested data contracts.
    /// </summary>
    [Fact]
    public async Task NestedContract_SerializesEquivalently()
    {
        var dataContractCode = """
            using System;
            using System.Runtime.Serialization;

            namespace TestContracts;

            [DataContract(Name = "Address", Namespace = "http://test.contracts")]
            public class AddressContract
            {
                [DataMember(Order = 1)]
                public string Street { get; set; } = "";

                [DataMember(Order = 2)]
                public string City { get; set; } = "";

                [DataMember(Order = 3)]
                public string ZipCode { get; set; } = "";
            }

            [DataContract(Name = "Customer", Namespace = "http://test.contracts")]
            public class CustomerContract
            {
                [DataMember(Order = 1)]
                public string Name { get; set; } = "";

                [DataMember(Order = 2)]
                public AddressContract? Address { get; set; }
            }
            """;

        var testCode = """
            var original = new TestContracts.CustomerContract
            {
                Name = "Test Customer",
                Address = new TestContracts.AddressContract
                {
                    Street = "123 Main St",
                    City = "Springfield",
                    ZipCode = "12345"
                }
            };

            return SerializationTestRunner.RunTest(original, typeof(TestContracts.CustomerContract));
            """;

        var result = await RunSerializationTest(dataContractCode, testCode);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.True(result.XmlEquivalent, "Serialized XMLs should be semantically equivalent");
        Assert.True(result.DeserializedEqual, "Deserialized objects should be equal to originals");
    }

    /// <summary>
    /// Tests serialization and deserialization of a contract with dictionary properties.
    /// </summary>
    [Fact]
    public async Task DictionaryContract_SerializesEquivalently()
    {
        var dataContractCode = """
            using System;
            using System.Collections.Generic;
            using System.Runtime.Serialization;

            namespace TestContracts;

            [DataContract(Name = "DictionaryTest", Namespace = "http://test.contracts")]
            public class DictionaryTestContract
            {
                [DataMember(Order = 1)]
                public Dictionary<string, int> Scores { get; set; } = new();
            }
            """;

        var testCode = """
            var original = new TestContracts.DictionaryTestContract
            {
                Scores = new Dictionary<string, int>
                {
                    { "Alice", 100 },
                    { "Bob", 85 },
                    { "Charlie", 92 }
                }
            };

            return SerializationTestRunner.RunTest(original, typeof(TestContracts.DictionaryTestContract));
            """;

        var result = await RunSerializationTest(dataContractCode, testCode);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.True(result.XmlEquivalent, "Serialized XMLs should be semantically equivalent");
        Assert.True(result.DeserializedEqual, "Deserialized objects should be equal to originals");
    }

    /// <summary>
    /// Tests serialization and deserialization of an enum property.
    /// </summary>
    [Fact]
    public async Task EnumContract_SerializesEquivalently()
    {
        var dataContractCode = """
            using System;
            using System.Runtime.Serialization;

            namespace TestContracts;

            [DataContract(Name = "Status", Namespace = "http://test.contracts")]
            public enum StatusEnum
            {
                [EnumMember]
                Unknown = 0,
                [EnumMember]
                Active = 1,
                [EnumMember]
                Inactive = 2,
                [EnumMember]
                Pending = 3
            }

            [DataContract(Name = "Order", Namespace = "http://test.contracts")]
            public class OrderContract
            {
                [DataMember(Order = 1)]
                public string OrderId { get; set; } = "";

                [DataMember(Order = 2)]
                public StatusEnum Status { get; set; }
            }
            """;

        var testCode = """
            var original = new TestContracts.OrderContract
            {
                OrderId = "ORD-12345",
                Status = TestContracts.StatusEnum.Active
            };

            return SerializationTestRunner.RunTest(original, typeof(TestContracts.OrderContract));
            """;

        var result = await RunSerializationTest(dataContractCode, testCode);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.True(result.XmlEquivalent, "Serialized XMLs should be semantically equivalent");
        Assert.True(result.DeserializedEqual, "Deserialized objects should be equal to originals");
    }

    /// <summary>
    /// Tests serialization and deserialization of a contract with byte array (binary data).
    /// </summary>
    [Fact]
    public async Task ByteArrayContract_SerializesEquivalently()
    {
        var dataContractCode = """
            using System;
            using System.Runtime.Serialization;

            namespace TestContracts;

            [DataContract(Name = "BinaryData", Namespace = "http://test.contracts")]
            public class BinaryDataContract
            {
                [DataMember(Order = 1)]
                public string Name { get; set; } = "";

                [DataMember(Order = 2)]
                public byte[]? Data { get; set; }
            }
            """;

        var testCode = """
            var original = new TestContracts.BinaryDataContract
            {
                Name = "TestFile",
                Data = new byte[] { 0x48, 0x65, 0x6C, 0x6C, 0x6F } // "Hello" in ASCII
            };

            return SerializationTestRunner.RunTest(original, typeof(TestContracts.BinaryDataContract));
            """;

        var result = await RunSerializationTest(dataContractCode, testCode);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.True(result.XmlEquivalent, "Serialized XMLs should be semantically equivalent");
        Assert.True(result.DeserializedEqual, "Deserialized objects should be equal to originals");
    }

    /// <summary>
    /// Tests serialization and deserialization of a contract with DateTime and Guid properties.
    /// </summary>
    [Fact]
    public async Task DateTimeAndGuidContract_SerializesEquivalently()
    {
        var dataContractCode = """
            using System;
            using System.Runtime.Serialization;

            namespace TestContracts;

            [DataContract(Name = "Event", Namespace = "http://test.contracts")]
            public class EventContract
            {
                [DataMember(Order = 1)]
                public Guid EventId { get; set; }

                [DataMember(Order = 2)]
                public DateTime Timestamp { get; set; }

                [DataMember(Order = 3)]
                public string Description { get; set; } = "";
            }
            """;

        var testCode = """
            var original = new TestContracts.EventContract
            {
                EventId = new Guid("12345678-1234-1234-1234-123456789012"),
                Timestamp = new DateTime(2024, 6, 15, 10, 30, 0, DateTimeKind.Utc),
                Description = "Test Event"
            };

            return SerializationTestRunner.RunTest(original, typeof(TestContracts.EventContract));
            """;

        var result = await RunSerializationTest(dataContractCode, testCode);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.True(result.XmlEquivalent, "Serialized XMLs should be semantically equivalent");
        Assert.True(result.DeserializedEqual, "Deserialized objects should be equal to originals");
    }

    private async Task<SerializationTestResult> RunSerializationTest(string dataContractCode, string testCode)
    {
        var testRunner = new SerializationTestCompiler();
        return await testRunner.CompileAndRunAsync(dataContractCode, testCode);
    }

    /// <summary>
    /// Tests serialization with custom DataMember names.
    /// </summary>
    [Fact]
    public async Task CustomDataMemberNames_SerializesEquivalently()
    {
        var dataContractCode = """
            using System;
            using System.Runtime.Serialization;

            namespace TestContracts;

            [DataContract(Name = "Product", Namespace = "http://test.contracts")]
            public class ProductContract
            {
                [DataMember(Name = "product_id", Order = 1)]
                public int Id { get; set; }

                [DataMember(Name = "product_name", Order = 2)]
                public string Name { get; set; } = "";

                [DataMember(Name = "unit_price", Order = 3)]
                public decimal Price { get; set; }
            }
            """;

        var testCode = """
            var original = new TestContracts.ProductContract
            {
                Id = 12345,
                Name = "Widget",
                Price = 19.99m
            };

            return SerializationTestRunner.RunTest(original, typeof(TestContracts.ProductContract));
            """;

        var result = await RunSerializationTest(dataContractCode, testCode);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.True(result.XmlEquivalent, "Serialized XMLs should be semantically equivalent");
        Assert.True(result.DeserializedEqual, "Deserialized objects should be equal to originals");
    }

    /// <summary>
    /// Tests serialization with EmitDefaultValue = false.
    /// </summary>
    [Fact]
    public async Task EmitDefaultValueFalse_SerializesEquivalently()
    {
        var dataContractCode = """
            using System;
            using System.Runtime.Serialization;

            namespace TestContracts;

            [DataContract(Name = "Config", Namespace = "http://test.contracts")]
            public class ConfigContract
            {
                [DataMember(Order = 1, EmitDefaultValue = false)]
                public string? OptionalName { get; set; }

                [DataMember(Order = 2, EmitDefaultValue = false)]
                public int OptionalCount { get; set; }

                [DataMember(Order = 3)]
                public string RequiredField { get; set; } = "";
            }
            """;

        var testCode = """
            var original = new TestContracts.ConfigContract
            {
                OptionalName = null,
                OptionalCount = 0,
                RequiredField = "Test"
            };

            return SerializationTestRunner.RunTest(original, typeof(TestContracts.ConfigContract));
            """;

        var result = await RunSerializationTest(dataContractCode, testCode);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.True(result.XmlEquivalent, "Serialized XMLs should be semantically equivalent");
        Assert.True(result.DeserializedEqual, "Deserialized objects should be equal to originals");
    }

    /// <summary>
    /// Tests serialization of a list of complex objects.
    /// </summary>
    [Fact]
    public async Task ListOfComplexObjects_SerializesEquivalently()
    {
        var dataContractCode = """
            using System;
            using System.Collections.Generic;
            using System.Runtime.Serialization;

            namespace TestContracts;

            [DataContract(Name = "Item", Namespace = "http://test.contracts")]
            public class ItemContract
            {
                [DataMember(Order = 1)]
                public int Id { get; set; }

                [DataMember(Order = 2)]
                public string Name { get; set; } = "";
            }

            [DataContract(Name = "Container", Namespace = "http://test.contracts")]
            public class ContainerContract
            {
                [DataMember(Order = 1)]
                public List<ItemContract> Items { get; set; } = new();
            }
            """;

        var testCode = """
            var original = new TestContracts.ContainerContract
            {
                Items = new List<TestContracts.ItemContract>
                {
                    new TestContracts.ItemContract { Id = 1, Name = "First" },
                    new TestContracts.ItemContract { Id = 2, Name = "Second" },
                    new TestContracts.ItemContract { Id = 3, Name = "Third" }
                }
            };

            return SerializationTestRunner.RunTest(original, typeof(TestContracts.ContainerContract));
            """;

        var result = await RunSerializationTest(dataContractCode, testCode);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.True(result.XmlEquivalent, "Serialized XMLs should be semantically equivalent");
        Assert.True(result.DeserializedEqual, "Deserialized objects should be equal to originals");
    }

    /// <summary>
    /// Tests serialization of TimeSpan property.
    /// </summary>
    [Fact]
    public async Task TimeSpanProperty_SerializesEquivalently()
    {
        var dataContractCode = """
            using System;
            using System.Runtime.Serialization;

            namespace TestContracts;

            [DataContract(Name = "Timer", Namespace = "http://test.contracts")]
            public class TimerContract
            {
                [DataMember(Order = 1)]
                public string Name { get; set; } = "";

                [DataMember(Order = 2)]
                public TimeSpan Duration { get; set; }

                [DataMember(Order = 3)]
                public TimeSpan? OptionalTimeout { get; set; }
            }
            """;

        var testCode = """
            var original = new TestContracts.TimerContract
            {
                Name = "TestTimer",
                Duration = TimeSpan.FromMinutes(30),
                OptionalTimeout = TimeSpan.FromSeconds(45)
            };

            return SerializationTestRunner.RunTest(original, typeof(TestContracts.TimerContract));
            """;

        var result = await RunSerializationTest(dataContractCode, testCode);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.True(result.XmlEquivalent, "Serialized XMLs should be semantically equivalent");
        Assert.True(result.DeserializedEqual, "Deserialized objects should be equal to originals");
    }

    /// <summary>
    /// Tests serialization with multiple numeric types.
    /// </summary>
    [Fact]
    public async Task MultipleNumericTypes_SerializesEquivalently()
    {
        var dataContractCode = """
            using System;
            using System.Runtime.Serialization;

            namespace TestContracts;

            [DataContract(Name = "Numbers", Namespace = "http://test.contracts")]
            public class NumbersContract
            {
                [DataMember(Order = 1)]
                public byte ByteValue { get; set; }

                [DataMember(Order = 2)]
                public sbyte SByteValue { get; set; }

                [DataMember(Order = 3)]
                public short ShortValue { get; set; }

                [DataMember(Order = 4)]
                public ushort UShortValue { get; set; }

                [DataMember(Order = 5)]
                public int IntValue { get; set; }

                [DataMember(Order = 6)]
                public uint UIntValue { get; set; }

                [DataMember(Order = 7)]
                public long LongValue { get; set; }

                [DataMember(Order = 8)]
                public ulong ULongValue { get; set; }

                [DataMember(Order = 9)]
                public float FloatValue { get; set; }

                [DataMember(Order = 10)]
                public double DoubleValue { get; set; }

                [DataMember(Order = 11)]
                public decimal DecimalValue { get; set; }
            }
            """;

        var testCode = """
            var original = new TestContracts.NumbersContract
            {
                ByteValue = 255,
                SByteValue = -128,
                ShortValue = -32000,
                UShortValue = 65000,
                IntValue = -2000000,
                UIntValue = 4000000000,
                LongValue = -9000000000000000000,
                ULongValue = 18000000000000000000,
                FloatValue = 3.14159f,
                DoubleValue = 3.141592653589793,
                DecimalValue = 12345.6789m
            };

            return SerializationTestRunner.RunTest(original, typeof(TestContracts.NumbersContract));
            """;

        var result = await RunSerializationTest(dataContractCode, testCode);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.True(result.XmlEquivalent, "Serialized XMLs should be semantically equivalent");
        Assert.True(result.DeserializedEqual, "Deserialized objects should be equal to originals");
    }

    /// <summary>
    /// Tests serialization of empty collections.
    /// </summary>
    [Fact]
    public async Task EmptyCollections_SerializesEquivalently()
    {
        var dataContractCode = """
            using System;
            using System.Collections.Generic;
            using System.Runtime.Serialization;

            namespace TestContracts;

            [DataContract(Name = "EmptyCollections", Namespace = "http://test.contracts")]
            public class EmptyCollectionsContract
            {
                [DataMember(Order = 1)]
                public List<string> EmptyList { get; set; } = new();

                [DataMember(Order = 2)]
                public int[] EmptyArray { get; set; } = Array.Empty<int>();

                [DataMember(Order = 3)]
                public Dictionary<string, int> EmptyDictionary { get; set; } = new();
            }
            """;

        var testCode = """
            var original = new TestContracts.EmptyCollectionsContract
            {
                EmptyList = new List<string>(),
                EmptyArray = Array.Empty<int>(),
                EmptyDictionary = new Dictionary<string, int>()
            };

            return SerializationTestRunner.RunTest(original, typeof(TestContracts.EmptyCollectionsContract));
            """;

        var result = await RunSerializationTest(dataContractCode, testCode);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.True(result.XmlEquivalent, "Serialized XMLs should be semantically equivalent");
        Assert.True(result.DeserializedEqual, "Deserialized objects should be equal to originals");
    }

    /// <summary>
    /// Tests serialization of deeply nested objects.
    /// </summary>
    [Fact]
    public async Task DeeplyNestedObjects_SerializesEquivalently()
    {
        var dataContractCode = """
            using System;
            using System.Runtime.Serialization;

            namespace TestContracts;

            [DataContract(Name = "Level3", Namespace = "http://test.contracts")]
            public class Level3Contract
            {
                [DataMember(Order = 1)]
                public string Value { get; set; } = "";
            }

            [DataContract(Name = "Level2", Namespace = "http://test.contracts")]
            public class Level2Contract
            {
                [DataMember(Order = 1)]
                public string Name { get; set; } = "";

                [DataMember(Order = 2)]
                public Level3Contract? Inner { get; set; }
            }

            [DataContract(Name = "Level1", Namespace = "http://test.contracts")]
            public class Level1Contract
            {
                [DataMember(Order = 1)]
                public int Id { get; set; }

                [DataMember(Order = 2)]
                public Level2Contract? Child { get; set; }
            }
            """;

        var testCode = """
            var original = new TestContracts.Level1Contract
            {
                Id = 1,
                Child = new TestContracts.Level2Contract
                {
                    Name = "Middle",
                    Inner = new TestContracts.Level3Contract
                    {
                        Value = "DeepValue"
                    }
                }
            };

            return SerializationTestRunner.RunTest(original, typeof(TestContracts.Level1Contract));
            """;

        var result = await RunSerializationTest(dataContractCode, testCode);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.True(result.XmlEquivalent, "Serialized XMLs should be semantically equivalent");
        Assert.True(result.DeserializedEqual, "Deserialized objects should be equal to originals");
    }

    /// <summary>
    /// Tests serialization of enum with custom values.
    /// </summary>
    [Fact]
    public async Task EnumWithCustomValues_SerializesEquivalently()
    {
        var dataContractCode = """
            using System;
            using System.Runtime.Serialization;

            namespace TestContracts;

            [DataContract(Name = "Priority", Namespace = "http://test.contracts")]
            public enum PriorityEnum
            {
                [EnumMember(Value = "low")]
                Low = 10,
                [EnumMember(Value = "medium")]
                Medium = 50,
                [EnumMember(Value = "high")]
                High = 100,
                [EnumMember(Value = "critical")]
                Critical = 200
            }

            [DataContract(Name = "Task", Namespace = "http://test.contracts")]
            public class TaskContract
            {
                [DataMember(Order = 1)]
                public string Title { get; set; } = "";

                [DataMember(Order = 2)]
                public PriorityEnum Priority { get; set; }
            }
            """;

        var testCode = """
            var original = new TestContracts.TaskContract
            {
                Title = "Important Task",
                Priority = TestContracts.PriorityEnum.High
            };

            return SerializationTestRunner.RunTest(original, typeof(TestContracts.TaskContract));
            """;

        var result = await RunSerializationTest(dataContractCode, testCode);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.True(result.XmlEquivalent, "Serialized XMLs should be semantically equivalent");
        Assert.True(result.DeserializedEqual, "Deserialized objects should be equal to originals");
    }

    /// <summary>
    /// Tests serialization with HashSet collection.
    /// </summary>
    [Fact]
    public async Task HashSetCollection_SerializesEquivalently()
    {
        var dataContractCode = """
            using System;
            using System.Collections.Generic;
            using System.Runtime.Serialization;

            namespace TestContracts;

            [DataContract(Name = "Tags", Namespace = "http://test.contracts")]
            public class TagsContract
            {
                [DataMember(Order = 1)]
                public HashSet<string> Tags { get; set; } = new();
            }
            """;

        var testCode = """
            var original = new TestContracts.TagsContract
            {
                Tags = new HashSet<string> { "alpha", "beta", "gamma" }
            };

            return SerializationTestRunner.RunTest(original, typeof(TestContracts.TagsContract));
            """;

        var result = await RunSerializationTest(dataContractCode, testCode);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.True(result.XmlEquivalent, "Serialized XMLs should be semantically equivalent");
        Assert.True(result.DeserializedEqual, "Deserialized objects should be equal to originals");
    }

    /// <summary>
    /// Tests serialization of Uri property.
    /// </summary>
    [Fact]
    public async Task UriProperty_SerializesEquivalently()
    {
        var dataContractCode = """
            using System;
            using System.Runtime.Serialization;

            namespace TestContracts;

            [DataContract(Name = "Link", Namespace = "http://test.contracts")]
            public class LinkContract
            {
                [DataMember(Order = 1)]
                public string Title { get; set; } = "";

                [DataMember(Order = 2)]
                public Uri? Url { get; set; }
            }
            """;

        var testCode = """
            var original = new TestContracts.LinkContract
            {
                Title = "Example Link",
                Url = new Uri("https://example.com/path?query=value#fragment")
            };

            return SerializationTestRunner.RunTest(original, typeof(TestContracts.LinkContract));
            """;

        var result = await RunSerializationTest(dataContractCode, testCode);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.True(result.XmlEquivalent, "Serialized XMLs should be semantically equivalent");
        Assert.True(result.DeserializedEqual, "Deserialized objects should be equal to originals");
    }

    /// <summary>
    /// Tests serialization of nullable value types.
    /// </summary>
    [Fact]
    public async Task NullableValueTypes_SerializesEquivalently()
    {
        var dataContractCode = """
            using System;
            using System.Runtime.Serialization;

            namespace TestContracts;

            [DataContract(Name = "NullableValues", Namespace = "http://test.contracts")]
            public class NullableValuesContract
            {
                [DataMember(Order = 1)]
                public int? NullableInt { get; set; }

                [DataMember(Order = 2)]
                public bool? NullableBool { get; set; }

                [DataMember(Order = 3)]
                public double? NullableDouble { get; set; }

                [DataMember(Order = 4)]
                public Guid? NullableGuid { get; set; }

                [DataMember(Order = 5)]
                public DateTime? NullableDateTime { get; set; }
            }
            """;

        var testCode = """
            var original = new TestContracts.NullableValuesContract
            {
                NullableInt = 42,
                NullableBool = null,
                NullableDouble = 3.14,
                NullableGuid = null,
                NullableDateTime = new DateTime(2024, 1, 1)
            };

            return SerializationTestRunner.RunTest(original, typeof(TestContracts.NullableValuesContract));
            """;

        var result = await RunSerializationTest(dataContractCode, testCode);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.True(result.XmlEquivalent, "Serialized XMLs should be semantically equivalent");
        Assert.True(result.DeserializedEqual, "Deserialized objects should be equal to originals");
    }

    /// <summary>
    /// Tests serialization with array of complex objects.
    /// </summary>
    [Fact]
    public async Task ArrayOfComplexObjects_SerializesEquivalently()
    {
        var dataContractCode = """
            using System;
            using System.Runtime.Serialization;

            namespace TestContracts;

            [DataContract(Name = "Point", Namespace = "http://test.contracts")]
            public class PointContract
            {
                [DataMember(Order = 1)]
                public double X { get; set; }

                [DataMember(Order = 2)]
                public double Y { get; set; }
            }

            [DataContract(Name = "Polygon", Namespace = "http://test.contracts")]
            public class PolygonContract
            {
                [DataMember(Order = 1)]
                public string Name { get; set; } = "";

                [DataMember(Order = 2)]
                public PointContract[] Vertices { get; set; } = Array.Empty<PointContract>();
            }
            """;

        var testCode = """
            var original = new TestContracts.PolygonContract
            {
                Name = "Triangle",
                Vertices = new TestContracts.PointContract[]
                {
                    new TestContracts.PointContract { X = 0, Y = 0 },
                    new TestContracts.PointContract { X = 10, Y = 0 },
                    new TestContracts.PointContract { X = 5, Y = 10 }
                }
            };

            return SerializationTestRunner.RunTest(original, typeof(TestContracts.PolygonContract));
            """;

        var result = await RunSerializationTest(dataContractCode, testCode);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.True(result.XmlEquivalent, "Serialized XMLs should be semantically equivalent");
        Assert.True(result.DeserializedEqual, "Deserialized objects should be equal to originals");
    }

    /// <summary>
    /// Tests serialization with dictionary of complex values.
    /// </summary>
    [Fact]
    public async Task DictionaryWithComplexValues_SerializesEquivalently()
    {
        var dataContractCode = """
            using System;
            using System.Collections.Generic;
            using System.Runtime.Serialization;

            namespace TestContracts;

            [DataContract(Name = "Person", Namespace = "http://test.contracts")]
            public class PersonInfo
            {
                [DataMember(Order = 1)]
                public string Name { get; set; } = "";

                [DataMember(Order = 2)]
                public int Age { get; set; }
            }

            [DataContract(Name = "Directory", Namespace = "http://test.contracts")]
            public class DirectoryContract
            {
                [DataMember(Order = 1)]
                public Dictionary<string, PersonInfo> People { get; set; } = new();
            }
            """;

        var testCode = """
            var original = new TestContracts.DirectoryContract
            {
                People = new Dictionary<string, TestContracts.PersonInfo>
                {
                    { "john", new TestContracts.PersonInfo { Name = "John Doe", Age = 30 } },
                    { "jane", new TestContracts.PersonInfo { Name = "Jane Doe", Age = 28 } }
                }
            };

            return SerializationTestRunner.RunTest(original, typeof(TestContracts.DirectoryContract));
            """;

        var result = await RunSerializationTest(dataContractCode, testCode);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.True(result.XmlEquivalent, "Serialized XMLs should be semantically equivalent");
        Assert.True(result.DeserializedEqual, "Deserialized objects should be equal to originals");
    }

    /// <summary>
    /// Tests serialization with char property.
    /// </summary>
    [Fact]
    public async Task CharProperty_SerializesEquivalently()
    {
        var dataContractCode = """
            using System;
            using System.Runtime.Serialization;

            namespace TestContracts;

            [DataContract(Name = "CharData", Namespace = "http://test.contracts")]
            public class CharDataContract
            {
                [DataMember(Order = 1)]
                public char Letter { get; set; }

                [DataMember(Order = 2)]
                public char Digit { get; set; }

                [DataMember(Order = 3)]
                public char Special { get; set; }
            }
            """;

        var testCode = """
            var original = new TestContracts.CharDataContract
            {
                Letter = 'A',
                Digit = '5',
                Special = '@'
            };

            return SerializationTestRunner.RunTest(original, typeof(TestContracts.CharDataContract));
            """;

        var result = await RunSerializationTest(dataContractCode, testCode);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.True(result.XmlEquivalent, "Serialized XMLs should be semantically equivalent");
        Assert.True(result.DeserializedEqual, "Deserialized objects should be equal to originals");
    }

    /// <summary>
    /// Tests serialization with different date/time kinds.
    /// </summary>
    [Fact]
    public async Task DateTimeKinds_SerializesEquivalently()
    {
        var dataContractCode = """
            using System;
            using System.Runtime.Serialization;

            namespace TestContracts;

            [DataContract(Name = "DateTimes", Namespace = "http://test.contracts")]
            public class DateTimesContract
            {
                [DataMember(Order = 1)]
                public DateTime UtcDateTime { get; set; }

                [DataMember(Order = 2)]
                public DateTime LocalDateTime { get; set; }

                [DataMember(Order = 3)]
                public DateTime UnspecifiedDateTime { get; set; }
            }
            """;

        var testCode = """
            var original = new TestContracts.DateTimesContract
            {
                UtcDateTime = new DateTime(2024, 6, 15, 12, 0, 0, DateTimeKind.Utc),
                LocalDateTime = new DateTime(2024, 6, 15, 12, 0, 0, DateTimeKind.Local),
                UnspecifiedDateTime = new DateTime(2024, 6, 15, 12, 0, 0, DateTimeKind.Unspecified)
            };

            return SerializationTestRunner.RunTest(original, typeof(TestContracts.DateTimesContract));
            """;

        var result = await RunSerializationTest(dataContractCode, testCode);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.True(result.XmlEquivalent, "Serialized XMLs should be semantically equivalent");
        Assert.True(result.DeserializedEqual, "Deserialized objects should be equal to originals");
    }

    /// <summary>
    /// Tests serialization with multiple enums.
    /// </summary>
    [Fact]
    public async Task MultipleEnums_SerializesEquivalently()
    {
        var dataContractCode = """
            using System;
            using System.Runtime.Serialization;

            namespace TestContracts;

            [DataContract(Name = "Color", Namespace = "http://test.contracts")]
            public enum ColorEnum
            {
                [EnumMember] Red,
                [EnumMember] Green,
                [EnumMember] Blue
            }

            [DataContract(Name = "Size", Namespace = "http://test.contracts")]
            public enum SizeEnum
            {
                [EnumMember] Small,
                [EnumMember] Medium,
                [EnumMember] Large,
                [EnumMember] ExtraLarge
            }

            [DataContract(Name = "Product", Namespace = "http://test.contracts")]
            public class ProductWithEnumsContract
            {
                [DataMember(Order = 1)]
                public string Name { get; set; } = "";

                [DataMember(Order = 2)]
                public ColorEnum Color { get; set; }

                [DataMember(Order = 3)]
                public SizeEnum Size { get; set; }
            }
            """;

        var testCode = """
            var original = new TestContracts.ProductWithEnumsContract
            {
                Name = "T-Shirt",
                Color = TestContracts.ColorEnum.Blue,
                Size = TestContracts.SizeEnum.Medium
            };

            return SerializationTestRunner.RunTest(original, typeof(TestContracts.ProductWithEnumsContract));
            """;

        var result = await RunSerializationTest(dataContractCode, testCode);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.True(result.XmlEquivalent, "Serialized XMLs should be semantically equivalent");
        Assert.True(result.DeserializedEqual, "Deserialized objects should be equal to originals");
    }

    /// <summary>
    /// Tests serialization with self-referencing null (no circular reference, just null).
    /// </summary>
    [Fact]
    public async Task SelfReferencingTypeWithNull_SerializesEquivalently()
    {
        var dataContractCode = """
            using System;
            using System.Runtime.Serialization;

            namespace TestContracts;

            [DataContract(Name = "TreeNode", Namespace = "http://test.contracts")]
            public class TreeNodeContract
            {
                [DataMember(Order = 1)]
                public string Value { get; set; } = "";

                [DataMember(Order = 2)]
                public TreeNodeContract? Left { get; set; }

                [DataMember(Order = 3)]
                public TreeNodeContract? Right { get; set; }
            }
            """;

        var testCode = """
            var original = new TestContracts.TreeNodeContract
            {
                Value = "Root",
                Left = new TestContracts.TreeNodeContract
                {
                    Value = "Left",
                    Left = null,
                    Right = null
                },
                Right = new TestContracts.TreeNodeContract
                {
                    Value = "Right",
                    Left = null,
                    Right = null
                }
            };

            return SerializationTestRunner.RunTest(original, typeof(TestContracts.TreeNodeContract));
            """;

        var result = await RunSerializationTest(dataContractCode, testCode);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.True(result.XmlEquivalent, "Serialized XMLs should be semantically equivalent");
        Assert.True(result.DeserializedEqual, "Deserialized objects should be equal to originals");
    }

    /// <summary>
    /// Tests serialization of large byte array.
    /// </summary>
    [Fact]
    public async Task LargeByteArray_SerializesEquivalently()
    {
        var dataContractCode = """
            using System;
            using System.Runtime.Serialization;

            namespace TestContracts;

            [DataContract(Name = "BinaryBlob", Namespace = "http://test.contracts")]
            public class BinaryBlobContract
            {
                [DataMember(Order = 1)]
                public string Id { get; set; } = "";

                [DataMember(Order = 2)]
                public byte[] Data { get; set; } = Array.Empty<byte>();
            }
            """;

        var testCode = """
            var data = new byte[1000];
            for (int i = 0; i < data.Length; i++)
                data[i] = (byte)(i % 256);

            var original = new TestContracts.BinaryBlobContract
            {
                Id = "blob-001",
                Data = data
            };

            return SerializationTestRunner.RunTest(original, typeof(TestContracts.BinaryBlobContract));
            """;

        var result = await RunSerializationTest(dataContractCode, testCode);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.True(result.XmlEquivalent, "Serialized XMLs should be semantically equivalent");
        Assert.True(result.DeserializedEqual, "Deserialized objects should be equal to originals");
    }

    /// <summary>
    /// Tests serialization with all null reference properties.
    /// </summary>
    [Fact]
    public async Task AllNullProperties_SerializesEquivalently()
    {
        var dataContractCode = """
            using System;
            using System.Collections.Generic;
            using System.Runtime.Serialization;

            namespace TestContracts;

            [DataContract(Name = "NullContainer", Namespace = "http://test.contracts")]
            public class NullContainerContract
            {
                [DataMember(Order = 1)]
                public string? NullString { get; set; }

                [DataMember(Order = 2)]
                public byte[]? NullBytes { get; set; }

                [DataMember(Order = 3)]
                public List<int>? NullList { get; set; }

                [DataMember(Order = 4)]
                public Uri? NullUri { get; set; }
            }
            """;

        var testCode = """
            var original = new TestContracts.NullContainerContract
            {
                NullString = null,
                NullBytes = null,
                NullList = null,
                NullUri = null
            };

            return SerializationTestRunner.RunTest(original, typeof(TestContracts.NullContainerContract));
            """;

        var result = await RunSerializationTest(dataContractCode, testCode);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.True(result.XmlEquivalent, "Serialized XMLs should be semantically equivalent");
        Assert.True(result.DeserializedEqual, "Deserialized objects should be equal to originals");
    }

    /// <summary>
    /// Tests serialization with SortedDictionary.
    /// </summary>
    [Fact]
    public async Task SortedDictionaryProperty_SerializesEquivalently()
    {
        var dataContractCode = """
            using System;
            using System.Collections.Generic;
            using System.Runtime.Serialization;

            namespace TestContracts;

            [DataContract(Name = "SortedMap", Namespace = "http://test.contracts")]
            public class SortedMapContract
            {
                [DataMember(Order = 1)]
                public SortedDictionary<string, int> SortedItems { get; set; } = new();
            }
            """;

        var testCode = """
            var original = new TestContracts.SortedMapContract
            {
                SortedItems = new SortedDictionary<string, int>
                {
                    { "zebra", 3 },
                    { "apple", 1 },
                    { "mango", 2 }
                }
            };

            return SerializationTestRunner.RunTest(original, typeof(TestContracts.SortedMapContract));
            """;

        var result = await RunSerializationTest(dataContractCode, testCode);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.True(result.XmlEquivalent, "Serialized XMLs should be semantically equivalent");
        Assert.True(result.DeserializedEqual, "Deserialized objects should be equal to originals");
    }

    /// <summary>
    /// Tests serialization with mixed data member ordering.
    /// </summary>
    [Fact]
    public async Task NonSequentialDataMemberOrdering_SerializesEquivalently()
    {
        var dataContractCode = """
            using System;
            using System.Runtime.Serialization;

            namespace TestContracts;

            [DataContract(Name = "Ordered", Namespace = "http://test.contracts")]
            public class OrderedContract
            {
                [DataMember(Order = 10)]
                public string Tenth { get; set; } = "";

                [DataMember(Order = 1)]
                public string First { get; set; } = "";

                [DataMember(Order = 5)]
                public string Fifth { get; set; } = "";

                [DataMember(Order = 3)]
                public string Third { get; set; } = "";
            }
            """;

        var testCode = """
            var original = new TestContracts.OrderedContract
            {
                First = "A",
                Third = "C",
                Fifth = "E",
                Tenth = "J"
            };

            return SerializationTestRunner.RunTest(original, typeof(TestContracts.OrderedContract));
            """;

        var result = await RunSerializationTest(dataContractCode, testCode);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.True(result.XmlEquivalent, "Serialized XMLs should be semantically equivalent");
        Assert.True(result.DeserializedEqual, "Deserialized objects should be equal to originals");
    }

    /// <summary>
    /// Tests serialization with decimal edge values.
    /// </summary>
    [Fact]
    public async Task DecimalEdgeValues_SerializesEquivalently()
    {
        var dataContractCode = """
            using System;
            using System.Runtime.Serialization;

            namespace TestContracts;

            [DataContract(Name = "Decimals", Namespace = "http://test.contracts")]
            public class DecimalsContract
            {
                [DataMember(Order = 1)]
                public decimal Zero { get; set; }

                [DataMember(Order = 2)]
                public decimal NegativeValue { get; set; }

                [DataMember(Order = 3)]
                public decimal SmallValue { get; set; }

                [DataMember(Order = 4)]
                public decimal LargeValue { get; set; }

                [DataMember(Order = 5)]
                public decimal ManyDecimalPlaces { get; set; }
            }
            """;

        var testCode = """
            var original = new TestContracts.DecimalsContract
            {
                Zero = 0m,
                NegativeValue = -12345.67m,
                SmallValue = 0.0000001m,
                LargeValue = 9999999999999.99m,
                ManyDecimalPlaces = 1.23456789012345678901234567m
            };

            return SerializationTestRunner.RunTest(original, typeof(TestContracts.DecimalsContract));
            """;

        var result = await RunSerializationTest(dataContractCode, testCode);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.True(result.XmlEquivalent, "Serialized XMLs should be semantically equivalent");
        Assert.True(result.DeserializedEqual, "Deserialized objects should be equal to originals");
    }

    /// <summary>
    /// Tests serialization with List of enums.
    /// </summary>
    [Fact]
    public async Task ListOfEnums_SerializesEquivalently()
    {
        var dataContractCode = """
            using System;
            using System.Collections.Generic;
            using System.Runtime.Serialization;

            namespace TestContracts;

            [DataContract(Name = "DayOfWeek", Namespace = "http://test.contracts")]
            public enum DayOfWeekEnum
            {
                [EnumMember] Sunday,
                [EnumMember] Monday,
                [EnumMember] Tuesday,
                [EnumMember] Wednesday,
                [EnumMember] Thursday,
                [EnumMember] Friday,
                [EnumMember] Saturday
            }

            [DataContract(Name = "Schedule", Namespace = "http://test.contracts")]
            public class ScheduleContract
            {
                [DataMember(Order = 1)]
                public string Name { get; set; } = "";

                [DataMember(Order = 2)]
                public List<DayOfWeekEnum> WorkDays { get; set; } = new();
            }
            """;

        var testCode = """
            var original = new TestContracts.ScheduleContract
            {
                Name = "Standard Week",
                WorkDays = new List<TestContracts.DayOfWeekEnum>
                {
                    TestContracts.DayOfWeekEnum.Monday,
                    TestContracts.DayOfWeekEnum.Tuesday,
                    TestContracts.DayOfWeekEnum.Wednesday,
                    TestContracts.DayOfWeekEnum.Thursday,
                    TestContracts.DayOfWeekEnum.Friday
                }
            };

            return SerializationTestRunner.RunTest(original, typeof(TestContracts.ScheduleContract));
            """;

        var result = await RunSerializationTest(dataContractCode, testCode);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.True(result.XmlEquivalent, "Serialized XMLs should be semantically equivalent");
        Assert.True(result.DeserializedEqual, "Deserialized objects should be equal to originals");
    }
}
