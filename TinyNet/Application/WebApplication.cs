using System.Collections.Concurrent;
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
    private NetHandler _handler;
    private MiddlewarePipeline _pipeline;
    private ControllerHandler _controllerHandler;
    private IConfiguration _configuration;
    private ConcurrentQueue<NetClient> _clients = new ConcurrentQueue<NetClient>();
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
        var enqueueThread = new Thread(() => EnqueueClients())
        {
            IsBackground = true
        };
        enqueueThread.Start();
        var workerTasks = new List<Task>();
        int workerCount = Environment.ProcessorCount * 2 - 1; 
        for (int i = 0; i < workerCount; i++)
        {
            workerTasks.Add(Task.Run(() => TryProcess()));
        }
        await Task.WhenAll(workerTasks);
    }

    private void EnqueueClients()
    {
        while (true)
        {
            try
            {
                var client = _handler.Accept();
                _clients.Enqueue(client);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Accept error: {ex.Message}");
            }
        }
    }
    
    private async Task TryProcess()
    {
        while (true)
        {
            if (_clients.TryDequeue(out var client))
            {
                await ProcessClient(client);
            }
            else
            {
                await Task.Delay(10);
            }
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

                    response = context.Response;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Processing error: {ex.Message}");
                    new InternalError().ExecuteResult(ref context);
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