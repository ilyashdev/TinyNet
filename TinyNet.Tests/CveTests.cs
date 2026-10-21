using System.Text;
using TinyNet.Application;
using TinyNet.Exceptions;
using TinyNet.Http;

namespace TinyNet.Tests;

public class CveTests
{
    private const string Post = "POST /echo HTTP/1.1\r\nHost: localhost\r\n";
    private const string Chunked = "Transfer-Encoding: chunked\r\n\r\n";
    private const string Smuggled = "GET /smuggled HTTP/1.1\r\nHost: localhost\r\n\r\n";

    [Theory]
    [InlineData("CVE-2025-55315", Post + Chunked + "2;a\nxx\r\n0\r\n\r\n" + Smuggled, 400)]
    [InlineData("CVE-2025-22871", Post + Chunked + "1\nx\r\n0\r\n\r\n" + Smuggled, 400)]
    [InlineData("CVE-2025-22871", Post + Chunked + "1\n0\r\n\r\n" + Smuggled, 400)]
    [InlineData("CVE-2022-32214", Post + "Transfer-Encoding: chunked\n\r\n0\r\n\r\n" + Smuggled, 400)]
    [InlineData("CVE-2023-30589", Post + "X: y\rTransfer-Encoding: chunked\r\nContent-Length: 50\r\n\r\n0\r\n\r\n" + Smuggled, 400)]
    [InlineData("CVE-2022-35256", Post + "Transfer-Encoding: chunked\r\r\nContent-Length: 50\r\n\r\n0\r\n\r\n" + Smuggled, 400)]
    [InlineData("CVE-2022-32215", Post + "Transfer-Encoding: identity\r\n chunked\r\nContent-Length: 50\r\n\r\n0\r\n\r\n" + Smuggled, 400)]
    [InlineData("CVE-2022-32213", Post + "Transfer-Encoding: chunked, identity\r\n\r\n0\r\n\r\n" + Smuggled, 400)]
    [InlineData("CVE-2019-16276", Post + "Transfer-Encoding : chunked\r\nContent-Length: 50\r\n\r\n0\r\n\r\n" + Smuggled, 400)]
    [InlineData("CVE-2020-7238", Post + " Transfer-Encoding: chunked\r\nContent-Length: 50\r\n\r\n0\r\n\r\n" + Smuggled, 400)]
    [InlineData("CVE-2019-20444", Post + "Transfer-Encoding\r\nContent-Length: 50\r\n\r\n0\r\n\r\n" + Smuggled, 400)]
    [InlineData("CVE-2019-20445", Post + "Content-Length: 0\r\nContent-Length: 45\r\n\r\n" + Smuggled, 400)]
    [InlineData("CVE-2019-20445", Post + "Content-Length: 5\r\nTransfer-Encoding: chunked\r\n\r\n0\r\n\r\n" + Smuggled, 400)]
    [InlineData("CVE-2023-40175", Post + "Content-Length: \r\n\r\n" + Smuggled, 400)]
    [InlineData("CVE-2023-40175", Post + Chunked + "0\r\n" + Smuggled, 400)]
    [InlineData("CVE-2013-2028", Post + Chunked + "FFFFFFFFFFFFFFF0\r\nx\r\n0\r\n\r\n" + Smuggled, 413)]
    [InlineData("CVE-2019-9900", Post + "X: a\0b\r\nContent-Length: 0\r\n\r\n" + Smuggled, 400)]
    public async Task SmugglingAttempt_IsRejected_AndNothingIsSmuggled(string cve, string request, int status)
    {
        var (response, smuggled) = await AttackAsync(request);

        Assert.True(response.StartsWith($"HTTP/1.1 {status} "), $"{cve}: {response}");
        Assert.True(smuggled == 0, $"{cve}: smuggled request reached the handler");
    }

    [Fact]
    public async Task ChunkExtensionFlood_Cve2024_21647_IsRejected()
    {
        var (response, _) = await AttackAsync(Post + Chunked + "1;" + new string('a', 16 * 1024) + "\r\nx\r\n0\r\n\r\n");

        Assert.StartsWith("HTTP/1.1 400 ", response);
    }

    [Fact]
    public async Task OversizedHead_Cve2018_12121_IsAnswered431()
    {
        var (response, _) = await AttackAsync(Post + "X: " + new string('a', 40 * 1024) + "\r\n\r\n");

        Assert.StartsWith("HTTP/1.1 431 ", response);
    }

    [Fact]
    public async Task SlowBody_Cve2007_6750_IsAnswered408()
    {
        var (response, _) = await AttackAsync(Post + "Content-Length: 10\r\n\r\nab",
            app => app.AddDefault(FrameworkDefaults.ServerBodyGracePeriodSeconds, "1"));

        Assert.StartsWith("HTTP/1.1 408 ", response);
    }

    [Theory]
    [InlineData("/.%2e/.%2e/etc/passwd")]
    [InlineData("/%2e%2e/%2e%2e/etc/passwd")]
    [InlineData("/static/../../etc/passwd")]
    public void DotSegments_Cve2021_41773_CannotEscapeRoot(string target)
    {
        var head = HttpParsingTests.Parse($"GET {target} HTTP/1.1\r\nHost: localhost\r\n\r\n");

        Assert.Equal("/etc/passwd", head.Path);
    }

    [Fact]
    public void DoubleEncodedDotSegments_Cve2021_42013_AreRejected()
    {
        var e = Assert.Throws<ProtocolException>(() =>
            HttpParsingTests.Parse("GET /.%%32%65/.%%32%65/etc/passwd HTTP/1.1\r\nHost: localhost\r\n\r\n"));

        Assert.Equal(400, e.StatusCode);
    }

    private static async Task<(string Response, int Smuggled)> AttackAsync(
        string request, Action<AppBuilder>? configure = null)
    {
        var smuggled = 0;
        await using var server = TestServer.Start(routes => routes
            .AddPost("/echo", Echo)
            .AddGet("/smuggled", (_, context) =>
            {
                Interlocked.Increment(ref smuggled);
                return Task.FromResult(context.Response().Text("smuggled"));
            })
            .AddGet("/ok", (_, context) => Task.FromResult(context.Response().Text("ok"))), configure);

        var response = await server.SendRawAsync(request);

        Assert.Equal(200, (await server.SendAsync("GET", "/ok")).Status);
        return (response, smuggled);
    }

    private static async Task<HttpResponse> Echo(HttpRequest request, HttpContext context)
        => context.Response().Text(Encoding.UTF8.GetString((await request.BufferBodyAsync()).Span));
}
