using System.Text.Json;

namespace TinyNet.Http;

public sealed class HttpSettings
{
    public JsonSerializerOptions Options { get; }
    public long MaxBodyLength { get; }

    internal HttpSettings(JsonSerializerOptions options, long maxBodyLength)
    {
        Options = options;
        MaxBodyLength = maxBodyLength;
    }
}