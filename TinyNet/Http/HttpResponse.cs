namespace TinyNet.Http;

public sealed class HttpResponse
{
    public int StatusCode { get; }
    public HttpHeaders Headers { get; }
    public string? ContentType { get; }
    public ReadOnlyMemory<byte> Buffer { get; }
    public Func<Stream, CancellationToken, Task>? Stream { get; }
    public long? ContentLength { get; }

    internal HttpResponse(
        int statusCode,
        HttpHeaders headers,
        string? contentType = null,
        ReadOnlyMemory<byte> buffer = default,
        Func<Stream, CancellationToken, Task>? stream = null,
        long? contentLength = 0)
    {
        StatusCode = statusCode;
        Headers = headers;
        ContentType = contentType;
        Buffer = buffer;
        Stream = stream;
        ContentLength = contentLength;
    }
}