using System.Text;
using System.Text.RegularExpressions;
using TinyNet.Application;
using TinyNet.Http;
using TinyNet.Routing;

namespace TinyNet.Tests;

public class RequestReadingTests
{
    [Fact]
    public async Task PipelinedRequests_OnOneConnection_AreAnsweredInOrder()
    {
        await using var server = TestServer.Start(Routes);

        var response = await server.SendRawAsync(
            "POST /ignore HTTP/1.1\r\nHost: localhost\r\nContent-Length: 5\r\n\r\nfirst" +
            "POST /echo HTTP/1.1\r\nHost: localhost\r\nContent-Length: 6\r\n\r\nsecond" +
            "GET /echo HTTP/1.1\r\nHost: localhost\r\nConnection: close\r\n\r\n");

        Assert.Equal(["200", "200", "200"], Statuses(response));
        Assert.EndsWith("\r\n\r\n", response);
        Assert.Contains("\r\n\r\nignored", response);
        Assert.Contains("\r\n\r\nsecond", response);
    }

    [Fact]
    public async Task ChunkedBody_IsReadToTheEnd()
    {
        await using var server = TestServer.Start(Routes);

        var response = await server.SendRawAsync(
            "POST /echo HTTP/1.1\r\nHost: localhost\r\nTransfer-Encoding: chunked\r\nConnection: close\r\n\r\n" +
            "5\r\nhello\r\n6\r\n world\r\n0\r\n\r\n");

        Assert.StartsWith("HTTP/1.1 200", response);
        Assert.EndsWith("\r\n\r\nhello world", response);
    }

    [Fact]
    public async Task ChunkedBodyOverLimit_IsAnswered413()
    {
        await using var server = TestServer.Start(Routes,
            app => app.AddDefault(FrameworkDefaults.ServerMaxBodyBytes, "8"));

        var response = await server.SendRawAsync(
            "POST /echo HTTP/1.1\r\nHost: localhost\r\nTransfer-Encoding: chunked\r\n\r\n" +
            "5\r\nhello\r\n6\r\n world\r\n0\r\n\r\n");

        Assert.StartsWith("HTTP/1.1 413", response);
    }

    [Fact]
    public async Task UnfinishedHead_IsAnswered408()
    {
        await using var server = TestServer.Start(Routes,
            app => app.AddDefault(FrameworkDefaults.ServerHeadersTimeoutSeconds, "1"));

        var response = await server.SendRawAsync("GET /echo HTTP/1.1\r\nHost: local");

        Assert.StartsWith("HTTP/1.1 408", response);
    }

    private static void Routes(GroupRoute routes) => routes
        .AddPost("/ignore", (_, context) => Task.FromResult(context.Response().Text("ignored")))
        .AddPost("/echo", Echo)
        .AddGet("/echo", Echo);

    private static async Task<HttpResponse> Echo(HttpRequest request, HttpContext context)
        => context.Response().Text(Encoding.UTF8.GetString((await request.BufferBodyAsync()).Span));

    private static string[] Statuses(string response)
        => Regex.Matches(response, @"HTTP/1\.1 (\d{3})").Select(m => m.Groups[1].Value).ToArray();
}
