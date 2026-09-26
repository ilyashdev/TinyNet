using System.Net.Sockets;
using System.Threading.Channels;
using TinyNet.ActionResult.Results;
using TinyNet.Configurations;
using TinyNet.DI;
using TinyNet.Http;
using TinyNet.Middlewares;



namespace TinyNet.Application;

public class WebApplication
{
    private readonly NetHandler _handler;
    private readonly MiddlewarePipeline _pipeline;
    private readonly IConfiguration _configuration;
    private readonly DIContainer _container;
    public WebApplication(
        NetHandler handler,
        MiddlewarePipeline pipeline,
        IConfiguration configuration,
        DIContainer container)
    {
        _handler = handler;
        _pipeline = pipeline;
        _configuration = configuration;
        _container = container;
    }

  
    public int Port => _handler.Port;

    public async Task Run(CancellationToken ct = default)
    {
        var port = _configuration.GetValue<int>(FrameworkDefaults.ServerPort);
        Console.WriteLine($"Application started on http://localhost:{port}");
        var channel = Channel.CreateBounded<NetClient>(
            new BoundedChannelOptions(_configuration.GetValue<int>(FrameworkDefaults.ServerMaxQueuedConnections))
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleWriter = true
            });
        var workers = Enumerable
            .Range(0, _configuration.GetValue<int>(FrameworkDefaults.ServerMaxConcurrentRequests))
            .Select(_ => Worker(channel, ct))
            .ToArray();
        try
        {
            await RunAcceptThread(channel, ct);
            channel.Writer.Complete();
            await Task.WhenAll(workers);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
        finally
        {
            await _container.DisposeAsync();
            Console.WriteLine($"Application stopped on http://localhost:{port}");
        }
    }
    
    private Task RunAcceptThread(Channel<NetClient> channel, CancellationToken ct)
    {
        var finished = new TaskCompletionSource();
        var thread = new Thread(() =>
        {
            try
            {
                AcceptLoop(channel, ct);
            }
            finally
            {
                finished.SetResult();
            }
        })
        {
            IsBackground = true,
            Name = "tinynet-accept"
        };
        thread.Start();
        return finished.Task;
    }

    private async Task Worker(Channel<NetClient> channel, CancellationToken ct)
    {
        await foreach (var client in channel.Reader.ReadAllAsync())
        {
            try
            {
                await ProcessClient(client, ct);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Worker error: {ex}");
            }
        }
    }

    private void AcceptLoop(Channel<NetClient> channel, CancellationToken ct)
    {
        using var stopping = ct.Register(_handler.StopListening);
        while (!ct.IsCancellationRequested)
        {
            NetClient client = null;
            try
            {
                client = _handler.Accept();
                if (!channel.Writer.TryWrite(client))
                {
                    client.SendOverloadedResponse();
                    client.Dispose();
                }
            }
            catch (Exception ex) when (ex is ObjectDisposedException or SocketException)
            {
                client?.Dispose();
                if (ct.IsCancellationRequested)
                    return;
                Console.WriteLine($"Accept error: {ex.Message}");
            }
            catch (Exception ex)
            {
                client?.Dispose();
                Console.WriteLine($"Accept error: {ex.InnerException?.Message ?? ex.Message}");
            }
        }
    }
    
    private async Task ProcessClient(NetClient client, CancellationToken ct)
    {
        var keepAliveMax = _configuration.GetValue<int>(FrameworkDefaults.ServerKeepAliveMax);
        var keepAliveTimeout = TimeSpan.FromSeconds(_configuration.GetValue<int>(FrameworkDefaults.ServerKeepAliveTimeout));
        using (client)
        {
            for (int remaining = keepAliveMax; remaining > 0; remaining--)
            {
                await using DIScope scope = _container.CreateScope();
                var idleTimeout = remaining == keepAliveMax ? (TimeSpan?)null : keepAliveTimeout;
                var response = await BuildResponse(client, scope, idleTimeout, remaining > 1, ct);
                if (response is null)
                    return;

                await SendSafely(client, response);
                if (!IsKeepAlive(response))
                    return;
            }
        }
    }

    private static bool IsKeepAlive(HttpResponse response)
        => response.Headers.TryGetValue("Connection", out var connection)
           && connection.Equals("keep-alive", StringComparison.OrdinalIgnoreCase);

    private async Task<HttpResponse?> BuildResponse(
        NetClient client, DIScope scope, TimeSpan? idleTimeout, bool allowKeepAlive, CancellationToken ct)
    {
        try
        {
            HttpRequest request = idleTimeout is null
                ? await client.GetRequest(ct)
                : await client.GetRequest(idleTimeout.Value, ct);
            var response = await Dispatch(new HttpContext(scope, request, ct));
            if (!response.Headers.ContainsKey("Connection"))
                response.Headers["Connection"] =
                    allowKeepAlive && Http.Http.IsKeepAlive(request) ? "keep-alive" : "close";
            return response;
        }
        catch (ConnectionClosedException)
        {
            return null;
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        catch (Exception ex)
        {
            var response = ToErrorResponse(ex);
            Console.WriteLine(response.StatusCode == 500
                ? $"Processing error: {ex}"
                : $"Request rejected: {ex.Message}");
            return response;
        }
    }

    private async Task<HttpResponse> Dispatch(HttpContext context)
    {
        try
        {
            await _pipeline.InvokeAsync(context);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Processing error: {ex.InnerException?.ToString() ?? ex.ToString()}");
            new InternalError().ExecuteResult(context);
        }

        return context.Response ?? new HttpResponse(500, "Internal server error");
    }

    private static HttpResponse ToErrorResponse(Exception ex) => ex switch
    {
        RequestTooLargeException => new HttpResponse(413, "Content too large"),
        RequestTimeoutException => new HttpResponse(408, "Request timeout"),
        BadRequestException => new HttpResponse(400, "Bad request"),
        _ => new HttpResponse(500, "Internal server error")
    };

    private static async Task SendSafely(NetClient client, HttpResponse response)
    {
        try
        {
            if (client.IsConnected())
                await client.SendResponse(response);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Send error: {ex.Message}");
        }
    }
}