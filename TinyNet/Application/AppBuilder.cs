using TinyNet.Controllers;
using TinyNet.DI;
using TinyNet.Middleware;

namespace TinyNet.Application;

public class AppBuilder
{
    public readonly DIContainer Services;
    private readonly NetHandler _handler;
    public readonly MiddlewarePipeline Pipeline;
    private readonly ControllerHandler _controllerHandler;

    public AppBuilder()
    {
        Services = new DIContainer();
        _handler = new NetHandler(5267);
        Pipeline = new MiddlewarePipeline(Services);
        _controllerHandler = new ControllerHandler(Services);
        _controllerHandler.InitControllers();
    }


    
    public WebApplication Build()
        => new WebApplication(_handler,_controllerHandler, Pipeline);
}