using TinyNet.Configurations;
using TinyNet.Controllers;
using TinyNet.DI;
using TinyNet.Middlewares;

namespace TinyNet.Application;

public class AppBuilder
{
    public DIContainer Services { get; init; }
    private NetHandler _netHandler;
    private ConfigurationBuilder _configBuilder { get; init; }
    private MiddlewarePipeline _pipeline { get; init; }
    private ControllerHandler _controllerHandler;

    public AppBuilder()
    {
        _configBuilder = new ConfigurationBuilder();
        Services = new DIContainer();
        _pipeline = new MiddlewarePipeline(Services);
    }
    public AppBuilder AddJsonConfig(string path)
    {
        _configBuilder.AddJsonFile(path);
        return this;
    }

    public AppBuilder AddEnvironmentVariables(string prefix = null)
    {
        _configBuilder.AddEnvironmentVariables(prefix);
        return this;
    }

    public AppBuilder RegisterMiddleware<T>() where T : Middleware
    {
        _pipeline.RegisterMiddleware<T>();
        return this;
    }

    public AppBuilder RegisterFilter<T>() where T : Middleware
    {
        _pipeline.RegisterFilter<T>();
        return this;
    }

    public WebApplication Build()
    {
        var conf = _configBuilder.Build();
        Services.AddInstance(conf);
        Services.AddTransient<MediaHandler>();
        _netHandler = new(
            Convert.ToInt32(
            conf["Server:Port"]));
        _controllerHandler = new ControllerHandler(Services);
        _controllerHandler.InitControllers();
        return new WebApplication(
            _netHandler,
            _controllerHandler,
            _pipeline
        );
    }
}