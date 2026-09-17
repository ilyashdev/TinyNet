using System.Net;
using System.Net.Sockets;
using System.Text;
using TinyNet.Application;

namespace TinyNet.Tests;

public class OverloadTests
{
    private const string SlowRequest = "GET /slow HTTP/1.1\r\nHost: localhost\r\n\r\n";

    [Fact]
    public async Task WhenQueueIsFull_ConnectionIsRejectedWith503()
    {
        SlowController.Reset();

        var builder = new AppBuilder();
        builder.AddDefault(FrameworkDefaults.ServerPort, "0");
        builder.AddDefault(FrameworkDefaults.ServerMaxConcurrentRequests, "1");
        builder.AddDefault(FrameworkDefaults.ServerMaxQueuedConnections, "1");
        var app = builder.Build();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        var run = app.Run(cts.Token);

        try
        {
            using var busy = await SendAsync(app.Port);
            await SlowController.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));

            using var queued = await SendAsync(app.Port);
            using var rejected = await SendAsync(app.Port);

            var response = await ReadToEndAsync(rejected);

            Assert.StartsWith("HTTP/1.1 503", response);
        }
        finally
        {
            SlowController.Release.TrySetResult();
            await cts.CancelAsync();
            await run;
        }
    }

    private static async Task<Socket> SendAsync(int port)
    {
        var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)
        {
            NoDelay = true
        };
        await socket.ConnectAsync(new IPEndPoint(IPAddress.Loopback, port));
        await socket.SendAsync(Encoding.UTF8.GetBytes(SlowRequest));
        return socket;
    }

    private static async Task<string> ReadToEndAsync(Socket socket)
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
        catch (SocketException ex) when (ex.SocketErrorCode == SocketError.ConnectionReset)
        {
        }

        return received.ToString();
    }
}