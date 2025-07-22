using System.Reflection;
using TinyNet.DI;
using TinyNet.Http;

namespace TinyNet.Middleware;

public class MiddlewarePipeline
{
    private readonly DIContainer _container;
    private ICollection<Type> _filterMiddlewares = new List<Type>();
    private ICollection<Type> _allMiddlewares = new List<Type>();

    public MiddlewarePipeline(DIContainer container)
    {
        _container = container;
    }

    public void RegisterMiddleware<T>() where T : IMiddleware
    {
        _container.AddTransient<T>();
        _allMiddlewares.Add(typeof(T));
    }

    public void RegisterFilter<T>() where T : IMiddleware
    {
        _container.AddTransient<T>();
        _filterMiddlewares.Add(typeof(T));
    }

    private List<IMiddleware> CreatePipeline(IEnumerable<FilterAttribute> filters, DIScope scope, RequestDelegate final)
    {
        var pipeline = new List<IMiddleware>();
        var pipelineMiddlewares = new List<Type>();
        pipelineMiddlewares.AddRange(_allMiddlewares);
        pipelineMiddlewares.AddRange(_filterMiddlewares.Where(c => filters.Any(f => f.FilterType == c)));
        pipelineMiddlewares.Reverse();
        RequestDelegate temp = final;
        foreach (var middleware in pipelineMiddlewares)
        {
            var middlewareInstance = _container.GetMiddleware(middleware, temp, scope);
            pipeline.Add(middlewareInstance);
            temp = middlewareInstance.InvokeAsync;
        }
        return pipeline;
    }
    
    public async Task InvokeAsync(HttpContext context, Type controller,DIScope scope, RequestDelegate final)
    {
        var attributes = controller.GetCustomAttributes<FilterAttribute>();
        var pipeline = CreatePipeline(attributes,scope, final);
        if (pipeline.Count == 0)
        {
            await final(context);
            return;
        }
        await pipeline.Last().InvokeAsync(context);
    }
    
}