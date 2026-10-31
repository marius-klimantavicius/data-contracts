using System.Threading.Tasks;
using Xunit;

namespace Marius.DataContracts.SourceGenerators.Tests;

public class ReviewIdentifierTests
{
    [Fact]
    public Task KeywordMembers_KeepXmlNamesSeparateFromCSharpIdentifiers() => ReviewRegression.AssertAsync(
        """
        namespace @namespace {
            [DataContract] public enum @enum { [EnumMember] @event = 1 }
            [DataContract] public class @class {
                [DataMember(Name = "event-name")] public int @event { get; set; }
                [DataMember] public string @namespace;
                [DataMember] public @enum @record;
                [DataMember] private readonly int @private = 9;
                public int Secret => @private;
            }
        }
        """,
        """
        var result = ReviewOracle.Parity(new @namespace.@class { @event = 5, @namespace = "text", @record = @namespace.@enum.@event }, value => {
            ReviewOracle.Check(value.@event == 5 && value.@namespace == "text", "Keyword property/field");
            ReviewOracle.Check(value.@record == @namespace.@enum.@event && value.Secret == 9, "Enum/private field");
        });
        var xml = XElement.Parse(result.CustomXml!);
        ReviewOracle.Check(xml.Elements().Any(e => e.Name.LocalName == "event-name"), "DataMember.Name must stay an XML name");
        ReviewOracle.Check(xml.Elements().Any(e => e.Name.LocalName == "namespace"), "No C# @ prefix in XML");
        ReviewOracle.Check(xml.Elements().Single(e => e.Name.LocalName == "record").Value == "event", "No C# @ prefix in enum XML");
        return result;
        """);

    [Fact]
    public Task KeywordGenericParameters_CompileUnsafeAccessorsAndConstraints() => ReviewRegression.AssertAsync(
        """
        [DataContract] public class Payload { [DataMember] public int Number; }
        [DataContract] public class Box<@class> where @class : class, new() {
            [DataMember] private readonly @class @event;
            public Box(@class @event) { this.@event = @event; }
            public @class Read() => @event;
        }
        [DataContract] public class Root { [DataMember] public Box<Payload> Box; }
        """,
        """
        return ReviewOracle.Parity(new Root { Box = new Box<Payload>(new Payload { Number = 7 }) }, value =>
            ReviewOracle.Check(value.Box.Read().Number == 7, "Keyword generic accessor must survive both interoperability directions"));
        """);

    [Fact]
    public void KeywordSchemaProviderMethod_Compiles()
    {
        var (output, _) = ReviewGeneratorDriver.Generate("""
            using System.Runtime.Serialization;
            [DataContract] public class Root { [DataMember] public XmlValue Value; }
            [System.Xml.Serialization.XmlSchemaProvider("event")]
            public class XmlValue : System.Xml.Serialization.IXmlSerializable {
                public System.Xml.Schema.XmlSchema GetSchema() => null;
                public void ReadXml(System.Xml.XmlReader reader) { reader.Skip(); }
                public void WriteXml(System.Xml.XmlWriter writer) { }
                public static System.Xml.XmlQualifiedName @event(System.Xml.Schema.XmlSchemaSet @namespace)
                    => new System.Xml.XmlQualifiedName("int", "http://www.w3.org/2001/XMLSchema");
            }
            """);
        ReviewGeneratorDriver.AssertCompiles(output);
    }
}
