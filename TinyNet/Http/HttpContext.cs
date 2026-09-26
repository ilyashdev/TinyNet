using System.Text.Json;
using TinyNet.DI;

namespace TinyNet.Http;

public sealed class HttpContext
{
    public HttpContext(DIScope scope, HttpRequest request, CancellationToken applicationStopping = default)
    {
        Scope = scope;
        Request = request;
        ApplicationStopping = applicationStopping;
    }

    public HttpRequest Request { get; }
    public HttpResponse? Response { get; set; }
    public CancellationToken ApplicationStopping { get; }
    internal DIScope Scope { get; }

    public T GetService<T>() => Scope.GetService<T>();

    public string? GetFromRoute(string key) => Request.Route.Find(key);

    public T? GetFromRoute<T>(string key) where T : struct, IParsable<T> => Request.Route.Find<T>(key);

    public string? GetFromQuery(string key) => Request.Query.Find(key);

    public T? GetFromQuery<T>(string key) where T : struct, IParsable<T> => Request.Query.Find<T>(key);

    public string? GetFromHeader(string key) => Request.Headers.Find(key);

    public T? GetFromHeader<T>(string key) where T : struct, IParsable<T> => Request.Headers.Find<T>(key);

    public ValueTask<T?> ReadFromBodyAsync<T>() where T : class
    {
        if (Request.Body is null)
            return ValueTask.FromResult<T?>(null);
        try
        {
            return ValueTask.FromResult(Request.Body.Deserialize<T>(JsonDefaults.Options));
        }
        catch (JsonException)
        {
            return ValueTask.FromResult<T?>(null);
        }
    }
}