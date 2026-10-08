using TinyNet.Http;
using TinyNet.Middlewares;

namespace TinyNet.Routing;

public sealed class EndpointRoute
{
    private readonly List<Type> _filters = new();
    private bool _frozen;

    internal EndpointRoute(string method, string path, Type? controller, RequestDelegate handler)
    {
        Method = method;
        Path = path;
        Controller = controller;
        Handler = handler;
    }

    internal string Method { get; }
    internal string Path { get; }
    internal Type? Controller { get; }
    internal RequestDelegate Handler { get; }
    internal IReadOnlyList<Type> Filters => _filters;

    internal void Freeze() => _frozen = true;

    public EndpointRoute AddFilter<T>() where T : IMiddleware
    {
        if (_frozen)
            throw new InvalidOperationException($"{Method} {Path} endpoint cannot be changed after Build()");
        _filters.Add(typeof(T));
        return this;
    }
}