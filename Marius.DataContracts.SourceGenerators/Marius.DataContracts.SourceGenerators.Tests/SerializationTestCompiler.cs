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
            public static SerializationTestResultData RunTest<T>(T original, Type contractType) where T : class
            {
                string netXml = null;
                string customXml = null;
                bool xmlEquivalent = false;
                try
                {
                    // Create serializers
                    var netSerializer = new System.Runtime.Serialization.DataContractSerializer(contractType);
                    var customProvider = new DataContractProvider(DataContractContext.DataContracts, DataContractContext.TypeDataContracts);
                    var customSerializer = new Marius.DataContracts.Runtime.DataContractSerializer(customProvider, contractType);

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
                    var netElement = XElement.Parse(netXml);
                    var customElement = XElement.Parse(customXml);
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
        
                    // Compare deserialized objects
                    var customEqualsOriginal = ObjectsAreEqual(original, customDeserialized);
                    var netEqualsOriginal = ObjectsAreEqual(original, netDeserialized);
                    var deserializedEqual = customEqualsOriginal && netEqualsOriginal;
        
                    var details = $"Custom deserializer equals original: {customEqualsOriginal}\n" +
                                  $".NET deserializer equals original: {netEqualsOriginal}";
        
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
        
            private static bool AreXmlSemanticallyEquivalent(XElement elem1, XElement elem2)
            {
                // Compare element names (includes namespace)
                if (elem1.Name != elem2.Name)
                    return false;
        
                // Compare attributes (ignoring namespace declarations and order)
                var attrs1 = elem1.Attributes()
                    .Where(a => !a.IsNamespaceDeclaration)
                    .OrderBy(a => a.Name.NamespaceName)
                    .ThenBy(a => a.Name.LocalName)
                    .ToList();
                var attrs2 = elem2.Attributes()
                    .Where(a => !a.IsNamespaceDeclaration)
                    .OrderBy(a => a.Name.NamespaceName)
                    .ThenBy(a => a.Name.LocalName)
                    .ToList();
        
                if (attrs1.Count != attrs2.Count)
                    return false;
        
                for (int i = 0; i < attrs1.Count; i++)
                {
                    if (attrs1[i].Name != attrs2[i].Name || attrs1[i].Value != attrs2[i].Value)
                        return false;
                }
        
                // Compare child elements
                var elements1 = elem1.Elements().ToList();
                var elements2 = elem2.Elements().ToList();
        
                if (elements1.Count == 0 && elements2.Count == 0)
                {
                    // Compare text content
                    return elem1.Value.Trim() == elem2.Value.Trim();
                }
        
                if (elements1.Count != elements2.Count)
                    return false;
        
                for (int i = 0; i < elements1.Count; i++)
                {
                    if (!AreXmlSemanticallyEquivalent(elements1[i], elements2[i]))
                        return false;
                }
        
                return true;
            }
        
            private static bool ObjectsAreEqual(object? obj1, object? obj2)
            {
                if (obj1 == null && obj2 == null) return true;
                if (obj1 == null || obj2 == null) return false;
        
                var type = obj1.GetType();
                if (type != obj2.GetType()) return false;
        
                // For primitive types and strings
                if (type.IsPrimitive || type == typeof(string) || type == typeof(decimal) ||
                    type == typeof(DateTime) || type == typeof(Guid) || type == typeof(TimeSpan))
                {
                    return obj1.Equals(obj2);
                }
        
                // For byte arrays
                if (type == typeof(byte[]))
                {
                    var arr1 = (byte[])obj1;
                    var arr2 = (byte[])obj2;
                    return arr1.SequenceEqual(arr2);
                }
        
                // For enums
                if (type.IsEnum)
                {
                    return obj1.Equals(obj2);
                }
        
                // Check for IEquatable<T>
                var equatableInterface = type.GetInterfaces()
                    .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEquatable<>));
                if (equatableInterface != null)
                {
                    var equalsMethod = equatableInterface.GetMethod("Equals");
                    if (equalsMethod != null)
                    {
                        var result = equalsMethod.Invoke(obj1, new[] { obj2 });
                        if (result is bool boolResult)
                            return boolResult;
                    }
                }
        
                // Check for IComparable<T>
                var comparableInterface = type.GetInterfaces()
                    .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IComparable<>));
                if (comparableInterface != null)
                {
                    var compareMethod = comparableInterface.GetMethod("CompareTo");
                    if (compareMethod != null)
                    {
                        var result = compareMethod.Invoke(obj1, new[] { obj2 });
                        if (result is int intResult)
                            return intResult == 0;
                    }
                }
        
                // Check for non-generic IComparable
                if (obj1 is IComparable comparable1)
                {
                    return comparable1.CompareTo(obj2) == 0;
                }
        
                // For arrays
                if (type.IsArray)
                {
                    var arr1 = (Array)obj1;
                    var arr2 = (Array)obj2;
                    if (arr1.Length != arr2.Length) return false;
                    for (int i = 0; i < arr1.Length; i++)
                    {
                        if (!ObjectsAreEqual(arr1.GetValue(i), arr2.GetValue(i)))
                            return false;
                    }
                    return true;
                }
        
                // For collections (IEnumerable) - but not strings which are also IEnumerable
                if (obj1 is IEnumerable enum1 && obj2 is IEnumerable enum2 && type != typeof(string))
                {
                    var list1 = enum1.Cast<object>().ToList();
                    var list2 = enum2.Cast<object>().ToList();
                    if (list1.Count != list2.Count) return false;
                    for (int i = 0; i < list1.Count; i++)
                    {
                        if (!ObjectsAreEqual(list1[i], list2[i]))
                            return false;
                    }
                    return true;
                }
        
                // For complex types, compare all public properties
                foreach (var prop in type.GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
                {
                    if (!prop.CanRead) continue;
                    var val1 = prop.GetValue(obj1);
                    var val2 = prop.GetValue(obj2);
                    if (!ObjectsAreEqual(val1, val2))
                        return false;
                }
        
                return true;
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
        return $$"""
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
        return $$"""
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
            Net80.References.All
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