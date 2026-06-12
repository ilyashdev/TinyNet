using System.Threading.Channels;
using TinyNet.ActionResult.Results;
using TinyNet.Configurations;
using TinyNet.Controllers;
using TinyNet.DI;
using TinyNet.Http;
using TinyNet.Middlewares;
using TinyNet.TaskResult;



namespace TinyNet.Application;

public class WebApplication
{
    private readonly NetHandler _handler;
    private readonly MiddlewarePipeline _pipeline;
    private readonly ControllerHandler _controllerHandler;
    private readonly IConfiguration _configuration;
    public WebApplication(
        NetHandler handler, ControllerHandler controllerHandler, MiddlewarePipeline pipeline, IConfiguration configuration)
    {
        _handler = handler;
        _controllerHandler = controllerHandler;
        _pipeline = pipeline;
        _configuration = configuration;
    }

  
    public async Task Run()
    {
        Console.WriteLine($"Application started on http://localhost:{_configuration["Server:Port"]}");
        var channel = Channel.CreateUnbounded<NetClient>();
        var acceptLoop = AcceptLoop(channel);
        int workerCount = Environment.ProcessorCount * 2 - 1;                                                                                                                                                                                         
        var workers = Enumerable.Range(0, workerCount)                                                                                                                                                                                                
            .Select(_ => Worker(channel));                                                                                                                                                                                                                                      
        await Task.WhenAll(workers.Append(acceptLoop));  
    }

    private async Task Worker(Channel<NetClient> channel)
    {
        await foreach (var client in channel.Reader.ReadAllAsync())
            await ProcessClient(client);
    }

    private async Task AcceptLoop(Channel<NetClient> channel)
    {
        while (true)
            try
            {
                await channel.Writer.WriteAsync(await _handler.AcceptAsync());
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Processing error: {ex.InnerException?.ToString() ?? ex.ToString()}");
            }
    }
    
    private async Task ProcessClient(NetClient client)
    {
        using (client)
        using (DIScope scope = new())
        {
            HttpResponse response = null;
            try
            {

                HttpRequest request = await client.GetRequest();
                HttpContext context = new(request, null);
                var adapter = new MiddlewareControllerAdapter(_controllerHandler, scope);
                try
                {
                    var controllerType = _controllerHandler.GetTypeHandler(request.Url);
                    if (controllerType.Status != HandleResultStatus.Success)
                    {
                        new BadRequest(controllerType.Status).ExecuteResult(context);
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

                    response = context.Response;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Processing error: {ex.InnerException?.ToString() ?? ex.ToString()}");
                    new InternalError().ExecuteResult(context);
                    response = context.Response;
                }

            }
            catch (Exception ex)
            {
                Console.WriteLine($"Processing error: {ex.Message}");
                var errorResponse = new HttpResponse(500, "Internal server error");
                response = errorResponse;
            }
            finally
            {
                if (client.IsConnected())
                    await client.SendResponse(response);
            }
        }
    }
}