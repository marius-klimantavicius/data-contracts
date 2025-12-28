using System.Threading.Tasks;
using Xunit;

namespace Marius.DataContracts.SourceGenerators.Tests;

/// <summary>
/// Tests for complex serialization scenarios including inheritance, structs,
/// preserve references, get-only collections, serialization callbacks, and special interfaces.
/// </summary>
public class ComplexEquivalenceTests
{
    private async Task<SerializationTestResult> RunSerializationTest(string dataContractCode, string testCode)
    {
        var testRunner = new SerializationTestCompiler();
        return await testRunner.CompileAndRunAsync(dataContractCode, testCode);
    }

    /// <summary>
    /// Tests inheritance with KnownType attribute.
    /// </summary>
    [Fact]
    public async Task Inheritance_WithKnownType_SerializesEquivalently()
    {
        var dataContractCode = """
            using System;
            using System.Runtime.Serialization;

            namespace TestContracts;

            [DataContract(Name = "Animal", Namespace = "http://test.contracts")]
            [KnownType(typeof(DogContract))]
            [KnownType(typeof(CatContract))]
            public class AnimalContract
            {
                [DataMember(Order = 1)]
                public string Name { get; set; } = "";

                [DataMember(Order = 2)]
                public int Age { get; set; }
            }

            [DataContract(Name = "Dog", Namespace = "http://test.contracts")]
            public class DogContract : AnimalContract
            {
                [DataMember(Order = 3)]
                public string Breed { get; set; } = "";
            }

            [DataContract(Name = "Cat", Namespace = "http://test.contracts")]
            public class CatContract : AnimalContract
            {
                [DataMember(Order = 3)]
                public bool IsIndoor { get; set; }
            }
            """;

        var testCode = """
            var original = new TestContracts.DogContract
            {
                Name = "Buddy",
                Age = 5,
                Breed = "Golden Retriever"
            };

            return SerializationTestRunner.RunTest(original, typeof(TestContracts.AnimalContract));
            """;

        var result = await RunSerializationTest(dataContractCode, testCode);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.True(result.XmlEquivalent, "Serialized XMLs should be semantically equivalent");
        Assert.True(result.DeserializedEqual, "Deserialized objects should be equal to originals");
    }

    /// <summary>
    /// Tests different contract names and namespaces.
    /// </summary>
    [Fact]
    public async Task DifferentNamesAndNamespaces_SerializesEquivalently()
    {
        var dataContractCode = """
            using System;
            using System.Runtime.Serialization;

            namespace TestContracts.Internal;

            [DataContract(Name = "ExternalPerson", Namespace = "http://external.api.com/v2")]
            public class InternalPersonContract
            {
                [DataMember(Name = "full_name", Order = 1)]
                public string FullName { get; set; } = "";

                [DataMember(Name = "date_of_birth", Order = 2)]
                public DateTime DateOfBirth { get; set; }

                [DataMember(Name = "contact_email", Order = 3)]
                public string Email { get; set; } = "";
            }
            """;

        var testCode = """
            var original = new TestContracts.Internal.InternalPersonContract
            {
                FullName = "Jane Smith",
                DateOfBirth = new DateTime(1990, 5, 15),
                Email = "jane.smith@example.com"
            };

            return SerializationTestRunner.RunTest(original, typeof(TestContracts.Internal.InternalPersonContract));
            """;

        var result = await RunSerializationTest(dataContractCode, testCode);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.True(result.XmlEquivalent, "Serialized XMLs should be semantically equivalent");
        Assert.True(result.DeserializedEqual, "Deserialized objects should be equal to originals");
    }

    /// <summary>
    /// Tests struct data contracts.
    /// </summary>
    [Fact]
    public async Task StructContract_SerializesEquivalently()
    {
        var dataContractCode = """
            using System;
            using System.Runtime.Serialization;

            namespace TestContracts;

            [DataContract(Name = "Point", Namespace = "http://test.contracts")]
            public struct PointContract
            {
                [DataMember(Order = 1)]
                public double X { get; set; }

                [DataMember(Order = 2)]
                public double Y { get; set; }

                [DataMember(Order = 3)]
                public double Z { get; set; }
            }

            [DataContract(Name = "Line", Namespace = "http://test.contracts")]
            public class LineContract
            {
                [DataMember(Order = 1)]
                public PointContract Start { get; set; }

                [DataMember(Order = 2)]
                public PointContract End { get; set; }
            }
            """;

        var testCode = """
            var original = new TestContracts.LineContract
            {
                Start = new TestContracts.PointContract { X = 1.0, Y = 2.0, Z = 3.0 },
                End = new TestContracts.PointContract { X = 4.0, Y = 5.0, Z = 6.0 }
            };

            return SerializationTestRunner.RunTest(original, typeof(TestContracts.LineContract));
            """;

        var result = await RunSerializationTest(dataContractCode, testCode);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.True(result.XmlEquivalent, "Serialized XMLs should be semantically equivalent");
        Assert.True(result.DeserializedEqual, "Deserialized objects should be equal to originals");
    }

    /// <summary>
    /// Tests IsReference for preserving object references.
    /// </summary>
    [Fact(Skip = "Need to pass preserve object references to DataContractSerializers")]
    public async Task IsReference_PreservesReferences_SerializesEquivalently()
    {
        var dataContractCode = """
            using System;
            using System.Runtime.Serialization;

            namespace TestContracts;

            [DataContract(Name = "Node", Namespace = "http://test.contracts", IsReference = true)]
            public class NodeContract
            {
                [DataMember(Order = 1)]
                public string Id { get; set; } = "";

                [DataMember(Order = 2)]
                public NodeContract? Parent { get; set; }

                [DataMember(Order = 3)]
                public NodeContract? Left { get; set; }

                [DataMember(Order = 4)]
                public NodeContract? Right { get; set; }
            }
            """;

        var testCode = """
            var root = new TestContracts.NodeContract { Id = "root" };
            var left = new TestContracts.NodeContract { Id = "left", Parent = root };
            var right = new TestContracts.NodeContract { Id = "right", Parent = root };
            root.Left = left;
            root.Right = right;

            return SerializationTestRunner.RunTest(root, typeof(TestContracts.NodeContract));
            """;

        var result = await RunSerializationTest(dataContractCode, testCode);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.True(result.XmlEquivalent, "Serialized XMLs should be semantically equivalent");
        Assert.True(result.DeserializedEqual, "Deserialized objects should be equal to originals");
    }

    /// <summary>
    /// Tests get-only collection properties.
    /// </summary>
    [Fact]
    public async Task GetOnlyCollection_SerializesEquivalently()
    {
        var dataContractCode = """
            using System;
            using System.Collections.Generic;
            using System.Runtime.Serialization;

            namespace TestContracts;

            [DataContract(Name = "Team", Namespace = "http://test.contracts")]
            public class TeamContract
            {
                [DataMember(Order = 1)]
                public string Name { get; set; } = "";

                private List<string>? _members;
            
                [DataMember(Order = 2)]
                public List<string> Members => _members ??= new List<string>();
            }
            """;

        var testCode = """
            var original = new TestContracts.TeamContract { Name = "Dev Team" };
            original.Members.Add("Alice");
            original.Members.Add("Bob");
            original.Members.Add("Charlie");

            return SerializationTestRunner.RunTest(original, typeof(TestContracts.TeamContract));
            """;

        var result = await RunSerializationTest(dataContractCode, testCode);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.True(result.XmlEquivalent, "Serialized XMLs should be semantically equivalent");
        Assert.True(result.DeserializedEqual, "Deserialized objects should be equal to originals");
    }

    /// <summary>
    /// Tests serialization callbacks (OnDeserializing, OnDeserialized, OnSerializing, OnSerialized).
    /// </summary>
    [Fact(Skip = "On(De)Serializ(ing|ed) is not implemneted")]
    public async Task SerializationCallbacks_SerializesEquivalently()
    {
        var dataContractCode = """
            using System;
            using System.Runtime.Serialization;

            namespace TestContracts;

            [DataContract(Name = "CallbackTest", Namespace = "http://test.contracts")]
            public class CallbackTestContract
            {
                [DataMember(Order = 1)]
                public string Value { get; set; } = "";

                [DataMember(Order = 2)]
                public int Counter { get; set; }

                [OnSerializing]
                private void OnSerializing(StreamingContext context)
                {
                    Counter++;
                }

                [OnSerialized]
                private void OnSerialized(StreamingContext context)
                {
                    Counter++;
                }

                [OnDeserializing]
                private void OnDeserializing(StreamingContext context)
                {
                    Counter = 100;
                }

                [OnDeserialized]
                private void OnDeserialized(StreamingContext context)
                {
                    Counter++;
                }
            }
            """;

        var testCode = """
            var original = new TestContracts.CallbackTestContract
            {
                Value = "Test",
                Counter = 0
            };

            return SerializationTestRunner.RunTest(original, typeof(TestContracts.CallbackTestContract));
            """;

        var result = await RunSerializationTest(dataContractCode, testCode);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.True(result.XmlEquivalent, "Serialized XMLs should be semantically equivalent");
    }

