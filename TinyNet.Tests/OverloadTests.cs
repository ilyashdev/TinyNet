using System.Text;
using TinyNet.Application;

namespace TinyNet.Tests;

public class OverloadTests
{
    private const string SlowRequest = "GET /slow HTTP/1.1\r\nHost: localhost\r\n\r\n";
    private const string FastRequest = "GET /fast HTTP/1.1\r\nHost: localhost\r\nConnection: close\r\n\r\n";

    [Fact]
    public async Task WhenSlotsAndQueueAreFull_RequestIsRejectedWith503()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var server = Start(entered, release);

        try
        {
            using var busy = await server.ConnectAsync();
            await busy.SendAsync(Encoding.UTF8.GetBytes(SlowRequest));
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));

            var response = await server.SendRawAsync(FastRequest);

            Assert.StartsWith("HTTP/1.1 503", response);
        }
        finally
        {
            release.TrySetResult();
        }
    }

    [Fact]
    public async Task IdleKeepAliveConnection_DoesNotHoldASlot()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var server = Start(entered, release);

        using var idle = await server.ConnectAsync();
        await idle.SendAsync(Encoding.UTF8.GetBytes("GET /fast HTTP/1.1\r\nHost: localhost\r\n\r\n"));
        var buffer = new byte[1024];
        var read = await idle.ReceiveAsync(buffer).WaitAsync(TimeSpan.FromSeconds(10));
        Assert.StartsWith("HTTP/1.1 200", Encoding.UTF8.GetString(buffer, 0, read));

        var response = await server.SendRawAsync(FastRequest);

        Assert.StartsWith("HTTP/1.1 200", response);
    }

    private static TestServer Start(TaskCompletionSource entered, TaskCompletionSource release)
        => TestServer.Start(
            routes => routes
                .AddGet("/slow", async (_, context) =>
                {
                    entered.TrySetResult();
                    await release.Task;
                    return context.Response().Empty();
                })
                .AddGet("/fast", (_, context) => Task.FromResult(context.Response().Text("fast"))),
            app => app
                .AddDefault(FrameworkDefaults.ServerMaxConcurrentRequests, "1")
                .AddDefault(FrameworkDefaults.ServerMaxQueuedRequests, "0"));
}
