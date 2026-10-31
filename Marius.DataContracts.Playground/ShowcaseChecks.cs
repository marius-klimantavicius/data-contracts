using System.Runtime.Serialization;
using System.Text;
using Marius.DataContracts.Runtime;
using FrameworkSerializer = System.Runtime.Serialization.DataContractSerializer;
using GeneratedSerializer = Marius.DataContracts.Runtime.DataContractSerializer;

namespace Marius.DataContracts.Playground;

internal sealed class ShowcaseChecks
{
    private readonly bool _showXml;

    public ShowcaseChecks(bool showXml)
    {
        _showXml = showXml;
    }

    public int Passed { get; private set; }
    public int Failed { get; private set; }

    public void Run(string title, string description, Action action)
    {
        Console.WriteLine($"\n{title}\n  {description}");
        try
        {
            action();
            Passed++;
            Console.WriteLine("  PASS");
        }
        catch (Exception exception)
        {
            Failed++;
            Console.Error.WriteLine($"  FAIL: {exception}");
        }
    }

    // State assertions deliberately inspect contract members and ReferenceEquals;
    // user Equals methods or a public-property-only comparer can hide data loss.
    public string Parity<T>(T value,
        Action<T> assertState,
        DataContractSerializerSettings? settings = null,
        ISerializationSurrogateProvider? surrogate = null)
    {
        var frameworkXml = Write(false, value, settings, surrogate);
        var generatedXml = Write(true, value, settings, surrogate);
        CompareXml(frameworkXml, generatedXml);
        foreach (var xml in new[] { frameworkXml, generatedXml })
        {
            assertState(Read<T>(false, xml, settings, surrogate));
            assertState(Read<T>(true, xml, settings, surrogate));
        }

        return generatedXml;
    }

    public void CompareXml(string frameworkXml, string generatedXml)
    {
        Check(XmlEquivalence.AreEqual(frameworkXml, generatedXml), $"XML mismatch.\n.NET: {frameworkXml}\nGenerated: {generatedXml}");
        if (_showXml)
        {
            Console.WriteLine($"  .NET XML:      {frameworkXml}");
            Console.WriteLine($"  Generated XML: {generatedXml}");
        }
    }

    public static string Write<T>(bool generated,
        T value,
        DataContractSerializerSettings? settings = null,
        ISerializationSurrogateProvider? surrogate = null)
    {
        using var stream = new MemoryStream();
        if (generated)
        {
            var serializer = new GeneratedSerializer(DataContractContext.DataContractProvider, typeof(T), settings);
            serializer.SetSerializationSurrogateProvider(surrogate);
            serializer.WriteObject(stream, value);
        }
        else
        {
            var serializer = new FrameworkSerializer(typeof(T), settings ?? new DataContractSerializerSettings());
            serializer.SetSerializationSurrogateProvider(surrogate);
            serializer.WriteObject(stream, value);
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    public static T Read<T>(bool generated,
        string xml,
        DataContractSerializerSettings? settings = null,
        ISerializationSurrogateProvider? surrogate = null)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(xml));
        if (generated)
        {
            var serializer = new GeneratedSerializer(DataContractContext.DataContractProvider, typeof(T), settings);
            serializer.SetSerializationSurrogateProvider(surrogate);
            return (T)serializer.ReadObject(stream)!;
        }

        var framework = new FrameworkSerializer(typeof(T), settings ?? new DataContractSerializerSettings());
        framework.SetSerializationSurrogateProvider(surrogate);
        return (T)framework.ReadObject(stream)!;
    }

    public static TException Throws<TException>(Action action) where TException : Exception
    {
        try
        {
            action();
        }
        catch (TException exception)
        {
            return exception;
        }

        throw new InvalidOperationException($"Expected {typeof(TException).Name}; operation succeeded.");
    }

    public static void Check(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}