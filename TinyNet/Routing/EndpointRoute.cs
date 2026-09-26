using TinyNet.Middlewares;

namespace TinyNet.Routing;

public sealed class EndpointRoute
{
    private readonly List<Type> _filters = new();
    private bool _frozen;

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

    internal void Freeze() => _frozen = true;

    public EndpointRoute AddFilter<T>() where T : IMiddleware
    {
        if (_frozen)
            throw new InvalidOperationException($"{Method} endpoint of {Controller.Name} cannot be changed after Build()");
        _filters.Add(typeof(T));
        return this;
    }
}