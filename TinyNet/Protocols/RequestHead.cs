using TinyNet.Http;

namespace TinyNet.Protocols.Http1;

internal sealed class RequestHead
{
    public string Method;
    public string Path;
    public string RawPath;
    public string Protocol;
    public string? Authority;
    public HttpHeaders Headers;
    public Dictionary<string, string> Query;
    public long? ContentLength;
    public bool IsChunked;
    public bool KeepAlive;
    public bool ExpectContinue;
    public bool HasBody() => IsChunked || ContentLength > 0;
}