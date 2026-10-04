using System.Globalization;
using System.Net;
using System.Text.Json;

namespace TinyNet.Http;

public class HttpRequest
{
    public string Method {get;}
    public string Path {get;}
    public string Protocol {get;}
    private IPEndPoint RemoteEndPoint {get;}
    private Dictionary<string, string> Route {get;}
    private HttpHeaders Headers {get;}
    private Dictionary<string, string> Query {get;}
    public Stream Body {get;}
    private readonly HttpSettings _settings;
    
    internal HttpRequest(
        string method,
        string path,
        string protocol,
        HttpSettings settings, 
        IPEndPoint remoteEndPoint,
        Dictionary<string, string> route,
        HttpHeaders headers,
        Dictionary<string, string> query,
        Stream body)
    {
        Method = method;
        Path = path;
        Protocol = protocol;
        _settings = settings;
        RemoteEndPoint = remoteEndPoint;
        Route = route;
        Headers = headers;
        Query = query;
        Body = body;
    }
    public IPAddress GetIpAddress()
        => RemoteEndPoint.Address;
    public T? GetFromRoute<T>(string route) where T : struct, IParsable<T>
        => Parse<T>(GetFromRoute(route));
    public string? GetFromRoute(string route)
        => Route.GetValueOrDefault(route);
    public T? GetFromQuery<T>(string query) where T : struct, IParsable<T>
        => Parse<T>(Query.GetValueOrDefault(query));
    public string? GetFromHeaders(string header)
        => Headers.GetOrNull(header);
    public Task<T?> ReadJsonAsync<T>() where T : class
        => ReadJsonAsync<T>(_settings.Options);
    public async Task<T?> ReadJsonAsync<T>(JsonSerializerOptions options) where T : class
    {
        try
        {
            return await JsonSerializer.DeserializeAsync<T>(Body, options);
        }
        catch (JsonException)
        {
            return null;
        }
    }
    public async Task<ReadOnlyMemory<byte>> ReadBytesAsync()
    {
        using var buffer = new MemoryStream();
        await Body.CopyToAsync(buffer);
        return buffer.GetBuffer().AsMemory(0, (int)buffer.Length);
    }
    private static T? Parse<T>(string? raw) where T : struct, IParsable<T>                                                                                                                                                                           
        => T.TryParse(raw, CultureInfo.InvariantCulture, out var value) ? value : null;       
}