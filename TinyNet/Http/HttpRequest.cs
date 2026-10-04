using System.Globalization;
using System.Text.Json;
using TinyNet.Http.Exceptions;

namespace TinyNet.Http;

public class HttpRequest
{
    public string Method {get;}
    public string Path {get;}
    public string Protocol {get;}
    private Dictionary<string, string> Route {get;}
    private HttpHeaders Headers {get;}
    private Dictionary<string, string> Query {get;}
    private readonly Stream _body;
    private byte[]? _buffer;
    private bool _consumed;
    private readonly HttpSettings _settings;
    private readonly CancellationToken _ct;
    
    internal HttpRequest(
        string method,
        string path,
        string protocol,
        HttpSettings settings, 
        Dictionary<string, string> route,
        HttpHeaders headers,
        Dictionary<string, string> query,
        Stream body, 
        CancellationToken ct)
    {
        Method = method;
        Path = path;
        Protocol = protocol;
        _settings = settings;
        Route = route;
        Headers = headers;
        Query = query;
        _body = body;
        _ct = ct;
    }
    public T? GetFromRoute<T>(string key) where T : struct, IParsable<T>
        => Parse<T>(GetFromRoute(key));
    public string? GetFromRoute(string key)
        => Route.GetValueOrDefault(key);
    public T? GetFromQuery<T>(string key) where T : struct, IParsable<T>
        => Parse<T>(GetFromQuery(key));
    public string? GetFromQuery(string key)
        => Query.GetValueOrDefault(key);
    public string? GetFromHeaders(string key)
        => Headers.GetOrNull(key);
    public ValueTask<T?> ReadJsonAsync<T>() where T : class
        => ReadJsonAsync<T>(_settings.Options);
    public Stream GetBodyStream()
    {
        if(_buffer is not null)
            return new MemoryStream(_buffer, writable: false);
        if(_consumed)
            throw new InvalidOperationException("Request body has already been read. " +
                                                "Call BufferBodyAsync() before the first read or read this once.");
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
    private static T? Parse<T>(string? raw) where T : struct, IParsable<T>                                                                                                                                                                           
        => T.TryParse(raw, CultureInfo.InvariantCulture, out var value) ? value : null;
}