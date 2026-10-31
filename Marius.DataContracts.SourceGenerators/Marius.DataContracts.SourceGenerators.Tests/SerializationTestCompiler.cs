using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Threading.Tasks;
using Basic.Reference.Assemblies;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;

namespace Marius.DataContracts.SourceGenerators.Tests;

/// <summary>
/// Compiles and runs serialization equivalence tests dynamically.
/// </summary>
public class SerializationTestCompiler
{
    /// <summary>
    /// Compiles the data contract code with the source generator and executes the test.
    /// </summary>
    public async Task<SerializationTestResult> CompileAndRunAsync(string dataContractCode, string testCode)
    {
        var generatedText = default(string?);
        try
        {
            (var assembly, var errors, generatedText) = await CompileWithSourceGeneratorAsync(dataContractCode, testCode);
            if (assembly == null)
                return SerializationTestResult.Failure($"Compilation failed:\n{string.Join("\n", errors)}");

            // Execute the test
            return ExecuteTest(assembly) with
            {
                GeneratedText = generatedText,
            };
        }
        catch (Exception ex)
        {
            return SerializationTestResult.Failure($"Test execution failed: {ex.Message}\n{ex.StackTrace}") with
            {
                GeneratedText = generatedText,
            };
        }
    }

    private const string InfrastructureCode =
        """
        using System;
        using System.IO;
        using System.Linq;
        using System.Collections;
        using System.Collections.Generic;
        using System.Xml;
        using System.Xml.Linq;
        using Marius.DataContracts.Runtime;

        public static class SerializationTestRunner
        {
            public static SerializationTestResultData RunTest<T>(T original, Type contractType, System.Runtime.Serialization.DataContractSerializerSettings? settings = null) where T : class
            {
                string netXml = null;
                string customXml = null;
                bool xmlEquivalent = false;
                try
                {
                    // Create serializers
                    var netSerializer = new System.Runtime.Serialization.DataContractSerializer(contractType, settings);
                    var customProvider = new DataContractProvider(DataContractContext.DataContracts, DataContractContext.TypeDataContracts);
                    var customSerializer = new Marius.DataContracts.Runtime.DataContractSerializer(customProvider, contractType, settings);

                    // Serialize with .NET serializer
                    using (var netStream = new MemoryStream())
                    {
                        netSerializer.WriteObject(netStream, original);
                        netStream.Position = 0;
                        using var reader = new StreamReader(netStream);
                        netXml = reader.ReadToEnd();
                    }
        
                    // Serialize with custom serializer
                    using (var customStream = new MemoryStream())
                    {
                        customSerializer.WriteObject(customStream, original);
                        customStream.Position = 0;
                        using var reader = new StreamReader(customStream);
                        customXml = reader.ReadToEnd();
                    }
        
                    // Compare XMLs semantically
                    var netElement = XElement.Parse(netXml, LoadOptions.PreserveWhitespace);
                    var customElement = XElement.Parse(customXml, LoadOptions.PreserveWhitespace);
                    xmlEquivalent = AreXmlSemanticallyEquivalent(netElement, customElement);
        
                    // Deserialize with custom serializer from .NET XML
                    T? customDeserialized;
                    using (var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(netXml)))
                    {
                        customDeserialized = (T?)customSerializer.ReadObject(stream);
                    }
        
                    // Deserialize with .NET serializer from custom XML
                    T? netDeserialized;
                    using (var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(customXml)))
                    {
                        netDeserialized = (T?)netSerializer.ReadObject(stream);
                    }
        
                    // Use the framework deserialization of each XML as the oracle. An input's
                    // noncontract state and concrete collection types need not survive serialization.
                    T? netFromNet;
                    T? customFromCustom;
                    using (var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(netXml)))
                        netFromNet = (T?)netSerializer.ReadObject(stream);

                    using (var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(customXml)))
                        customFromCustom = (T?)customSerializer.ReadObject(stream);

                    var preserve = settings?.PreserveObjectReferences == true;
                    var sameNetXml = new GraphComparison(preserve).Compare(netFromNet, customDeserialized);
                    var sameCustomXml = new GraphComparison(preserve).Compare(netDeserialized, customFromCustom);
                    var crossXml = new GraphComparison(preserve).Compare(netFromNet, netDeserialized);
                    var deserializedEqual = sameNetXml && sameCustomXml && crossXml;
                    var details = $"Same .NET XML state: {sameNetXml}\n" +
                                  $"Same custom XML state: {sameCustomXml}\n" +
                                  $"Framework state across XMLs: {crossXml}";

                    return new SerializationTestResultData
                    {
                        Success = true,
                        XmlEquivalent = xmlEquivalent,
                        DeserializedEqual = deserializedEqual,
                        NetXml = netXml,
                        CustomXml = customXml,
                        Details = details,
                    };
                }
                catch (Exception ex)
                {
                    return new SerializationTestResultData
                    {
                        Success = false,
                        ErrorMessage = ex.ToString(),
                        NetXml = netXml,
                        CustomXml = customXml,
                        XmlEquivalent = xmlEquivalent,
                    };
                }
            }
        
            private static bool AreXmlSemanticallyEquivalent(XElement left, XElement right)
            {
                if (left.Name != right.Name)
                    return false;

                var leftAttributes = left.Attributes().Where(a => !a.IsNamespaceDeclaration).ToList();
                var rightAttributes = right.Attributes().Where(a => !a.IsNamespaceDeclaration).ToList();
                if (leftAttributes.Count != rightAttributes.Count)
                    return false;
                
                XName xsiType = "{http://www.w3.org/2001/XMLSchema-instance}type";
                foreach (var attribute in leftAttributes)
                {
                    var other = right.Attribute(attribute.Name);
                    if (other == null) return false;
                    if (attribute.Name == xsiType)
                    {
                        if (ExpandedName(left, attribute.Value) != ExpandedName(right, other.Value))
                            return false;
                    }
                    else
                    {
                        if (attribute.Value != other.Value)
                            return false;
                    }
                }

                // Preserve all text, including significant spaces and text between children.
                // Adjacent CDATA/text nodes have the same XML character content.
                var leftNodes = Content(left).ToList();
                var rightNodes = Content(right).ToList();
                if (leftNodes.Count != rightNodes.Count) return false;
                for (int i = 0; i < leftNodes.Count; i++)
                {
                    if (leftNodes[i] is XElement le && rightNodes[i] is XElement re)
                    {
                        if (!AreXmlSemanticallyEquivalent(le, re))
                            return false;
                    }
                    else if (leftNodes[i] is string lt && rightNodes[i] is string rt)
                    {
                        if (lt != rt)
                            return false;
                    }
                    else
                    {
                        return false;
                    }
                }
                return true;
            }

            private static XName ExpandedName(XElement element, string value)
            {
                value = value.Trim();
                var parts = value.Split(':');
                if (parts.Length == 1) return element.GetDefaultNamespace() + parts[0];
                if (parts.Length != 2 || element.GetNamespaceOfPrefix(parts[0]) is not XNamespace ns)
                    throw new XmlException("Unbound or invalid QName: " + value);
                return ns + parts[1];
            }

            private static IEnumerable<object> Content(XElement element)
            {
                string text = "";
                foreach (var node in element.Nodes())
                {
                    if (node is XText t) text += t.Value;
                    else if (node is XElement child)
                    {
                        if (text.Length != 0)
                        {
                            yield return text;
                            text = "";
                        }

                        yield return child;
                    }
                }

                if (text.Length != 0)
                    yield return text;
            }

            private static bool ObjectsAreEqual(object? left, object? right) => new GraphComparison(true).Compare(left, right);

            private sealed class GraphComparison
            {
                private readonly bool preserveReferences;
                private readonly Dictionary<object, object> leftToRight = new(ReferenceEqualityComparer.Instance);
                private readonly Dictionary<object, object> rightToLeft = new(ReferenceEqualityComparer.Instance);
                private readonly Dictionary<object, HashSet<object>> visited = new(ReferenceEqualityComparer.Instance);
                public GraphComparison(bool preserveReferences) { this.preserveReferences = preserveReferences; }

                public bool Compare(object? left, object? right)
                {
                    if (left is null || right is null)
                        return left is null && right is null;

                    var type = left.GetType();
                    if (type != right.GetType())
                        return false;
                    
                    var contract = (System.Runtime.Serialization.DataContractAttribute?)Attribute.GetCustomAttribute(type, typeof(System.Runtime.Serialization.DataContractAttribute), false);
                    if (!type.IsValueType)
                    {
                        if (preserveReferences || contract?.IsReference == true)
                        {
                            if (leftToRight.TryGetValue(left, out var mapped)) return ReferenceEquals(mapped, right);
                            if (rightToLeft.ContainsKey(right)) return false;
                            leftToRight.Add(left, right);
                            rightToLeft.Add(right, left);
                        }
                        
                        if (!visited.TryGetValue(left, out var matches))
                            visited.Add(left, matches = new HashSet<object>(ReferenceEqualityComparer.Instance));
                        
                        if (!matches.Add(right))
                            return true;
                    }
                    
                    if (left is DateTime leftDate && right is DateTime rightDate)
                        return leftDate.Ticks == rightDate.Ticks && leftDate.Kind == rightDate.Kind;

                    if (type.IsPrimitive || type.IsEnum || left is string || left is decimal ||
                        left is DateTimeOffset || left is Guid || left is TimeSpan ||
                        left is Uri || left is XmlQualifiedName)
                    {
                        return left.Equals(right);
                    }

                    if (left is Array la && right is Array ra)
                    {
                        if (la.Rank != ra.Rank) return false;
                        for (int i = 0; i < la.Rank; i++)
                        {
                            if (la.GetLength(i) != ra.GetLength(i) || la.GetLowerBound(i) != ra.GetLowerBound(i))
                                return false;
                        }
                    }

                    if (left is IEnumerable first && right is IEnumerable second)
                    {
                        var a = first.GetEnumerator();
                        var b = second.GetEnumerator();
                        try
                        {
                            while (a.MoveNext())
                                if (!b.MoveNext() || !Compare(a.Current, b.Current)) return false;

                            return !b.MoveNext();
                        }
                        finally
                        {
                            (a as IDisposable)?.Dispose(); (b as IDisposable)?.Dispose();
                        }
                    }

                    // Walk declarations individually so hidden base members and private
                    // DataMembers are examined. Never delegate state checks to user equality.
                    for (var declaration = type; declaration != null && declaration != typeof(object); declaration = declaration.BaseType)
                    {
                        bool attributed = Attribute.IsDefined(declaration, typeof(System.Runtime.Serialization.DataContractAttribute), false);
                        bool serializable = Attribute.IsDefined(declaration, typeof(SerializableAttribute), false);
                        const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.DeclaredOnly;
                        
                        foreach (var field in declaration.GetFields(flags))
                        {
                            bool included = attributed ? Attribute.IsDefined(field, typeof(System.Runtime.Serialization.DataMemberAttribute)) :
                                serializable ? !Attribute.IsDefined(field, typeof(NonSerializedAttribute)) :
                                field.IsPublic && !Attribute.IsDefined(field, typeof(System.Runtime.Serialization.IgnoreDataMemberAttribute));
                            
                            if (included && !Compare(field.GetValue(left), field.GetValue(right)))
                                return false;
                        }

                        if (serializable && !attributed)
                            continue;

                        foreach (var property in declaration.GetProperties(flags))
                        {
                            if (property.GetIndexParameters().Length != 0 || property.GetMethod == null)
                                continue;

                            bool included = attributed ? Attribute.IsDefined(property, typeof(System.Runtime.Serialization.DataMemberAttribute)) :
                                property.GetMethod.IsPublic && !Attribute.IsDefined(property, typeof(System.Runtime.Serialization.IgnoreDataMemberAttribute)) &&
                                (property.SetMethod?.IsPublic == true || typeof(IEnumerable).IsAssignableFrom(property.PropertyType));

                            if (included && !Compare(property.GetValue(left), property.GetValue(right)))
                                return false;
                        }
                    }

                    return true;
                }
            }
        }
        
        public class SerializationTestResultData
        {
            public bool Success { get; set; }
            public string? ErrorMessage { get; set; }
            public bool XmlEquivalent { get; set; }
            public bool DeserializedEqual { get; set; }
            public string? NetXml { get; set; }
            public string? CustomXml { get; set; }
            public string? Details { get; set; }
        }
        """;

