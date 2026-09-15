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
        var conf = 
            _configBuilder
                
                .Build();
        
        Services.AddInstance(conf);
        Services.AddTransient<MediaHandler>();
        _netHandler = new(conf.GetValue<int>(FrameworkDefaults.ServerPort));
        var controllerHandler = new ControllerHandler(Services);
            controllerHandler.InitControllers();
        return new WebApplication(
            _netHandler,
            controllerHandler,
            _pipeline,
            conf
        );
    }
}