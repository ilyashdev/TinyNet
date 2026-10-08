using TinyNet.DI;
using TinyNet.Http;

namespace TinyNet.Middlewares;

public class MiddlewarePipeline
{
    private readonly DIContainer _container;
    private readonly List<Type> _middlewares = new();

    private bool _built = false;

    private RequestDelegate? _middlewareChains;

    public MiddlewarePipeline(DIContainer container)
    {
        _container = container;
    }

    public void RegisterMiddleware<T>() where T : IMiddleware
    {
        EnsureNotBuilt();
        _container.AddSingleton<T>();
        _middlewares.Add(typeof(T));
    }

    public void Build(RequestDelegate final)
    {
        EnsureNotBuilt();
        _built = true;
        _middlewareChains = Compose(_middlewares.Select(t => (IMiddleware)_container.GetSingleton(t)).ToList(), final);
    }

    public Task<HttpResponse> InvokeAsync(HttpRequest request, HttpContext context)
        => (_middlewareChains ?? throw new InvalidOperationException("Pipeline is not built"))(request, context);

    internal static RequestDelegate Compose(IReadOnlyList<IMiddleware> middlewares, RequestDelegate final)
    {
        var next = final;
        for (var i = middlewares.Count - 1; i >= 0; i--)
        {
            var middleware = middlewares[i];
            var inner = next;
            next = (request, ctx) => middleware.InvokeAsync(request, ctx, inner);
        }

        return next;
    }

    private void EnsureNotBuilt()
    {
        if (_built)
            throw new InvalidOperationException("Pipeline already built");
    }
}