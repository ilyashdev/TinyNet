using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using TinyNet.Exceptions;

namespace TinyNet.Http;

public sealed class HttpRequest
{
    public string Method { get; }
    public string Path { get; }
    internal string RawPath { get; }
    public string Protocol { get; }
    public bool HasBody { get; }
    private Dictionary<string, string>? Route { get; set; }
    private HttpHeaders Headers { get; }
    private Dictionary<string, string> Query { get; }
    private readonly Stream _body;
    private byte[]? _buffer;
    private bool _consumed;
    private readonly HttpSettings _settings;
    private readonly CancellationToken _ct;

    internal HttpRequest(
        string method,
        string path,
        string rawPath,
        string protocol,
        HttpSettings settings,
        HttpHeaders headers,
        Dictionary<string, string> query,
        Stream body,
        bool hasBody,
        CancellationToken ct)
    {
        Method = method;
        Path = path;
        RawPath = rawPath;
        Protocol = protocol;
        _settings = settings;
        Headers = headers;
        Query = query;
        _body = body;
        HasBody = hasBody;
        _ct = ct;
    }

    internal void SetRoute(Dictionary<string, string> route)
    {
        if (Route is not null)
            throw new UnreachableException("Route values have already been set.");
        Route = route;
    }

    public T? GetFromRoute<T>(string key) where T : struct, IParsable<T>
        => Parse<T>(GetFromRoute(key), RequestValueSource.Route, key);

    public string? GetFromRoute(string key)
    {
        if (Route is null)
            throw new InvalidOperationException("Route values are not available before routing.");
        return Route.GetValueOrDefault(key);
    }

    public T? GetFromQuery<T>(string key) where T : struct, IParsable<T>
        => Parse<T>(GetFromQuery(key),RequestValueSource.Query, key);

    public string? GetFromQuery(string key)
        => Query.GetValueOrDefault(key);

    public string? GetFromHeaders(string key)
        => Headers.GetOrNull(key);

    public ValueTask<T?> ReadJsonAsync<T>() where T : class
        => ReadJsonAsync<T>(_settings.Options);

    public Stream GetBodyStream()
    {
        if (_buffer is not null)
            return new MemoryStream(_buffer, writable: false);
        if (_consumed)
            throw new InvalidOperationException("Request body has already been read. " +
                                                "Call BufferBodyAsync() before the first read to " +
                                                "read it more than once.");
        _consumed = true;
        return _body;
    }

    public async ValueTask<ReadOnlyMemory<byte>> BufferBodyAsync()
    {
        if (_buffer is null)
        {
            using var buffer = new MemoryStream();
            await GetBodyStream().CopyToAsync(buffer, _ct);
            _buffer = buffer.ToArray();
        }

        return _buffer;
    }

    public async ValueTask<T?> ReadJsonAsync<T>(JsonSerializerOptions options) where T : class
    {
        try
        {
            return await JsonSerializer.DeserializeAsync<T>(GetBodyStream(), options, _ct);
        }
        catch (JsonException e)
        {
            throw new RequestJsonException(e);
        }
    }

    private static T? Parse<T>(string? raw, RequestValueSource source, string key) where T : struct, IParsable<T>
    {
        if (raw is null)
            return null;
        if (T.TryParse(raw, CultureInfo.InvariantCulture, out var value))
            return value;
        throw new RequestValueException(source, key, raw, typeof(T));
    }
}