    /// <summary>
    /// Tests [Serializable] class without [DataContract].
    /// </summary>
    [Fact]
    public async Task SerializableAttribute_SerializesEquivalently()
    {
        var dataContractCode = """
            using System;

            namespace TestContracts;

            [DataContract]
            [KnownType(typeof(LegacyPerson))]
            public class Marker
            {
            }
            
            [Serializable]
            public class LegacyPerson
            {
                public string FirstName { get; set; } = "";
                public string LastName { get; set; } = "";
                public int Age { get; set; }
            }
            """;

        var testCode = """
            var original = new TestContracts.LegacyPerson
            {
                FirstName = "John",
                LastName = "Doe",
                Age = 30
            };

            return SerializationTestRunner.RunTest(original, typeof(TestContracts.LegacyPerson));
            """;

        var result = await RunSerializationTest(dataContractCode, testCode);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.True(result.XmlEquivalent, "Serialized XMLs should be semantically equivalent");
        Assert.True(result.DeserializedEqual, "Deserialized objects should be equal to originals");
    }

    /// <summary>
    /// Tests ISerializable interface implementation.
    /// </summary>
    [Fact]
    public async Task ISerializable_SerializesEquivalently()
    {
        var dataContractCode = """
            using System;
            using System.Runtime.Serialization;

            namespace TestContracts;

            [DataContract]
            [KnownType(typeof(CustomSerializable))]
            public class Marker
            {
            }
            
            [Serializable]
            public class CustomSerializable : ISerializable
            {
                public string Data { get; set; } = "";
                public int Value { get; set; }
                public DateTime Dt { get; set; }

                public CustomSerializable() { }

                protected CustomSerializable(SerializationInfo info, StreamingContext context)
                {
                    Data = info.GetString("CustomData") ?? "";
                    Value = info.GetInt32("CustomValue");
                    Dt = info.GetDateTime("CustomDateTime");
                }

                public void GetObjectData(SerializationInfo info, StreamingContext context)
                {
                    info.AddValue("CustomData", Data);
                    info.AddValue("CustomValue", Value);
                    info.AddValue("CustomDateTime", Dt);
                }
            }
            """;

        var testCode = """
            var original = new TestContracts.CustomSerializable
            {
                Data = "Test Data",
                Value = 42,
                Dt = new DateTime(2024, 6, 20),
            };

            return SerializationTestRunner.RunTest(original, typeof(TestContracts.CustomSerializable));
            """;

        var result = await RunSerializationTest(dataContractCode, testCode);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.True(result.XmlEquivalent, "Serialized XMLs should be semantically equivalent");
        Assert.True(result.DeserializedEqual, "Deserialized objects should be equal to originals");
    }

    /// <summary>
    /// Tests IXmlSerializable interface implementation.
    /// </summary>
    [Fact]
    public async Task IXmlSerializable_SerializesEquivalently()
    {
        var dataContractCode = """
            using System;
            using System.Xml;
            using System.Xml.Schema;
            using System.Xml.Serialization;

            namespace TestContracts;
            
            [DataContract]
            [KnownType(typeof(XmlSerializableData))]
            public class Marker
            {
            }

            public class XmlSerializableData : IXmlSerializable
            {
                public string Name { get; set; } = "";
                public int Value { get; set; }

                public XmlSchema? GetSchema() => null;

                public void ReadXml(XmlReader reader)
                {
                    reader.ReadStartElement();
                    Name = reader.ReadElementContentAsString("Name", "");
                    Value = reader.ReadElementContentAsInt("Value", "");
                    reader.ReadEndElement();
                }

                public void WriteXml(XmlWriter writer)
                {
                    writer.WriteElementString("Name", "", Name);
                    writer.WriteElementString("Value", "", Value.ToString());
                }
            }
            """;

        var testCode = """
            var original = new TestContracts.XmlSerializableData
            {
                Name = "Test",
                Value = 123
            };

            return SerializationTestRunner.RunTest(original, typeof(TestContracts.XmlSerializableData));
            """;

        var result = await RunSerializationTest(dataContractCode, testCode);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.True(result.XmlEquivalent, "Serialized XMLs should be semantically equivalent");
        Assert.True(result.DeserializedEqual, "Deserialized objects should be equal to originals");
    }

    /// <summary>
    /// Tests deep inheritance hierarchy with multiple levels.
    /// </summary>
    [Fact]
    public async Task DeepInheritanceHierarchy_SerializesEquivalently()
    {
        var dataContractCode = """
            using System;
            using System.Runtime.Serialization;

            namespace TestContracts;

            [DataContract(Name = "Base", Namespace = "http://test.contracts")]
            [KnownType(typeof(Level1Contract))]
            public class BaseContract
            {
                [DataMember(Order = 1)]
                public string BaseValue { get; set; } = "";
            }

            [DataContract(Name = "Level1", Namespace = "http://test.contracts")]
            [KnownType(typeof(Level2Contract))]
            public class Level1Contract : BaseContract
            {
                [DataMember(Order = 2)]
                public string Level1Value { get; set; } = "";
            }

            [DataContract(Name = "Level2", Namespace = "http://test.contracts")]
            [KnownType(typeof(Level3Contract))]
            public class Level2Contract : Level1Contract
            {
                [DataMember(Order = 3)]
                public string Level2Value { get; set; } = "";
            }

            [DataContract(Name = "Level3", Namespace = "http://test.contracts")]
            public class Level3Contract : Level2Contract
            {
                [DataMember(Order = 4)]
                public string Level3Value { get; set; } = "";
            }
            """;

        var testCode = """
            var original = new TestContracts.Level3Contract
            {
                BaseValue = "Base",
                Level1Value = "L1",
                Level2Value = "L2",
                Level3Value = "L3"
            };

            return SerializationTestRunner.RunTest(original, typeof(TestContracts.BaseContract));
            """;

        var result = await RunSerializationTest(dataContractCode, testCode);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.True(result.XmlEquivalent, "Serialized XMLs should be semantically equivalent");
        Assert.True(result.DeserializedEqual, "Deserialized objects should be equal to originals");
    }

    /// <summary>
    /// Tests nullable struct values.
    /// </summary>
    [Fact]
    public async Task NullableStruct_SerializesEquivalently()
    {
        var dataContractCode = """
            using System;
            using System.Collections.Generic;
            using System.Runtime.Serialization;

            namespace TestContracts;

            [DataContract(Name = "Coordinate", Namespace = "http://test.contracts")]
            public struct CoordinateContract
            {
                [DataMember(Order = 1)]
                public double Latitude { get; set; }

                [DataMember(Order = 2)]
                public double Longitude { get; set; }
            }

            [DataContract(Name = "Location", Namespace = "http://test.contracts")]
            public class LocationContract
            {
                [DataMember(Order = 1)]
                public string Name { get; set; } = "";

                [DataMember(Order = 2)]
                public CoordinateContract? Position { get; set; }

                [DataMember(Order = 3)]
                public List<CoordinateContract?> Waypoints { get; set; } = new();
            }
            """;

        var testCode = """
            var original = new TestContracts.LocationContract
            {
                Name = "Route",
                Position = new TestContracts.CoordinateContract { Latitude = 51.5074, Longitude = -0.1278 },
                Waypoints = new List<TestContracts.CoordinateContract?>
                {
                    new TestContracts.CoordinateContract { Latitude = 48.8566, Longitude = 2.3522 },
                    null,
                    new TestContracts.CoordinateContract { Latitude = 40.7128, Longitude = -74.0060 }
                }
            };

            return SerializationTestRunner.RunTest(original, typeof(TestContracts.LocationContract));
            """;

        var result = await RunSerializationTest(dataContractCode, testCode);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.True(result.XmlEquivalent, "Serialized XMLs should be semantically equivalent");
        Assert.True(result.DeserializedEqual, "Deserialized objects should be equal to originals");
    }

    /// <summary>
    /// Tests circular references with multiple objects.
    /// </summary>
    [Fact(Skip = "Need to pass preserve object references to DataContractSerializers")]
    public async Task CircularReferences_WithMultipleObjects_SerializesEquivalently()
    {
        var dataContractCode = """
            using System;
            using System.Collections.Generic;
            using System.Runtime.Serialization;

            namespace TestContracts;

            [DataContract(Name = "Person", Namespace = "http://test.contracts", IsReference = true)]
            public class PersonWithFriendsContract
            {
                [DataMember(Order = 1)]
                public string Name { get; set; } = "";

                [DataMember(Order = 2)]
                public List<PersonWithFriendsContract> Friends { get; set; } = new();
            }
            """;

        var testCode = """
            var alice = new TestContracts.PersonWithFriendsContract { Name = "Alice" };
            var bob = new TestContracts.PersonWithFriendsContract { Name = "Bob" };
            var charlie = new TestContracts.PersonWithFriendsContract { Name = "Charlie" };
            
            alice.Friends.Add(bob);
            alice.Friends.Add(charlie);
            bob.Friends.Add(alice);
            bob.Friends.Add(charlie);
            charlie.Friends.Add(alice);
            charlie.Friends.Add(bob);

            return SerializationTestRunner.RunTest(alice, typeof(TestContracts.PersonWithFriendsContract));
            """;

        var result = await RunSerializationTest(dataContractCode, testCode);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.True(result.XmlEquivalent, "Serialized XMLs should be semantically equivalent");
        Assert.True(result.DeserializedEqual, "Deserialized objects should be equal to originals");
    }

