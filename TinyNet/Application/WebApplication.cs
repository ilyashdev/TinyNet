using System.Net;
using System.Net.Sockets;
using TinyNet.DI;
using TinyNet.Http;
using TinyNet.Protocols;
using TinyNet.Transport;

namespace TinyNet.Application;

public sealed class WebApplication
{
    private static readonly TimeSpan AcceptRetryDelay = TimeSpan.FromMilliseconds(100);
    private const int AcceptRetryJitterMs = 100;

    private readonly IConnectionListener _listener;
    private readonly IHttpProtocol _protocol;
    private readonly RequestDelegate _app;
    private readonly DIContainer _container;
    private readonly int _maxPending;
    private readonly int _acceptLoops;
    private readonly HashSet<Task> _connections = [];
    private readonly Lock _lock = new();
    private int _pending;

    internal WebApplication(
        IConnectionListener listener,
        IHttpProtocol protocol,
        RequestDelegate app,
        DIContainer container,
        int maxConnections,
        int acceptLoops)
    {
        _listener = listener;
        _protocol = protocol;
        _app = app;
        _container = container;
        _maxPending = maxConnections;
        _acceptLoops = acceptLoops;
    }

    public int Port => ((IPEndPoint)_listener.EndPoint).Port;

    public async Task Run(CancellationToken ct = default)
    {
        Console.WriteLine($"Application started on http://localhost:{Port}");
        try
        {
            await Task.WhenAll(Enumerable.Range(0, _acceptLoops).Select(_ => AcceptLoopAsync(ct)));
        }
        finally
        {
            _listener.Dispose();
            Task[] running;
            lock (_lock)
                running = [.. _connections];
            await Task.WhenAll(running);
            await _container.DisposeAsync();
            Console.WriteLine($"Application stopped on http://localhost:{Port}");
        }
    }

    private async Task AcceptLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            Connection connection;
            try
            {
                connection = await _listener.AcceptAsync(ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                return;
            }
            catch (SocketException e)
            {
                Console.WriteLine($"Accept error: {e.Message}");
                var jitter = TimeSpan.FromMilliseconds(Random.Shared.Next(AcceptRetryJitterMs));
                await Task.Delay(AcceptRetryDelay + jitter, ct).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
                continue;
            }

            Track(Task.Run(() => ServeAsync(connection, ct)));
        }
    }

    private async Task ServeAsync(Connection connection, CancellationToken ct)
    {
        await using (connection)
        {
            try
            {
                if (Interlocked.Increment(ref _pending) > _maxPending)
                    await _protocol.RejectAsync(connection, StatusCodes.ServiceUnavailable, "Server is overloaded.", ct);
                else
                {
                        await _protocol.ProcessAsync(connection, _app, ct);
                }
            }
            catch (Exception e) when (e is IOException or OperationCanceledException)
            {
            }
            catch (Exception e)
            {
                Console.WriteLine($"Connection error: {e}");
            }
            finally
            {
                Interlocked.Decrement(ref _pending);
            }

            await connection.CloseGracefullyAsync();
        }
    }

    private void Track(Task task)
    {
        lock (_lock)
            _connections.Add(task);
        task.ContinueWith(t =>
        {
            lock (_lock)
                _connections.Remove(t);
        }, TaskScheduler.Default);
    }
}