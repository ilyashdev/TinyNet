using System.Buffers;
using System.Text;
using System.Text.Json;
using TinyNet.Http;
using TinyNet.Protocols.Http1;

namespace TinyNet.Tests;

public class HttpParsingTests
{
    private const long MaxBodyLength = 1024;
    private const int MaxHeadLength = 32 * 1024;

    [Fact]
    public void QueryValueContainingEquals_IsNotTruncated()
    {
        var result = Parse("GET /callback?token=abc== HTTP/1.1\r\nHost: localhost\r\n\r\n");

        Assert.Equal(HeadParseStatus.Complete, result.Status);
        Assert.Equal("/callback", result.Head!.Path);
        Assert.Equal("abc==", result.Head.Query["token"]);
    }

    [Fact]
    public void HeadArrivingOneByteAtATime_IsParsedOnceComplete()
    {
        var raw = Encoding.Latin1.GetBytes("POST /echo?x=1 HTTP/1.1\r\nHost: localhost\r\nContent-Length: 4\r\n\r\nbody");
        var headLength = raw.Length - 4;
        var parser = new HeadParser(MaxBodyLength, MaxHeadLength);

        for (var received = 1; received < headLength; received++)
        {
            var partial = new ReadOnlySequence<byte>(raw, 0, received);
            Assert.Equal(HeadParseStatus.Incomplete, parser.TryParse(ref partial).Status);
        }

        var buffer = new ReadOnlySequence<byte>(raw);
        var result = parser.TryParse(ref buffer);

        Assert.Equal(HeadParseStatus.Complete, result.Status);
        Assert.Equal("1", result.Head!.Query["x"]);
        Assert.Equal(4, result.Head.ContentLength);
        Assert.Equal("body", Encoding.Latin1.GetString(buffer));
    }

    [Theory]
    [InlineData("Content-Length: 4\r\nContent-Length: 5\r\n", 400)]
    [InlineData("Content-Length: 4\r\nTransfer-Encoding: chunked\r\n", 400)]
    [InlineData("Host: other\r\n", 400)]
    [InlineData("X-Bad: a\nb\r\n", 400)]
    [InlineData("Content-Length: 1025\r\n", 413)]
    public void AmbiguousOrOversizedRequest_IsRejected(string headers, int status)
    {
        var result = Parse($"POST / HTTP/1.1\r\nHost: localhost\r\n{headers}\r\n");

        Assert.Equal(HeadParseStatus.Rejected, result.Status);
        Assert.Equal(status, result.StatusCode);
    }

    [Fact]
    public async Task ResponseContentLength_IsMeasuredInBytes()
    {
        var settings = new HttpSettings(new JsonSerializerOptions(JsonSerializerDefaults.Web), MaxBodyLength);
        var response = new ResponseBuilder(settings).Text("Привет");
        using var output = new MemoryStream();

        await ResponseWriter.WriteAsync(output, response, "HTTP/1.1", isHead: false, keepAlive: true,
            CancellationToken.None);

        var raw = Encoding.UTF8.GetString(output.ToArray());
        var body = raw[(raw.IndexOf("\r\n\r\n", StringComparison.Ordinal) + 4)..];
        Assert.Equal("Привет", body);
        Assert.Contains($"Content-Length: {Encoding.UTF8.GetByteCount(body)}\r\n", raw);
    }

    private static HeadParseResult Parse(string raw)
    {
        var buffer = new ReadOnlySequence<byte>(Encoding.Latin1.GetBytes(raw));
        return new HeadParser(MaxBodyLength, MaxHeadLength).TryParse(ref buffer);
    }
}
