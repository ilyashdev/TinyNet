using TinyNet.Middlewares;

namespace TinyNet.Routing;

public sealed class EndpointRoute
{
    private readonly List<Type> _filters = new();

    internal EndpointRoute(string method, Type controller, RequestDelegate handler)
    {
        Method = method;
        Controller = controller;
        Handler = handler;
    }

    internal string Method { get; }
    internal Type Controller { get; }
    internal RequestDelegate Handler { get; }
    internal IReadOnlyList<Type> Filters => _filters;

    public EndpointRoute AddFilter<T>() where T : IMiddleware
    {
        _filters.Add(typeof(T));
        return this;
    }
}