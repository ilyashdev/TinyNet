using TinyNet.DI;
using TinyNet.Http;
using TinyNet.Middlewares;

namespace TinyNet.Routing;

public sealed class Endpoint
{
    private readonly RequestDelegate _handler;
    private RequestDelegate? _chain;

    internal Endpoint(EndpointRoute route, string template, IReadOnlyList<Type> filters)
    {
        Method = route.Method;
        Template = template;
        var duplicate = filters.GroupBy(f => f).FirstOrDefault(g => g.Count() > 1);
        if (duplicate is not null)
            throw new InvalidOperationException($"{this}: filter {duplicate.Key.Name} is applied more than once");
        Filters = filters;
        _handler = route.Handler;
    }

    public string Method { get; }
    public string Template { get; }
    internal IReadOnlyList<Type> Filters { get; }

    internal void Link(DIContainer container)
    {
        if (_chain is not null)
            throw new InvalidOperationException($"{this}: endpoint already linked");
        var filters = Filters.Select(t => (IMiddleware)container.GetSingleton(t)).ToList();
        _chain = MiddlewarePipeline.Compose(filters, _handler);
    }

    internal Task InvokeAsync(HttpContext context)
        => (_chain ?? throw new InvalidOperationException($"{this}: endpoint is not linked"))(context);

    public override string ToString() => $"{Method} {Template}";
}