    private string BuildDataContractCode(string dataContractCode)
    {
        return 
            $$"""
            #nullable enable
            using System;
            using System.IO;
            using System.Linq;
            using System.Collections;
            using System.Collections.Generic;
            using System.Xml;
            using System.Xml.Linq;
            using System.Runtime.Serialization;

            {{dataContractCode}}
            """;
    }

    private string BuildTestCode(string testCode)
    {
        return 
            $$"""
            #nullable enable
            using System;
            using System.IO;
            using System.Linq;
            using System.Collections;
            using System.Collections.Generic;
            using System.Xml;
            using System.Xml.Linq;
            using Marius.DataContracts.Runtime;

            public static class TestEntryPoint
            {
                public static SerializationTestResultData Run()
                {
                    {{testCode}}
                }
            }
            """;
    }

    private async Task<(Assembly? Assembly, List<string> Errors, string? generatedText)> CompileWithSourceGeneratorAsync(string dataContractCode, string testCode)
    {
        var errors = new List<string>();

        var infrastructureTree = CSharpSyntaxTree.ParseText(InfrastructureCode, new CSharpParseOptions(LanguageVersion.Latest));
        var dataContractTree = CSharpSyntaxTree.ParseText(BuildDataContractCode(dataContractCode), new CSharpParseOptions(LanguageVersion.Latest));
        var testTree = CSharpSyntaxTree.ParseText(BuildTestCode(testCode), new CSharpParseOptions(LanguageVersion.Latest));

        var compilation = CSharpCompilation.Create(
            $"TestAssembly_{Guid.NewGuid():N}",
            new[] { infrastructureTree, dataContractTree, testTree },
#if NET9_0
            Net90.References.All
#else
            Net100.References.All
#endif
                .Add(MetadataReference.CreateFromFile(typeof(Runtime.DataContractSerializer).Assembly.Location))
            ,
            new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                allowUnsafe: true,
                optimizationLevel: OptimizationLevel.Debug,
                nullableContextOptions: NullableContextOptions.Enable));

