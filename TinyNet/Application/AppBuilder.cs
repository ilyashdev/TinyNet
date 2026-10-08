using System.Text.Json;
using TinyNet.Configurations;
using TinyNet.DI;
using TinyNet.Http;
using TinyNet.Middlewares;
using TinyNet.Protocols.Http1;
using TinyNet.Routing;
using TinyNet.Transport;

namespace TinyNet.Application;

public class AppBuilder
{
    public DIContainer Services { get; init; }
    public GroupRoute Routes { get; } = new("/");
    private ConfigurationBuilder _configBuilder { get; init; }
    private MiddlewarePipeline _pipeline { get; init; }
    private readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);
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

    public AppBuilder Json(Action<JsonSerializerOptions> configure)
    {
        configure(_json);
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
        _json.MakeReadOnly(populateMissingResolver: true);
        var settings = new HttpSettings(_json, conf.GetValue<long>(FrameworkDefaults.ServerMaxBodyBytes));
        var limits = new ServerLimits(
            TimeSpan.FromSeconds(conf.GetValue<int>(FrameworkDefaults.ServerKeepAliveTimeoutSeconds)),
            TimeSpan.FromSeconds(conf.GetValue<int>(FrameworkDefaults.ServerHeadersTimeoutSeconds)),
            TimeSpan.FromSeconds(conf.GetValue<int>(FrameworkDefaults.ServerBodyGracePeriodSeconds)),
            conf.GetValue<double>(FrameworkDefaults.ServerMinBodyBytesPerSecond),
            conf.GetValue<int>(FrameworkDefaults.ServerKeepAliveMax),
            conf.GetValue<int>(FrameworkDefaults.ServerMaxHeadBytes));

        var endpoints = RouteCompiler.Compile(Routes, Services);
        var router = new UrlRouter(endpoints);
        Services.Freeze();
        Services.Validate();
        foreach (var endpoint in endpoints)
            endpoint.Link(Services);
        _pipeline.Build(new RouteDispatcher(router, NotFound).InvokeAsync);

        var acceptLoops = conf.GetValue<int>(FrameworkDefaults.ServerAcceptLoops);
        if (acceptLoops < 1)
            throw new InvalidOperationException($"{FrameworkDefaults.ServerAcceptLoops} must be at least 1, got {acceptLoops}");

        return new WebApplication(
            new TcpConnectionListener(
                conf.GetValue<int>(FrameworkDefaults.ServerPort),
                conf.GetValue<int>(FrameworkDefaults.ServerListenBacklog)),
            new ConnectionHandler(settings, Services, limits),
            LimitConcurrency(_pipeline.InvokeAsync,
                conf.GetValue<int>(FrameworkDefaults.ServerMaxConcurrentRequests),
                conf.GetValue<int>(FrameworkDefaults.ServerMaxQueuedRequests),
                conf.GetValue<int>(FrameworkDefaults.ServerRequestQueueTimeoutSeconds)),
            Services,
            conf.GetValue<int>(FrameworkDefaults.ServerMaxConnections),
            acceptLoops);
    }

    internal static RequestDelegate LimitConcurrency(RequestDelegate app, int max, int queue, int seconds)
    {
        var slots = new SemaphoreSlim(max, max);
        var wait = TimeSpan.FromSeconds(seconds);
        var inSystem = 0;
        return async (request, context) =>
        {
            try
            {
                if (Interlocked.Increment(ref inSystem) > max + queue || !await slots.WaitAsync(wait))
                    return context.Response()
                        .Status(StatusCodes.ServiceUnavailable)
                        .AddHeader(HeaderNames.Connection, "close")
                        .Text("Server is overloaded.");
                try
                {
                    return await app(request, context);
                }
                finally
                {
                    slots.Release();
                }
            }
            finally
            {
                Interlocked.Decrement(ref inSystem);
            }
        };
    }

    private static Task<HttpResponse> NotFound(HttpRequest request, HttpContext context)
        => Task.FromResult(context.Response().Status(StatusCodes.NotFound).Empty());

}