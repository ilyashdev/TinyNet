using TinyNet.DI;

namespace TinyNet.Http;

public sealed class HttpContext
{
    private readonly DIScope _scope;
    private readonly HttpSettings _settings;

    internal HttpContext(DIScope scope, HttpSettings settings)
    {
        _scope = scope;
        _settings = settings;
    }

    public ResponseBuilder Response() => new(_settings);

    public T GetService<T>() => _scope.GetService<T>();
}