using System.Threading.Tasks;
using Xunit;

namespace Marius.DataContracts.SourceGenerators.Tests;

internal static class ReviewRegression
{
    // Assertions run inside the compiled assembly so hidden state and user operators
    // cannot be masked by the shared harness's reflection-based object comparer.
    public static async Task AssertAsync(string contracts, string assertions)
    {
        var result = await new SerializationTestCompiler().CompileAndRunAsync(contracts + Oracle, assertions);
        Assert.True(result.Success, result.ErrorMessage + "\n" + result.DeserializationDetails);
    }

    private const string Oracle = """

        public static class ReviewOracle
        {
            public static void Check(bool condition, string message)
            {
                if (!condition) throw new InvalidOperationException(message);
            }

            public static SerializationTestResultData Parity<T>(T original, Action<T> state)
            {
                var net = new System.Runtime.Serialization.DataContractSerializer(typeof(T));
                var custom = Marius.DataContracts.Runtime.DataContractContext.GetSerializer<T>();
                string Write(bool generated)
                {
                    using var stream = new MemoryStream();
                    if (generated)
                        custom.WriteObject(stream, original);
                    else
                        net.WriteObject(stream, original);

                    return System.Text.Encoding.UTF8.GetString(stream.ToArray());
                }

                var netXml = Write(false);
                var customXml = Write(true);
                Check(netXml == customXml, "XML mismatch. NET: " + netXml + " CUSTOM: " + customXml);
                state(original);

                foreach (var xml in new[] { netXml, customXml })
                {
                    using var netStream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(xml));
                    state((T)net.ReadObject(netStream)!);

                    using var customStream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(xml));
                    state((T)custom.ReadObject(customStream)!);
                }

                return new SerializationTestResultData { Success = true, NetXml = netXml, CustomXml = customXml };
            }
        }
        """;
}