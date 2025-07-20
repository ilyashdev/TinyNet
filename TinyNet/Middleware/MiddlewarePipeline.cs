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

    private List<IMiddleware> CreatePipeline(IEnumerable<FilterAttribute> filters, DIScope scope)
    {
        var pipeline = new List<IMiddleware>();
        var pipelineMiddlewares = new List<Type>();
        pipelineMiddlewares.AddRange(_allMiddlewares);
        pipelineMiddlewares.AddRange(_filterMiddlewares.Where(c => filters.Any(f => f.FilterType == c)));
        foreach (var middleware in pipelineMiddlewares)
        {
            var serviceMethodInfo = typeof(DIContainer).GetMethod("GetService");
            pipeline.Add((IMiddleware)serviceMethodInfo
                .MakeGenericMethod(middleware)
                .Invoke(_container, [scope]));
        }
        return pipeline;
    }
    
    public async Task InvokeAsync(HttpContext context, Type controller,DIScope scope, RequestDelegate finalHandler)
    {
        
        var attributes = controller.GetCustomAttributes<FilterAttribute>();
        var pipeline = CreatePipeline(attributes,scope);
        for (int i = 0; i < pipeline.Count-1; i++)
        {
            RequestDelegate next = pipeline[i + 1].InvokeAsync;
            await pipeline[i].InvokeAsync(context, next);
        }
        await pipeline.Last().InvokeAsync(context, finalHandler);
    }
    
}