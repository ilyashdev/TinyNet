using System.Net;
using System.Net.Sockets;
using System.Text;
using TinyNet.Application;
using TinyNet.Routing;

namespace TinyNet.Tests;

internal sealed class TestServer : IAsyncDisposable
{
    private readonly CancellationTokenSource _cts = new(TimeSpan.FromSeconds(60));
    private readonly WebApplication _app;
    private readonly Task _run;

    private TestServer(WebApplication app)
    {
        _app = app;
        _run = app.Run(_cts.Token);
    }

    public int Port => _app.Port;

    public static TestServer Start(Action<GroupRoute> routes, Action<AppBuilder>? configure = null)
    {
        var builder = new AppBuilder();
        builder.AddDefault(FrameworkDefaults.ServerPort, "0");
        configure?.Invoke(builder);
        routes(builder.Routes);
        return new TestServer(builder.Build());
    }

    public async Task<(int Status, string Body)> SendAsync(string method, string target, string? body = null)
    {
        var raw = new StringBuilder($"{method} {target} HTTP/1.1\r\nHost: localhost\r\nConnection: close\r\n");
        if (body is not null)
            raw.Append($"Content-Type: application/json\r\nContent-Length: {Encoding.UTF8.GetByteCount(body)}\r\n");
        raw.Append("\r\n").Append(body);

        var response = await SendRawAsync(raw.ToString());
        var separator = response.IndexOf("\r\n\r\n", StringComparison.Ordinal);
        var status = int.Parse(response.Split(' ')[1]);
        return (status, separator < 0 ? "" : response[(separator + 4)..]);
    }

    public async Task<string> SendRawAsync(string raw)
    {
        using var socket = await ConnectAsync();
        await socket.SendAsync(Encoding.UTF8.GetBytes(raw));
        return await ReadToEndAsync(socket);
    }

    public async Task<Socket> ConnectAsync()
    {
        var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
        await socket.ConnectAsync(new IPEndPoint(IPAddress.Loopback, Port));
        return socket;
    }

    public static async Task<string> ReadToEndAsync(Socket socket)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var buffer = new byte[4096];
        var received = new StringBuilder();
        try
        {
            while (true)
            {
                var read = await socket.ReceiveAsync(buffer, SocketFlags.None, timeout.Token);
                if (read == 0)
                    break;
                received.Append(Encoding.UTF8.GetString(buffer, 0, read));
            }
        }
        catch (SocketException e) when (e.SocketErrorCode == SocketError.ConnectionReset)
        {
        }

        return received.ToString();
    }

    public async ValueTask DisposeAsync()
    {
        await _cts.CancelAsync();
        await _run;
        _cts.Dispose();
    }
}