        // Run the source generator
        var generator = new DataContractGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);
        driver = (CSharpGeneratorDriver)driver.RunGeneratorsAndUpdateCompilation(
            compilation,
            out var outputCompilation,
            out var diagnostics);

        var runResults = driver.GetRunResult();
        var generatedFile = runResults.GeneratedTrees.FirstOrDefault();
        var generatedText = (await generatedFile!.GetTextAsync()).ToString();

        // Check for generator errors
        foreach (var diagnostic in diagnostics)
        {
            if (diagnostic.Severity == DiagnosticSeverity.Error)
                errors.Add($"Generator: {diagnostic.GetMessage()}");
        }

        // Check for compilation errors
        var compileDiagnostics = outputCompilation.GetDiagnostics();
        foreach (var diagnostic in compileDiagnostics)
        {
            if (diagnostic.Severity == DiagnosticSeverity.Error)
                errors.Add($"Compile: {diagnostic.GetMessage()} at {diagnostic.Location}");
        }

        if (errors.Count > 0)
            return (null, errors, generatedText);

        // Emit assembly
        using var ms = new MemoryStream();
        var result = outputCompilation.Emit(ms, options: new EmitOptions(debugInformationFormat: DebugInformationFormat.Embedded));

        if (!result.Success)
        {
            foreach (var diagnostic in result.Diagnostics)
            {
                if (diagnostic.Severity == DiagnosticSeverity.Error)
                    errors.Add($"Emit: {diagnostic.GetMessage()}");
            }

            return (null, errors, generatedText);
        }

        ms.Seek(0, SeekOrigin.Begin);

        // Load assembly in a separate context with the runtime dependency
        var loadContext = new TestAssemblyLoadContext($"TestContext_{Guid.NewGuid():N}");
        var assembly = loadContext.LoadFromStream(ms);

        return (assembly, errors, generatedText);
    }

    private SerializationTestResult ExecuteTest(Assembly assembly)
    {
        try
        {
            var entryPointType = assembly.GetType("TestEntryPoint");
            if (entryPointType == null)
                return SerializationTestResult.Failure("TestEntryPoint type not found in compiled assembly");

            var runMethod = entryPointType.GetMethod("Run", BindingFlags.Static | BindingFlags.Public);
            if (runMethod == null)
                return SerializationTestResult.Failure("Run method not found in TestEntryPoint");

            var result = runMethod.Invoke(null, null);
            if (result == null)
                return SerializationTestResult.Failure("Test returned null result");

            // Extract result data using reflection (since types are different)
            var resultType = result.GetType();
            var success = (bool)resultType.GetProperty("Success")!.GetValue(result)!;
            var errorMessage = (string?)resultType.GetProperty("ErrorMessage")?.GetValue(result);
            var xmlEquivalent = (bool)resultType.GetProperty("XmlEquivalent")!.GetValue(result)!;
            var deserializedEqual = (bool)resultType.GetProperty("DeserializedEqual")!.GetValue(result)!;
            var netXml = (string?)resultType.GetProperty("NetXml")?.GetValue(result);
            var customXml = (string?)resultType.GetProperty("CustomXml")?.GetValue(result);
            var details = (string?)resultType.GetProperty("Details")?.GetValue(result);

            if (!success)
            {
                return SerializationTestResult.Failure(
                    errorMessage ?? "Unknown error",
                    xmlEquivalent,
                    deserializedEqual,
                    netXml ?? "",
                    customXml ?? "",
                    details);
            }

            return SerializationTestResult.FromTestOutput(
                xmlEquivalent,
                deserializedEqual,
                netXml ?? "",
                customXml ?? "",
                details);
        }
        catch (TargetInvocationException ex)
        {
            return SerializationTestResult.Failure($"Test execution threw exception: {ex.InnerException?.Message ?? ex.Message}\n{ex.InnerException?.StackTrace ?? ex.StackTrace}");
        }
        catch (Exception ex)
        {
            return SerializationTestResult.Failure($"Test execution failed: {ex.Message}\n{ex.StackTrace}");
        }
    }

    /// <summary>
    /// Custom AssemblyLoadContext that can resolve the Marius.DataContracts.Runtime assembly.
    /// </summary>
    private class TestAssemblyLoadContext : AssemblyLoadContext
    {
        public TestAssemblyLoadContext(string name) : base(name, isCollectible: true)
        {
        }

        protected override Assembly? Load(AssemblyName assemblyName)
        {
            // First try to load from the default context
            try
            {
                return Default.LoadFromAssemblyName(assemblyName);
            }
            catch
            {
                return null;
            }
        }
    }
}