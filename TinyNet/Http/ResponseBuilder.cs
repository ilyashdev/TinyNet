using System.Text;
using System.Text.Json;

namespace TinyNet.Http;

public sealed class ResponseBuilder
{
    private readonly HttpSettings _settings;
    private readonly HttpHeaders _headers = new();
    private int _statusCode = StatusCodes.OK;

    internal ResponseBuilder(HttpSettings settings)
    {
        _settings = settings;
    }

    public ResponseBuilder Status(int statusCode)
    {
        if (statusCode is < 200 or > 599)
            throw new ArgumentOutOfRangeException(nameof(statusCode), statusCode, "Status code must be in range 200-599.");
        _statusCode = statusCode;
        return this;
    }

    public ResponseBuilder AddHeader(string name, string value)
    {
        if (name.Equals(HeaderNames.ContentLength, StringComparison.OrdinalIgnoreCase)
            || name.Equals(HeaderNames.TransferEncoding, StringComparison.OrdinalIgnoreCase)
            || name.Equals("Keep-Alive", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException($"Header '{name}' is set by the server.", nameof(name));
        if (name.Equals(HeaderNames.Connection, StringComparison.OrdinalIgnoreCase)
            && !value.Equals("close", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Connection header only accepts 'close'.", nameof(value));
        _headers.Add(name, value);
        return this;
    }

    public HttpResponse Json<T>(T value)
        => Json(value, _settings.Options);

    public HttpResponse Json<T>(T value, JsonSerializerOptions options)
        => Bytes(JsonSerializer.SerializeToUtf8Bytes(value, options), MediaTypes.ApplicationJson);

    public HttpResponse Text(string text)
        => Bytes(Encoding.UTF8.GetBytes(text), MediaTypes.TextPlainUtf8);

    public HttpResponse Bytes(ReadOnlyMemory<byte> data, string contentType)
        => new(_statusCode, _headers, contentType, data, contentLength: data.Length);

    public HttpResponse Stream(Func<Stream, CancellationToken, Task> write, string contentType, long? length = null)
        => new(_statusCode, _headers, contentType, stream: write, contentLength: length);

    public HttpResponse Empty()
        => new(_statusCode, _headers);
}