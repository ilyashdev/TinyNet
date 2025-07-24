using TinyNet.ActionResult.Results;
using TinyNet.Controllers;
using TinyNet.DI;
using TinyNet.Http;
using TinyNet.Middlewares;
using TinyNet.TaskResult;

namespace TinyNet.Application;

public class WebApplication
{
    private NetHandler _handler;
    private MiddlewarePipeline _pipeline;
    private ControllerHandler _controllerHandler;
    public WebApplication(
        NetHandler handler, ControllerHandler controllerHandler, MiddlewarePipeline pipeline)
    {
        _handler = handler;
        _controllerHandler = controllerHandler;
        _pipeline = pipeline;
    }

    public async Task Run()
    {
        while (true)
        {
            try
            {
                var client = _handler.Accept();
                _ = Task.Run(async () => await ProcessClient(client));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Accept error: {ex.Message}");
            }
        }
    }

    private async Task ProcessClient(NetClient client)
    {
        try
        {
            using (client)
            using (DIScope scope = new())
            {
                HttpRequest request = await client.GetRequest();
                HttpContext context = new(request, null);
                var adapter = new MiddlewareControllerAdapter(_controllerHandler, scope);
                try
                {
                    var controllerType  = _controllerHandler.GetTypeHandler(request.Url);
                    if (controllerType.Status != HandleResultStatus.Success)
                    {
                        new BadRequest(controllerType.Status).ExecuteResult(ref context);
                    }
                    else
                    {
                        
                        await _pipeline.InvokeAsync(
                            context,
                            controllerType.Result,
                            scope,
                            adapter.InvokeAsync
                        );
                    }
                    client.SendResponse(context.Response);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Processing error: {ex.Message}");
                    new InternalError().ExecuteResult(ref context);
                    client.SendResponse(context.Response);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Processing error: {ex.Message}");
            var errorResponse = new HttpResponse(500,"Internal server error");
            client.SendResponse(errorResponse);
        }
    }
}