using TinyNet.Http;

namespace TinyNet.Protocols.Http1;

internal sealed record RequestHead(
    string Method,
    string Path,
    string RawPath,
    string Protocol,
    string? Authority,
    HttpHeaders Headers,
    Dictionary<string, string> Query,
    long? ContentLength,
    bool IsChunked,
    bool KeepAlive,
    bool ExpectContinue)
{
    public bool HasBody => IsChunked || ContentLength > 0;
}