    /// <summary>
    /// Tests polymorphic collection with mixed derived types.
    /// </summary>
    [Fact]
    public async Task PolymorphicCollection_SerializesEquivalently()
    {
        var dataContractCode = """
            using System;
            using System.Collections.Generic;
            using System.Runtime.Serialization;

            namespace TestContracts;

            [DataContract(Name = "Shape", Namespace = "http://test.contracts")]
            [KnownType(typeof(CircleContract))]
            [KnownType(typeof(RectangleContract))]
            [KnownType(typeof(TriangleContract))]
            public abstract class ShapeContract
            {
                [DataMember(Order = 1)]
                public string Name { get; set; } = "";
            }

            [DataContract(Name = "Circle", Namespace = "http://test.contracts")]
            public class CircleContract : ShapeContract
            {
                [DataMember(Order = 2)]
                public double Radius { get; set; }
            }

            [DataContract(Name = "Rectangle", Namespace = "http://test.contracts")]
            public class RectangleContract : ShapeContract
            {
                [DataMember(Order = 2)]
                public double Width { get; set; }

                [DataMember(Order = 3)]
                public double Height { get; set; }
            }

            [DataContract(Name = "Triangle", Namespace = "http://test.contracts")]
            public class TriangleContract : ShapeContract
            {
                [DataMember(Order = 2)]
                public double Base { get; set; }

                [DataMember(Order = 3)]
                public double Height { get; set; }
            }

            [DataContract(Name = "Canvas", Namespace = "http://test.contracts")]
            public class CanvasContract
            {
                [DataMember(Order = 1)]
                public string Title { get; set; } = "";

                [DataMember(Order = 2)]
                public List<ShapeContract> Shapes { get; set; } = new();
            }
            """;

        var testCode = """
            var original = new TestContracts.CanvasContract
            {
                Title = "My Canvas",
                Shapes = new List<TestContracts.ShapeContract>
                {
                    new TestContracts.CircleContract { Name = "Circle1", Radius = 5.0 },
                    new TestContracts.RectangleContract { Name = "Rect1", Width = 10.0, Height = 20.0 },
                    new TestContracts.TriangleContract { Name = "Tri1", Base = 8.0, Height = 12.0 }
                }
            };

            return SerializationTestRunner.RunTest(original, typeof(TestContracts.CanvasContract));
            """;

        var result = await RunSerializationTest(dataContractCode, testCode);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.True(result.XmlEquivalent, "Serialized XMLs should be semantically equivalent");
        Assert.True(result.DeserializedEqual, "Deserialized objects should be equal to originals");
    }

    /// <summary>
    /// Tests private setters and fields.
    /// </summary>
    [Fact]
    public async Task PrivateSetters_SerializesEquivalently()
    {
        var dataContractCode = """
            using System;
            using System.Runtime.Serialization;

            namespace TestContracts;

            [DataContract(Name = "Immutable", Namespace = "http://test.contracts")]
            public class ImmutableContract
            {
                [DataMember(Order = 1)]
                public string Id { get; private set; }

                [DataMember(Order = 2)]
                public DateTime CreatedAt { get; private set; }

                [DataMember(Order = 3)]
                private string _secret = "";

                public string Secret => _secret;

                public ImmutableContract()
                {
                    Id = "";
                    CreatedAt = DateTime.MinValue;
                }

                public ImmutableContract(string id, DateTime createdAt, string secret)
                {
                    Id = id;
                    CreatedAt = createdAt;
                    _secret = secret;
                }
            }
            """;

        var testCode = """
            var original = new TestContracts.ImmutableContract("ID-123", new DateTime(2024, 6, 15), "secret-value");

            return SerializationTestRunner.RunTest(original, typeof(TestContracts.ImmutableContract));
            """;

        var result = await RunSerializationTest(dataContractCode, testCode);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.True(result.XmlEquivalent, "Serialized XMLs should be semantically equivalent");
        Assert.True(result.DeserializedEqual, "Deserialized objects should be equal to originals");
    }

    /// <summary>
    /// Tests nested collections (List of Lists, Dictionary of Lists).
    /// </summary>
    [Fact]
    public async Task NestedCollections_SerializesEquivalently()
    {
        var dataContractCode = """
            using System;
            using System.Collections.Generic;
            using System.Runtime.Serialization;

            namespace TestContracts;

            [DataContract(Name = "Matrix", Namespace = "http://test.contracts")]
            public class MatrixContract
            {
                [DataMember(Order = 1)]
                public string Name { get; set; } = "";

                [DataMember(Order = 2)]
                public List<List<int>> Rows { get; set; } = new();

                [DataMember(Order = 3)]
                public Dictionary<string, List<double>> NamedVectors { get; set; } = new();
            }
            """;

        var testCode = """
            var original = new TestContracts.MatrixContract
            {
                Name = "TestMatrix",
                Rows = new List<List<int>>
                {
                    new List<int> { 1, 2, 3 },
                    new List<int> { 4, 5, 6 },
                    new List<int> { 7, 8, 9 }
                },
                NamedVectors = new Dictionary<string, List<double>>
                {
                    { "vector1", new List<double> { 1.1, 2.2, 3.3 } },
                    { "vector2", new List<double> { 4.4, 5.5, 6.6 } }
                }
            };

            return SerializationTestRunner.RunTest(original, typeof(TestContracts.MatrixContract));
            """;

        var result = await RunSerializationTest(dataContractCode, testCode);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.True(result.XmlEquivalent, "Serialized XMLs should be semantically equivalent");
        Assert.True(result.DeserializedEqual, "Deserialized objects should be equal to originals");
    }

    /// <summary>
    /// Tests custom collection with CollectionDataContract attribute.
    /// </summary>
    [Fact]
    public async Task CollectionDataContract_SerializesEquivalently()
    {
        var dataContractCode = """
            using System;
            using System.Collections.Generic;
            using System.Runtime.Serialization;

            namespace TestContracts;

            [CollectionDataContract(
                Name = "StringList",
                Namespace = "http://test.contracts",
                ItemName = "Item")]
            public class CustomStringList : List<string>
            {
            }

            [DataContract(Name = "Document", Namespace = "http://test.contracts")]
            public class DocumentContract
            {
                [DataMember(Order = 1)]
                public string Title { get; set; } = "";

                [DataMember(Order = 2)]
                public CustomStringList Tags { get; set; } = new();
            }
            """;

        var testCode = """
            var original = new TestContracts.DocumentContract
            {
                Title = "Important Document",
                Tags = new TestContracts.CustomStringList { "urgent", "confidential", "review" }
            };

            return SerializationTestRunner.RunTest(original, typeof(TestContracts.DocumentContract));
            """;

        var result = await RunSerializationTest(dataContractCode, testCode);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.True(result.XmlEquivalent, "Serialized XMLs should be semantically equivalent");
        Assert.True(result.DeserializedEqual, "Deserialized objects should be equal to originals");
    }

    /// <summary>
    /// Tests enum with custom EnumMember values.
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
                [EnumMember(Value = "LOW_PRIORITY")]
                Low = 0,

                [EnumMember(Value = "MEDIUM_PRIORITY")]
                Medium = 1,

                [EnumMember(Value = "HIGH_PRIORITY")]
                High = 2,

