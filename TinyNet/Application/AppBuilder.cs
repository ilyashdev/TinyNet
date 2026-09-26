using TinyNet.ActionResult.Results;
using TinyNet.Configurations;
using TinyNet.DI;
using TinyNet.Http;
using TinyNet.Middlewares;
using TinyNet.Routing;

namespace TinyNet.Application;

public class AppBuilder
{
    public DIContainer Services { get; init; }
    public GroupRoute Routes { get; } = new("/");
    private NetHandler _netHandler;
    private ConfigurationBuilder _configBuilder { get; init; }
    private MiddlewarePipeline _pipeline { get; init; }
    private bool _isBuilt = false;

    public AppBuilder()
    {
        _configBuilder = new ConfigurationBuilder();
        _configBuilder.AddDefaults(FrameworkDefaults.All);
        Services = new DIContainer();
        _pipeline = new MiddlewarePipeline(Services);
    }

    public AppBuilder AddDefault(string key, string value)
    {
        
        _configBuilder.AddDefault(key, value);
        return this;
    }

    public AppBuilder AddDefaults(IEnumerable<KeyValuePair<string, string>> values)
    {
        _configBuilder.AddDefaults(values);
        return this;
    }

    public AppBuilder AddJsonConfig(string path, bool optional = false)
    {
        _configBuilder.AddJsonFile(path, optional);
        return this;
    }

    public AppBuilder AddEnvironmentVariables(string prefix = null)
    {
        _configBuilder.AddEnvironmentVariables(prefix);
        return this;
    }

    public AppBuilder RegisterMiddleware<T>() where T : IMiddleware
    {
        _pipeline.RegisterMiddleware<T>();
        return this;
    }

    public WebApplication Build()
    {
        if (_isBuilt)
            throw new InvalidOperationException("App is already built.");
        _isBuilt = true;
        var conf =
            _configBuilder

                .Build();

        Services.AddInstance(conf);
        _netHandler = new(conf.GetValue<int>(FrameworkDefaults.ServerPort), ReadHttpLimits(conf));
        var endpoints = RouteCompiler.Compile(Routes, Services);
        var router = new UrlRouter(endpoints);
        Services.Freeze();
        Services.Validate();
        foreach (var endpoint in endpoints)
            endpoint.Link(Services);
        _pipeline.Build(new RouteDispatcher(router, NotFound).InvokeAsync);
        return new WebApplication(
            _netHandler,
            _pipeline,
            conf,
            Services
        );
    }

    private static Task NotFound(HttpContext context)
    {
        new NotFound().ExecuteResult(context);
        return Task.CompletedTask;
    }

    private static HttpLimits ReadHttpLimits(IConfiguration conf) =>
        new(
            conf.GetValue<int>(FrameworkDefaults.ServerMaxHeadBytes),
            conf.GetValue<int>(FrameworkDefaults.ServerMaxBodyBytes),
            conf.GetValue<int>(FrameworkDefaults.ServerReceiveBufferSize),
            TimeSpan.FromSeconds(conf.GetValue<int>(FrameworkDefaults.ServerReadTimeoutSeconds))
        );
}