                [EnumMember(Value = "CRITICAL_PRIORITY")]
                Critical = 3
            }

            [DataContract(Name = "Task", Namespace = "http://test.contracts")]
            public class TaskContract
            {
                [DataMember(Order = 1)]
                public string Description { get; set; } = "";

                [DataMember(Order = 2)]
                public PriorityEnum Priority { get; set; }

                [DataMember(Order = 3)]
                public PriorityEnum? OptionalPriority { get; set; }
            }
            """;

        var testCode = """
            var original = new TestContracts.TaskContract
            {
                Description = "Fix critical bug",
                Priority = TestContracts.PriorityEnum.Critical,
                OptionalPriority = TestContracts.PriorityEnum.High
            };

            return SerializationTestRunner.RunTest(original, typeof(TestContracts.TaskContract));
            """;

        var result = await RunSerializationTest(dataContractCode, testCode);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.True(result.XmlEquivalent, "Serialized XMLs should be semantically equivalent");
        Assert.True(result.DeserializedEqual, "Deserialized objects should be equal to originals");
    }

    /// <summary>
    /// Tests DataContract with no explicit order (alphabetical ordering).
    /// </summary>
    [Fact]
    public async Task AlphabeticalOrdering_SerializesEquivalently()
    {
        var dataContractCode = """
            using System;
            using System.Runtime.Serialization;

            namespace TestContracts;

            [DataContract(Name = "Alphabetical", Namespace = "http://test.contracts")]
            public class AlphabeticalContract
            {
                [DataMember]
                public string Zebra { get; set; } = "";

                [DataMember]
                public string Alpha { get; set; } = "";

                [DataMember]
                public string Middle { get; set; } = "";

                [DataMember]
                public int Numeric { get; set; }
            }
            """;

        var testCode = """
            var original = new TestContracts.AlphabeticalContract
            {
                Zebra = "Last",
                Alpha = "First",
                Middle = "Center",
                Numeric = 42
            };

            return SerializationTestRunner.RunTest(original, typeof(TestContracts.AlphabeticalContract));
            """;

        var result = await RunSerializationTest(dataContractCode, testCode);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.True(result.XmlEquivalent, "Serialized XMLs should be semantically equivalent");
        Assert.True(result.DeserializedEqual, "Deserialized objects should be equal to originals");
    }

    /// <summary>
    /// Tests IsRequired on DataMember.
    /// </summary>
    [Fact]
    public async Task IsRequired_SerializesEquivalently()
    {
        var dataContractCode = """
            using System;
            using System.Runtime.Serialization;

            namespace TestContracts;

            [DataContract(Name = "Required", Namespace = "http://test.contracts")]
            public class RequiredContract
            {
                [DataMember(Order = 1, IsRequired = true)]
                public string MandatoryField { get; set; } = "";

                [DataMember(Order = 2, IsRequired = false)]
                public string OptionalField { get; set; } = "";

                [DataMember(Order = 3, IsRequired = true)]
                public int MandatoryNumber { get; set; }
            }
            """;

        var testCode = """
            var original = new TestContracts.RequiredContract
            {
                MandatoryField = "Required Value",
                OptionalField = "Optional Value",
                MandatoryNumber = 100
            };

            return SerializationTestRunner.RunTest(original, typeof(TestContracts.RequiredContract));
            """;

        var result = await RunSerializationTest(dataContractCode, testCode);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.True(result.XmlEquivalent, "Serialized XMLs should be semantically equivalent");
        Assert.True(result.DeserializedEqual, "Deserialized objects should be equal to originals");
    }

    /// <summary>
    /// Tests readonly field with DataMember.
    /// </summary>
    [Fact]
    public async Task ReadonlyField_SerializesEquivalently()
    {
        var dataContractCode = """
            using System;
            using System.Runtime.Serialization;

            namespace TestContracts;

            [DataContract(Name = "ReadonlyTest", Namespace = "http://test.contracts")]
            public class ReadonlyFieldContract
            {
                [DataMember(Order = 1)]
                public readonly string Id;

                [DataMember(Order = 2)]
                public string Name { get; set; } = "";

                public ReadonlyFieldContract()
                {
                    Id = "";
                }

                public ReadonlyFieldContract(string id, string name)
                {
                    Id = id;
                    Name = name;
                }
            }
            """;

        var testCode = """
            var original = new TestContracts.ReadonlyFieldContract("readonly-123", "Test Name");

            return SerializationTestRunner.RunTest(original, typeof(TestContracts.ReadonlyFieldContract));
            """;

        var result = await RunSerializationTest(dataContractCode, testCode);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.True(result.XmlEquivalent, "Serialized XMLs should be semantically equivalent");
        Assert.True(result.DeserializedEqual, "Deserialized objects should be equal to originals");
    }

    /// <summary>
    /// Tests self-referencing type without circular reference (tree structure).
    /// </summary>
    [Fact]
    public async Task SelfReferencing_TreeStructure_SerializesEquivalently()
    {
        var dataContractCode = """
            using System;
            using System.Collections.Generic;
            using System.Runtime.Serialization;

            namespace TestContracts;

            [DataContract(Name = "TreeNode", Namespace = "http://test.contracts")]
            public class TreeNodeContract
            {
                [DataMember(Order = 1)]
                public string Value { get; set; } = "";

                [DataMember(Order = 2)]
                public List<TreeNodeContract> Children { get; set; } = new();
            }
            """;

        var testCode = """
            var original = new TestContracts.TreeNodeContract
            {
                Value = "Root",
                Children = new List<TestContracts.TreeNodeContract>
                {
                    new TestContracts.TreeNodeContract
                    {
                        Value = "Child1",
                        Children = new List<TestContracts.TreeNodeContract>
                        {
                            new TestContracts.TreeNodeContract { Value = "Grandchild1" },
                            new TestContracts.TreeNodeContract { Value = "Grandchild2" }
                        }
                    },
                    new TestContracts.TreeNodeContract { Value = "Child2" }
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
    /// Tests Dictionary with complex value type.
    /// </summary>
    [Fact]
    public async Task DictionaryWithComplexValue_SerializesEquivalently()
    {
        var dataContractCode = """
            using System;
            using System.Collections.Generic;
            using System.Runtime.Serialization;

            namespace TestContracts;

            [DataContract(Name = "Address", Namespace = "http://test.contracts")]
            public class AddressContract
            {
                [DataMember(Order = 1)]
                public string Street { get; set; } = "";

                [DataMember(Order = 2)]
                public string City { get; set; } = "";
            }

            [DataContract(Name = "AddressBook", Namespace = "http://test.contracts")]
            public class AddressBookContract
            {
                [DataMember(Order = 1)]
                public Dictionary<string, AddressContract> Addresses { get; set; } = new();
            }
            """;

        var testCode = """
            var original = new TestContracts.AddressBookContract
            {
                Addresses = new Dictionary<string, TestContracts.AddressContract>
                {
                    { "Home", new TestContracts.AddressContract { Street = "123 Main St", City = "Springfield" } },
                    { "Work", new TestContracts.AddressContract { Street = "456 Office Blvd", City = "Metropolis" } }
                }
            };

            return SerializationTestRunner.RunTest(original, typeof(TestContracts.AddressBookContract));
            """;

        var result = await RunSerializationTest(dataContractCode, testCode);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.True(result.XmlEquivalent, "Serialized XMLs should be semantically equivalent");
        Assert.True(result.DeserializedEqual, "Deserialized objects should be equal to originals");
    }

    /// <summary>
    /// Tests empty collections serialization.
    /// </summary>
    [Fact]
    public async Task EmptyCollections_SerializesEquivalently()
    {
        var dataContractCode = """
            using System;
            using System.Collections.Generic;
            using System.Runtime.Serialization;

            namespace TestContracts;

            [DataContract(Name = "EmptyCollection", Namespace = "http://test.contracts")]
            public class EmptyCollectionContract
            {
                [DataMember(Order = 1)]
                public List<string> EmptyList { get; set; } = new();

                [DataMember(Order = 2)]
                public Dictionary<string, int> EmptyDict { get; set; } = new();

                [DataMember(Order = 3)]
                public int[] EmptyArray { get; set; } = Array.Empty<int>();
            }
            """;

        var testCode = """
            var original = new TestContracts.EmptyCollectionContract();

            return SerializationTestRunner.RunTest(original, typeof(TestContracts.EmptyCollectionContract));
            """;

        var result = await RunSerializationTest(dataContractCode, testCode);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.True(result.XmlEquivalent, "Serialized XMLs should be semantically equivalent");
        Assert.True(result.DeserializedEqual, "Deserialized objects should be equal to originals");
    }

    /// <summary>
    /// Tests flags enum serialization.
    /// </summary>
    [Fact]
    public async Task FlagsEnum_SerializesEquivalently()
    {
        var dataContractCode = """
            using System;
            using System.Runtime.Serialization;

            namespace TestContracts;

            [Flags]
            [DataContract(Name = "Permissions", Namespace = "http://test.contracts")]
            public enum PermissionsEnum
            {
                [EnumMember]
                None = 0,

                [EnumMember]
                Read = 1,

                [EnumMember]
                Write = 2,

                [EnumMember]
                Execute = 4,

                [EnumMember]
                All = Read | Write | Execute
            }

            [DataContract(Name = "User", Namespace = "http://test.contracts")]
            public class UserContract
            {
                [DataMember(Order = 1)]
                public string Username { get; set; } = "";

                [DataMember(Order = 2)]
                public PermissionsEnum Permissions { get; set; }
            }
            """;

        var testCode = """
            var original = new TestContracts.UserContract
            {
                Username = "admin",
                Permissions = TestContracts.PermissionsEnum.Read | TestContracts.PermissionsEnum.Write
            };

            return SerializationTestRunner.RunTest(original, typeof(TestContracts.UserContract));
            """;

        var result = await RunSerializationTest(dataContractCode, testCode);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.True(result.XmlEquivalent, "Serialized XMLs should be semantically equivalent");
        Assert.True(result.DeserializedEqual, "Deserialized objects should be equal to originals");
    }

    /// <summary>
    /// Tests DateTimeOffset serialization.
    /// </summary>
    [Fact(Skip = "DateTimeOffset adapter is not implemented")]
    public async Task DateTimeOffset_SerializesEquivalently()
    {
        var dataContractCode = """
            using System;
            using System.Runtime.Serialization;

            namespace TestContracts;

            [DataContract(Name = "Event", Namespace = "http://test.contracts")]
            public class EventWithOffsetContract
            {
                [DataMember(Order = 1)]
                public string Name { get; set; } = "";

                [DataMember(Order = 2)]
                public DateTimeOffset StartTime { get; set; }

                [DataMember(Order = 3)]
                public DateTimeOffset? EndTime { get; set; }
            }
            """;

        var testCode = """
            var original = new TestContracts.EventWithOffsetContract
            {
                Name = "Conference",
                StartTime = new DateTimeOffset(2024, 6, 15, 9, 0, 0, TimeSpan.FromHours(-5)),
                EndTime = new DateTimeOffset(2024, 6, 15, 17, 0, 0, TimeSpan.FromHours(-5))
            };

            return SerializationTestRunner.RunTest(original, typeof(TestContracts.EventWithOffsetContract));
            """;

        var result = await RunSerializationTest(dataContractCode, testCode);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.True(result.XmlEquivalent, "Serialized XMLs should be semantically equivalent");
        Assert.True(result.DeserializedEqual, "Deserialized objects should be equal to originals");
    }

    /// <summary>
    /// Tests Uri serialization.
    /// </summary>
    [Fact]
    public async Task Uri_SerializesEquivalently()
    {
        var dataContractCode = """
            using System;
            using System.Runtime.Serialization;

            namespace TestContracts;

            [DataContract(Name = "WebResource", Namespace = "http://test.contracts")]
            public class WebResourceContract
            {
                [DataMember(Order = 1)]
                public string Title { get; set; } = "";

                [DataMember(Order = 2)]
                public Uri? Url { get; set; }

                [DataMember(Order = 3)]
                public Uri? FallbackUrl { get; set; }
            }
            """;

        var testCode = """
            var original = new TestContracts.WebResourceContract
            {
                Title = "Example Site",
                Url = new Uri("https://example.com/path?query=value"),
                FallbackUrl = null
            };

            return SerializationTestRunner.RunTest(original, typeof(TestContracts.WebResourceContract));
            """;

        var result = await RunSerializationTest(dataContractCode, testCode);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.True(result.XmlEquivalent, "Serialized XMLs should be semantically equivalent");
        Assert.True(result.DeserializedEqual, "Deserialized objects should be equal to originals");
    }

    /// <summary>
    /// Tests object array with mixed types.
    /// </summary>
    [Fact]
    public async Task ObjectArrayWithMixedTypes_SerializesEquivalently()
    {
        var dataContractCode = """
            using System;
            using System.Runtime.Serialization;

            namespace TestContracts;

            [DataContract(Name = "MixedContainer", Namespace = "http://test.contracts")]
            [KnownType(typeof(string))]
            [KnownType(typeof(int))]
            [KnownType(typeof(bool))]
            [KnownType(typeof(decimal))]
            public class MixedContainerContract
            {
                [DataMember(Order = 1)]
                public object?[] Values { get; set; } = Array.Empty<object>();
            }
            """;

        var testCode = """
            var original = new TestContracts.MixedContainerContract
            {
                Values = new object?[] { "Hello", 42, true, 99.99m, null }
            };

            return SerializationTestRunner.RunTest(original, typeof(TestContracts.MixedContainerContract));
            """;

        var result = await RunSerializationTest(dataContractCode, testCode);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.True(result.XmlEquivalent, "Serialized XMLs should be semantically equivalent");
        Assert.True(result.DeserializedEqual, "Deserialized objects should be equal to originals");
    }

    /// <summary>
    /// Tests XmlElement property serialization.
    /// </summary>
    [Fact]
    public async Task XmlElement_SerializesEquivalently()
    {
        var dataContractCode = """
            using System;
            using System.Runtime.Serialization;
            using System.Xml;

            namespace TestContracts;

            [DataContract(Name = "XmlDocument", Namespace = "http://test.contracts")]
            public class XmlDocumentContract
            {
                [DataMember(Order = 1)]
                public string Name { get; set; } = "";

                [DataMember(Order = 2)]
                public XmlElement? Content { get; set; }
            }
            """;

        var testCode = """
            var doc = new XmlDocument();
            doc.LoadXml("<root><child attr='value'>Text</child></root>");
            
            var original = new TestContracts.XmlDocumentContract
            {
                Name = "Test Document",
                Content = doc.DocumentElement
            };

            return SerializationTestRunner.RunTest(original, typeof(TestContracts.XmlDocumentContract));
            """;

        var result = await RunSerializationTest(dataContractCode, testCode);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.True(result.XmlEquivalent, "Serialized XMLs should be semantically equivalent");
        Assert.True(result.DeserializedEqual, "Deserialized objects should be equal to originals");
    }

    /// <summary>
    /// Tests HashSet serialization.
    /// </summary>
    [Fact]
    public async Task HashSet_SerializesEquivalently()
    {
        var dataContractCode = """
            using System;
            using System.Collections.Generic;
            using System.Runtime.Serialization;

            namespace TestContracts;

            [DataContract(Name = "TagContainer", Namespace = "http://test.contracts")]
            public class TagContainerContract
            {
                [DataMember(Order = 1)]
                public string Name { get; set; } = "";

                [DataMember(Order = 2)]
                public HashSet<string> Tags { get; set; } = new();

                [DataMember(Order = 3)]
                public HashSet<int> Ids { get; set; } = new();
            }
            """;

        var testCode = """
            var original = new TestContracts.TagContainerContract
            {
                Name = "Tagged Item",
                Tags = new HashSet<string> { "important", "urgent", "review" },
                Ids = new HashSet<int> { 1, 2, 3, 5, 8 }
            };

            return SerializationTestRunner.RunTest(original, typeof(TestContracts.TagContainerContract));
            """;

        var result = await RunSerializationTest(dataContractCode, testCode);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.True(result.XmlEquivalent, "Serialized XMLs should be semantically equivalent");
        Assert.True(result.DeserializedEqual, "Deserialized objects should be equal to originals");
    }

    /// <summary>
    /// Tests DataMember on fields.
    /// </summary>
    [Fact]
    public async Task DataMemberOnFields_SerializesEquivalently()
    {
        var dataContractCode = """
            using System;
            using System.Runtime.Serialization;

            namespace TestContracts;

            [DataContract(Name = "FieldBased", Namespace = "http://test.contracts")]
            public class FieldBasedContract
            {
                [DataMember(Order = 1)]
                public string PublicField = "";

                [DataMember(Order = 2)]
                private string _privateField = "";

                [DataMember(Order = 3)]
                internal int InternalField;

                public string PrivateFieldValue
                {
                    get => _privateField;
                    set => _privateField = value;
                }
            }
            """;

        var testCode = """
            var original = new TestContracts.FieldBasedContract
            {
                PublicField = "Public",
                PrivateFieldValue = "Private",
                InternalField = 42
            };

            return SerializationTestRunner.RunTest(original, typeof(TestContracts.FieldBasedContract));
            """;

        var result = await RunSerializationTest(dataContractCode, testCode);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.True(result.XmlEquivalent, "Serialized XMLs should be semantically equivalent");
        Assert.True(result.DeserializedEqual, "Deserialized objects should be equal to originals");
    }

    /// <summary>
    /// Tests nested generic types.
    /// </summary>
    [Fact]
    public async Task NestedGenerics_SerializesEquivalently()
    {
        var dataContractCode = """
            using System;
            using System.Collections.Generic;
            using System.Runtime.Serialization;

            namespace TestContracts;

            [DataContract(Name = "Wrapper", Namespace = "http://test.contracts")]
            public class WrapperContract<T>
            {
                [DataMember(Order = 1)]
                public T Value { get; set; } = default!;

                [DataMember(Order = 2)]
                public string Description { get; set; } = "";
            }

            [DataContract(Name = "Container", Namespace = "http://test.contracts")]
            public class ContainerContract
            {
                [DataMember(Order = 1)]
                public WrapperContract<WrapperContract<int>> Nested { get; set; } = new();
            }
            """;

        var testCode = """
            var original = new TestContracts.ContainerContract
            {
                Nested = new TestContracts.WrapperContract<TestContracts.WrapperContract<int>>
                {
                    Value = new TestContracts.WrapperContract<int>
                    {
                        Value = 42,
                        Description = "Inner wrapper"
                    },
                    Description = "Outer wrapper"
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
    /// Tests implicit data contract (POCO without attributes).
    /// </summary>
    [Fact]
    public async Task ImplicitDataContract_SerializesEquivalently()
    {
        var dataContractCode = """
            using System;

            namespace TestContracts;

            [DataContract]
            [KnownType(typeof(SimplePoco))]
            public class Marker { }
            
            public class SimplePoco
            {
                public string Name { get; set; } = "";
                public int Age { get; set; }
                public bool IsActive { get; set; }
            }
            """;

        var testCode = """
            var original = new TestContracts.SimplePoco
            {
                Name = "John Doe",
                Age = 30,
                IsActive = true
            };

            return SerializationTestRunner.RunTest(original, typeof(TestContracts.SimplePoco));
            """;

        var result = await RunSerializationTest(dataContractCode, testCode);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.True(result.XmlEquivalent, "Serialized XMLs should be semantically equivalent");
        Assert.True(result.DeserializedEqual, "Deserialized objects should be equal to originals");
    }

    /// <summary>
    /// Tests DataContract with non-DataContract base class.
    /// </summary>
    [Fact]
    public async Task DataContractWithNonDataContractBase_SerializesEquivalently()
    {
        var dataContractCode = """
            using System;
            using System.Runtime.Serialization;

            namespace TestContracts;

            [Serializable]
            public class BaseClass
            {
                public string BaseProperty { get; set; } = "";
            }

            [DataContract(Name = "Derived", Namespace = "http://test.contracts")]
            public class DerivedContract : BaseClass
            {
                [DataMember(Order = 1)]
                public string DerivedProperty { get; set; } = "";

                [DataMember(Order = 2)]
                public int DerivedNumber { get; set; }
            }
            """;

        var testCode = """
            var original = new TestContracts.DerivedContract
            {
                BaseProperty = "Base Value",
                DerivedProperty = "Derived Value",
                DerivedNumber = 42
            };

            return SerializationTestRunner.RunTest(original, typeof(TestContracts.DerivedContract));
            """;

        var result = await RunSerializationTest(dataContractCode, testCode);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.True(result.XmlEquivalent, "Serialized XMLs should be semantically equivalent");
        Assert.True(result.DeserializedEqual, "Deserialized objects should be equal to originals");
    }

    /// <summary>
    /// Tests abstract base class with concrete implementations.
    /// </summary>
    [Fact]
    public async Task AbstractBaseClass_SerializesEquivalently()
    {
        var dataContractCode = """
            using System;
            using System.Runtime.Serialization;

            namespace TestContracts;

            [DataContract(Name = "Vehicle", Namespace = "http://test.contracts")]
            [KnownType(typeof(CarContract))]
            [KnownType(typeof(MotorcycleContract))]
            public abstract class VehicleContract
            {
                [DataMember(Order = 1)]
                public string Make { get; set; } = "";

                [DataMember(Order = 2)]
                public string Model { get; set; } = "";

                [DataMember(Order = 3)]
                public int Year { get; set; }
            }

            [DataContract(Name = "Car", Namespace = "http://test.contracts")]
            public class CarContract : VehicleContract
            {
                [DataMember(Order = 4)]
                public int NumberOfDoors { get; set; }
            }

            [DataContract(Name = "Motorcycle", Namespace = "http://test.contracts")]
            public class MotorcycleContract : VehicleContract
            {
                [DataMember(Order = 4)]
                public bool HasSidecar { get; set; }
            }
            """;

        var testCode = """
            var original = new TestContracts.CarContract
            {
                Make = "Toyota",
                Model = "Camry",
                Year = 2023,
                NumberOfDoors = 4
            };

            return SerializationTestRunner.RunTest(original, typeof(TestContracts.VehicleContract));
            """;

        var result = await RunSerializationTest(dataContractCode, testCode);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.True(result.XmlEquivalent, "Serialized XMLs should be semantically equivalent");
        Assert.True(result.DeserializedEqual, "Deserialized objects should be equal to originals");
    }

    /// <summary>
    /// Tests sealed data contract class.
    /// </summary>
    [Fact]
    public async Task SealedClass_SerializesEquivalently()
    {
        var dataContractCode = """
            using System;
            using System.Runtime.Serialization;

            namespace TestContracts;

            [DataContract(Name = "Sealed", Namespace = "http://test.contracts")]
            public sealed class SealedContract
            {
                [DataMember(Order = 1)]
                public string Id { get; set; } = "";

                [DataMember(Order = 2)]
                public string Value { get; set; } = "";
            }
            """;

        var testCode = """
            var original = new TestContracts.SealedContract
            {
                Id = "sealed-001",
                Value = "Sealed Value"
            };

            return SerializationTestRunner.RunTest(original, typeof(TestContracts.SealedContract));
            """;

        var result = await RunSerializationTest(dataContractCode, testCode);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.True(result.XmlEquivalent, "Serialized XMLs should be semantically equivalent");
        Assert.True(result.DeserializedEqual, "Deserialized objects should be equal to originals");
    }

    /// <summary>
    /// Tests contracts from different namespaces.
    /// </summary>
    [Fact]
    public async Task MultipleNamespaces_SerializesEquivalently()
    {
        var dataContractCode = """
            using System;
            using System.Runtime.Serialization;

            namespace TestContracts.ModuleA
            {
                [DataContract(Name = "Address", Namespace = "http://moduleA.contracts")]
                public class AddressContract
                {
                    [DataMember(Order = 1)]
                    public string Street { get; set; } = "";

                    [DataMember(Order = 2)]
                    public string City { get; set; } = "";
                }
            }

            namespace TestContracts.ModuleB
            {
                using TestContracts.ModuleA;

                [DataContract(Name = "Company", Namespace = "http://moduleB.contracts")]
                public class CompanyContract
                {
                    [DataMember(Order = 1)]
                    public string Name { get; set; } = "";

                    [DataMember(Order = 2)]
                    public AddressContract? Headquarters { get; set; }
                }
            }
            """;

        var testCode = """
            var original = new TestContracts.ModuleB.CompanyContract
            {
                Name = "Acme Corp",
                Headquarters = new TestContracts.ModuleA.AddressContract
                {
                    Street = "123 Business Ave",
                    City = "Commerce City"
                }
            };

            return SerializationTestRunner.RunTest(original, typeof(TestContracts.ModuleB.CompanyContract));
            """;

        var result = await RunSerializationTest(dataContractCode, testCode);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.True(result.XmlEquivalent, "Serialized XMLs should be semantically equivalent");
        Assert.True(result.DeserializedEqual, "Deserialized objects should be equal to originals");
    }

    /// <summary>
    /// Tests nested types within a data contract.
    /// </summary>
    [Fact]
    public async Task NestedTypes_SerializesEquivalently()
    {
        var dataContractCode = """
            using System;
            using System.Runtime.Serialization;

            namespace TestContracts;

            [DataContract(Name = "Outer", Namespace = "http://test.contracts")]
            public class OuterContract
            {
                [DataContract(Name = "Inner", Namespace = "http://test.contracts")]
                public class InnerContract
                {
                    [DataMember(Order = 1)]
                    public string InnerValue { get; set; } = "";
                }

                [DataMember(Order = 1)]
                public string OuterValue { get; set; } = "";

                [DataMember(Order = 2)]
                public InnerContract? Nested { get; set; }
            }
            """;

        var testCode = """
            var original = new TestContracts.OuterContract
            {
                OuterValue = "Outer",
                Nested = new TestContracts.OuterContract.InnerContract
                {
                    InnerValue = "Inner"
                }
            };

            return SerializationTestRunner.RunTest(original, typeof(TestContracts.OuterContract));
            """;

        var result = await RunSerializationTest(dataContractCode, testCode);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.True(result.XmlEquivalent, "Serialized XMLs should be semantically equivalent");
        Assert.True(result.DeserializedEqual, "Deserialized objects should be equal to originals");
    }

    /// <summary>
    /// Tests data contract with private constructor.
    /// </summary>
    [Fact]
    public async Task PrivateConstructor_SerializesEquivalently()
    {
        var dataContractCode = """
            using System;
            using System.Runtime.Serialization;

            namespace TestContracts;

            [DataContract(Name = "PrivateCtor", Namespace = "http://test.contracts")]
            public class PrivateConstructorContract
            {
                [DataMember(Order = 1)]
                public string Id { get; private set; } = "";

                [DataMember(Order = 2)]
                public string Value { get; private set; } = "";

                private PrivateConstructorContract() { }

                public static PrivateConstructorContract Create(string id, string value)
                {
                    return new PrivateConstructorContract { Id = id, Value = value };
                }
            }
            """;

        var testCode = """
            var original = TestContracts.PrivateConstructorContract.Create("id-001", "Test Value");

            return SerializationTestRunner.RunTest(original, typeof(TestContracts.PrivateConstructorContract));
            """;

        var result = await RunSerializationTest(dataContractCode, testCode);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.True(result.XmlEquivalent, "Serialized XMLs should be semantically equivalent");
        Assert.True(result.DeserializedEqual, "Deserialized objects should be equal to originals");
    }

    /// <summary>
    /// Tests LinkedList collection serialization.
    /// </summary>
    [Fact]
    public async Task LinkedList_SerializesEquivalently()
    {
        var dataContractCode = """
            using System;
            using System.Collections.Generic;
            using System.Runtime.Serialization;

            namespace TestContracts;

            [DataContract(Name = "LinkedListContainer", Namespace = "http://test.contracts")]
            public class LinkedListContract
            {
                [DataMember(Order = 1)]
                public LinkedList<string> Items { get; set; } = new();
            }
            """;

        var testCode = """
            var original = new TestContracts.LinkedListContract();
            original.Items.AddLast("First");
            original.Items.AddLast("Second");
            original.Items.AddLast("Third");

            return SerializationTestRunner.RunTest(original, typeof(TestContracts.LinkedListContract));
            """;

        var result = await RunSerializationTest(dataContractCode, testCode);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.True(result.XmlEquivalent, "Serialized XMLs should be semantically equivalent");
        Assert.True(result.DeserializedEqual, "Deserialized objects should be equal to originals");
    }

    /// <summary>
    /// Tests SortedDictionary and SortedList serialization.
    /// </summary>
    [Fact]
    public async Task SortedCollections_SerializesEquivalently()
    {
        var dataContractCode = """
            using System;
            using System.Collections.Generic;
            using System.Runtime.Serialization;

            namespace TestContracts;

            [DataContract(Name = "SortedContainer", Namespace = "http://test.contracts")]
            public class SortedContainerContract
            {
                [DataMember(Order = 1)]
                public SortedDictionary<string, int> SortedDict { get; set; } = new();

                [DataMember(Order = 2)]
                public SortedList<int, string> SortedList { get; set; } = new();
            }
            """;

        var testCode = """
            var original = new TestContracts.SortedContainerContract
            {
                SortedDict = new SortedDictionary<string, int>
                {
                    { "Charlie", 3 },
                    { "Alice", 1 },
                    { "Bob", 2 }
                },
                SortedList = new SortedList<int, string>
                {
                    { 3, "Third" },
                    { 1, "First" },
                    { 2, "Second" }
                }
            };

            return SerializationTestRunner.RunTest(original, typeof(TestContracts.SortedContainerContract));
            """;

        var result = await RunSerializationTest(dataContractCode, testCode);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.True(result.XmlEquivalent, "Serialized XMLs should be semantically equivalent");
        Assert.True(result.DeserializedEqual, "Deserialized objects should be equal to originals");
    }

    /// <summary>
    /// Tests IgnoreDataMember attribute.
    /// </summary>
    [Fact]
    public async Task IgnoreDataMember_SerializesEquivalently()
    {
        var dataContractCode = """
            using System;
            using System.Runtime.Serialization;

            namespace TestContracts;

            [DataContract(Name = "WithIgnored", Namespace = "http://test.contracts")]
            public class IgnoreDataMemberContract
            {
                [DataMember(Order = 1)]
                public string IncludedProperty { get; set; } = "";

                [IgnoreDataMember]
                public string IgnoredProperty { get; set; } = "";

                [DataMember(Order = 2)]
                public int IncludedNumber { get; set; }
            }
            """;

        var testCode = """
            var original = new TestContracts.IgnoreDataMemberContract
            {
                IncludedProperty = "Included",
                IgnoredProperty = "This should be ignored",
                IncludedNumber = 42
            };

            return SerializationTestRunner.RunTest(original, typeof(TestContracts.IgnoreDataMemberContract));
            """;

        var result = await RunSerializationTest(dataContractCode, testCode);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.True(result.XmlEquivalent, "Serialized XMLs should be semantically equivalent");
    }

    /// <summary>
    /// Tests interface-based polymorphism with KnownTypes.
    /// </summary>
    [Fact]
    public async Task InterfacePolymorphism_SerializesEquivalently()
    {
        var dataContractCode = """
            using System;
            using System.Collections.Generic;
            using System.Runtime.Serialization;

            namespace TestContracts;

            public interface IMessage
            {
                string Content { get; set; }
            }

            [DataContract(Name = "TextMessage", Namespace = "http://test.contracts")]
            public class TextMessageContract : IMessage
            {
                [DataMember(Order = 1)]
                public string Content { get; set; } = "";
            }

            [DataContract(Name = "ImageMessage", Namespace = "http://test.contracts")]
            public class ImageMessageContract : IMessage
            {
                [DataMember(Order = 1)]
                public string Content { get; set; } = "";

                [DataMember(Order = 2)]
                public string ImageUrl { get; set; } = "";
            }

            [DataContract(Name = "Chat", Namespace = "http://test.contracts")]
            [KnownType(typeof(TextMessageContract))]
            [KnownType(typeof(ImageMessageContract))]
            public class ChatContract
            {
                [DataMember(Order = 1)]
                public string Title { get; set; } = "";

                [DataMember(Order = 2)]
                public List<IMessage> Messages { get; set; } = new();
            }
            """;

        var testCode = """
            var original = new TestContracts.ChatContract
            {
                Title = "Test Chat",
                Messages = new List<TestContracts.IMessage>
                {
                    new TestContracts.TextMessageContract { Content = "Hello!" },
                    new TestContracts.ImageMessageContract { Content = "Check this out", ImageUrl = "http://example.com/image.png" }
                }
            };

            return SerializationTestRunner.RunTest(original, typeof(TestContracts.ChatContract));
            """;

        var result = await RunSerializationTest(dataContractCode, testCode);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.True(result.XmlEquivalent, "Serialized XMLs should be semantically equivalent");
        Assert.True(result.DeserializedEqual, "Deserialized objects should be equal to originals");
    }

    /// <summary>
    /// Tests mixed explicit and default ordering of data members.
    /// </summary>
    [Fact]
    public async Task MixedOrdering_SerializesEquivalently()
    {
        var dataContractCode = """
            using System;
            using System.Runtime.Serialization;

            namespace TestContracts;

            [DataContract(Name = "MixedOrder", Namespace = "http://test.contracts")]
            public class MixedOrderContract
            {
                [DataMember(Order = 10)]
                public string ExplicitlyLast { get; set; } = "";

                [DataMember]
                public string ImplicitB { get; set; } = "";

                [DataMember(Order = 1)]
                public string ExplicitlyFirst { get; set; } = "";

                [DataMember]
                public string ImplicitA { get; set; } = "";

                [DataMember(Order = 5)]
                public string ExplicitlyMiddle { get; set; } = "";
            }
            """;

        var testCode = """
            var original = new TestContracts.MixedOrderContract
            {
                ExplicitlyLast = "Last",
                ImplicitB = "B",
                ExplicitlyFirst = "First",
                ImplicitA = "A",
                ExplicitlyMiddle = "Middle"
            };

            return SerializationTestRunner.RunTest(original, typeof(TestContracts.MixedOrderContract));
            """;

        var result = await RunSerializationTest(dataContractCode, testCode);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.True(result.XmlEquivalent, "Serialized XMLs should be semantically equivalent");
        Assert.True(result.DeserializedEqual, "Deserialized objects should be equal to originals");
    }

    /// <summary>
    /// Tests special XML characters in string values.
    /// </summary>
    [Fact]
    public async Task SpecialXmlCharacters_SerializesEquivalently()
    {
        var dataContractCode = """
            using System;
            using System.Runtime.Serialization;

            namespace TestContracts;

            [DataContract(Name = "XmlChars", Namespace = "http://test.contracts")]
            public class XmlCharsContract
            {
                [DataMember(Order = 1, Name = "Tom & Jerry")]
                public string Ampersand { get; set; } = "";

                [DataMember(Order = 2, Name = "5 < 10")]
                public string LessThan { get; set; } = "";

                [DataMember(Order = 3, Name = "10 > 5")]
                public string GreaterThan { get; set; } = "";

                [DataMember(Order = 4, Name = "He said \"Hello\"")]
                public string Quote { get; set; } = "";

                [DataMember(Order = 5, Name = "<tag>Value & \"quoted\"</tag>")]
                public string Mixed { get; set; } = "";
            }
            """;

        var testCode = """
            var original = new TestContracts.XmlCharsContract
            {
                Ampersand = "Tom & Jerry",
                LessThan = "5 < 10",
                GreaterThan = "10 > 5",
                Quote = "He said \"Hello\"",
                Mixed = "<tag>Value & \"quoted\"</tag>"
            };

            return SerializationTestRunner.RunTest(original, typeof(TestContracts.XmlCharsContract));
            """;

        var result = await RunSerializationTest(dataContractCode, testCode);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.True(result.XmlEquivalent, "Serialized XMLs should be semantically equivalent");
        Assert.True(result.DeserializedEqual, "Deserialized objects should be equal to originals");
    }

    /// <summary>
    /// Tests Unicode string values.
    /// </summary>
    [Fact]
    public async Task UnicodeStrings_SerializesEquivalently()
    {
        var dataContractCode = """
            using System;
            using System.Runtime.Serialization;

            namespace TestContracts;

            [DataContract(Name = "Unicode", Namespace = "http://test.contracts")]
            public class UnicodeContract
            {
                [DataMember(Order = 1, Name = "こんにちは")]
                public string Japanese { get; set; } = "";

                [DataMember(Order = 2, Name = "你好")]
                public string Chinese { get; set; } = "";

                [DataMember(Order = 3, Name = "مرحبا")]
                public string Arabic { get; set; } = "";

                [DataMember(Order = 4, Name = "👋🌍🎉")]
                public string Emoji { get; set; } = "";

                [DataMember(Order = 5, Name = "Hello こんにちは 你好 مرحبا 👋")]
                public string Mixed { get; set; } = "";
            }
            """;

        var testCode = """
            var original = new TestContracts.UnicodeContract
            {
                Japanese = "こんにちは",
                Chinese = "你好",
                Arabic = "مرحبا",
                Emoji = "👋🌍🎉",
                Mixed = "Hello こんにちは 你好 مرحبا 👋"
            };

            return SerializationTestRunner.RunTest(original, typeof(TestContracts.UnicodeContract));
            """;

        var result = await RunSerializationTest(dataContractCode, testCode);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.True(result.XmlEquivalent, "Serialized XMLs should be semantically equivalent");
        Assert.True(result.DeserializedEqual, "Deserialized objects should be equal to originals");
    }

    /// <summary>
    /// Tests extreme numeric values (min, max, special values).
    /// </summary>
    [Fact]
    public async Task ExtremeNumericValues_SerializesEquivalently()
    {
        var dataContractCode = """
            using System;
            using System.Runtime.Serialization;

            namespace TestContracts;

            [DataContract(Name = "ExtremeNumbers", Namespace = "http://test.contracts")]
            public class ExtremeNumericContract
            {
                [DataMember(Order = 1)]
                public int IntMin { get; set; }

                [DataMember(Order = 2)]
                public int IntMax { get; set; }

                [DataMember(Order = 3)]
                public long LongMin { get; set; }

                [DataMember(Order = 4)]
                public long LongMax { get; set; }

                [DataMember(Order = 5)]
                public double DoubleMin { get; set; }

                [DataMember(Order = 6)]
                public double DoubleMax { get; set; }

                [DataMember(Order = 7)]
                public double PositiveInfinity { get; set; }

                [DataMember(Order = 8)]
                public double NegativeInfinity { get; set; }

                [DataMember(Order = 9)]
                public double NaN { get; set; }
            }
            """;

        var testCode = """
            var original = new TestContracts.ExtremeNumericContract
            {
                IntMin = int.MinValue,
                IntMax = int.MaxValue,
                LongMin = long.MinValue,
                LongMax = long.MaxValue,
                DoubleMin = double.MinValue,
                DoubleMax = double.MaxValue,
                PositiveInfinity = double.PositiveInfinity,
                NegativeInfinity = double.NegativeInfinity,
                NaN = double.NaN
            };

            return SerializationTestRunner.RunTest(original, typeof(TestContracts.ExtremeNumericContract));
            """;

        var result = await RunSerializationTest(dataContractCode, testCode);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.True(result.XmlEquivalent, "Serialized XMLs should be semantically equivalent");
        Assert.True(result.DeserializedEqual, "Deserialized objects should be equal to originals");
    }

    /// <summary>
    /// Tests null collection properties.
    /// </summary>
    [Fact]
    public async Task NullCollections_SerializesEquivalently()
    {
        var dataContractCode = """
            using System;
            using System.Collections.Generic;
            using System.Runtime.Serialization;

            namespace TestContracts;

            [DataContract(Name = "NullCollections", Namespace = "http://test.contracts")]
            public class NullCollectionsContract
            {
                [DataMember(Order = 1)]
                public List<string>? NullableList { get; set; }

                [DataMember(Order = 2)]
                public Dictionary<string, int>? NullableDictionary { get; set; }

                [DataMember(Order = 3)]
                public int[]? NullableArray { get; set; }
            }
            """;

        var testCode = """
            var original = new TestContracts.NullCollectionsContract
            {
                NullableList = null,
                NullableDictionary = null,
                NullableArray = null
            };

            return SerializationTestRunner.RunTest(original, typeof(TestContracts.NullCollectionsContract));
            """;

        var result = await RunSerializationTest(dataContractCode, testCode);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.True(result.XmlEquivalent, "Serialized XMLs should be semantically equivalent");
        Assert.True(result.DeserializedEqual, "Deserialized objects should be equal to originals");
    }

    /// <summary>
    /// Tests record type serialization (C# 9+).
    /// </summary>
    [Fact]
    public async Task RecordType_SerializesEquivalently()
    {
        var dataContractCode = """
            using System;
            using System.Runtime.Serialization;

            namespace TestContracts;

            [DataContract(Name = "PersonRecord", Namespace = "http://test.contracts")]
            public record PersonRecordContract
            {
                [DataMember(Order = 1)]
                public string FirstName { get; init; } = "";

                [DataMember(Order = 2)]
                public string LastName { get; init; } = "";

                [DataMember(Order = 3)]
                public int Age { get; init; }
            }
            """;

        var testCode = """
            var original = new TestContracts.PersonRecordContract
            {
                FirstName = "John",
                LastName = "Doe",
                Age = 30
            };

            return SerializationTestRunner.RunTest(original, typeof(TestContracts.PersonRecordContract));
            """;

        var result = await RunSerializationTest(dataContractCode, testCode);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.True(result.XmlEquivalent, "Serialized XMLs should be semantically equivalent");
        Assert.True(result.DeserializedEqual, "Deserialized objects should be equal to originals");
    }

    /// <summary>
    /// Tests record struct serialization (C# 10+).
    /// </summary>
    [Fact]
    public async Task RecordStruct_SerializesEquivalently()
    {
        var dataContractCode = """
            using System;
            using System.Runtime.Serialization;

            namespace TestContracts;

            [DataContract(Name = "PointRecord", Namespace = "http://test.contracts")]
            public record struct PointRecordContract
            {
                [DataMember(Order = 1)]
                public double X { get; init; }

                [DataMember(Order = 2)]
                public double Y { get; init; }
            }

            [DataContract(Name = "LineRecord", Namespace = "http://test.contracts")]
            public class LineRecordContract
            {
                [DataMember(Order = 1)]
                public PointRecordContract Start { get; set; }

                [DataMember(Order = 2)]
                public PointRecordContract End { get; set; }
            }
            """;

        var testCode = """
            var original = new TestContracts.LineRecordContract
            {
                Start = new TestContracts.PointRecordContract { X = 1.0, Y = 2.0 },
                End = new TestContracts.PointRecordContract { X = 3.0, Y = 4.0 }
            };

            return SerializationTestRunner.RunTest(original, typeof(TestContracts.LineRecordContract));
            """;

        var result = await RunSerializationTest(dataContractCode, testCode);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.True(result.XmlEquivalent, "Serialized XMLs should be semantically equivalent");
        Assert.True(result.DeserializedEqual, "Deserialized objects should be equal to originals");
    }

    /// <summary>
    /// Tests init-only properties.
    /// </summary>
    [Fact]
    public async Task InitOnlyProperties_SerializesEquivalently()
    {
        var dataContractCode = """
            using System;
            using System.Runtime.Serialization;

            namespace TestContracts;

            [DataContract(Name = "InitOnly", Namespace = "http://test.contracts")]
            public class InitOnlyContract
            {
                [DataMember(Order = 1)]
                public string Id { get; init; } = "";

                [DataMember(Order = 2)]
                public DateTime CreatedAt { get; init; }

                [DataMember(Order = 3)]
                public string? Description { get; init; }
            }
            """;

        var testCode = """
            var original = new TestContracts.InitOnlyContract
            {
                Id = "init-001",
                CreatedAt = new DateTime(2024, 6, 15),
                Description = "Test Description"
            };

            return SerializationTestRunner.RunTest(original, typeof(TestContracts.InitOnlyContract));
            """;

        var result = await RunSerializationTest(dataContractCode, testCode);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.True(result.XmlEquivalent, "Serialized XMLs should be semantically equivalent");
        Assert.True(result.DeserializedEqual, "Deserialized objects should be equal to originals");
    }
    
    
    /// <summary>
    /// Tests generic type contract.
    /// </summary>
    [Fact]
    public async Task GenericType_SerializesEquivalently()
    {
        var dataContractCode = """
            using System;
            using System.Runtime.Serialization;

            namespace TestContracts;

            [DataContract(Name = "Generic", Namespace = "http://test.contracts")]
            [KnownType(typeof(GenericContract<int>))]
            public class GenericContract<TValue>
                where TValue: struct
            {
                [DataMember(Order = 1)]
                public TValue Id { get; init; }

                [DataMember(Order = 2)]
                private TValue CreatedAt;

                [DataMember(Order = 3)]
                public string? Description { get; init; }
                
                public TValue CreatedAtSetter
                {
                    get => CreatedAt;
                    set => CreatedAt = value;
                }
            }
            """;

        var testCode = """
            var original = new TestContracts.GenericContract<int>
            {
                Id = 5554,
                CreatedAtSetter = 20240615,
                Description = "Test Description"
            };

            return SerializationTestRunner.RunTest(original, typeof(TestContracts.GenericContract<int>));
            """;

        var result = await RunSerializationTest(dataContractCode, testCode);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.True(result.XmlEquivalent, "Serialized XMLs should be semantically equivalent");
        Assert.True(result.DeserializedEqual, "Deserialized objects should be equal to originals");
    }

}