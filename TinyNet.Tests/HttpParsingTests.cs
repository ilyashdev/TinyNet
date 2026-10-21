using System.Buffers;
using System.Text;
using System.Text.Json;
using TinyNet.Exceptions;
using TinyNet.Http;
using TinyNet.Protocols;
using TinyNet.Protocols.Http1;

namespace TinyNet.Tests;

public class HttpParsingTests
{
    private const long MaxBodyLength = 1024;
    private const int MaxHeadLength = 32 * 1024;

    [Fact]
    public void QueryValueContainingEquals_IsNotTruncated()
    {
        var head = Parse("GET /callback?token=abc== HTTP/1.1\r\nHost: localhost\r\n\r\n");

        Assert.Equal("/callback", head.Path);
        Assert.Equal("abc==", head.Query["token"]);
    }

    [Fact]
    public void HeadArrivingOneByteAtATime_IsParsedOnceComplete()
    {
        var raw = Encoding.Latin1.GetBytes("POST /echo?x=1 HTTP/1.1\r\nHost: localhost\r\nContent-Length: 4\r\n\r\nbody");
        var headLength = raw.Length - 4;
        var parser = new HeadParser(MaxHeadLength);
        var builder = new RequestHeadBuilder(MaxBodyLength);
        var consumed = 0;
        RequestHead? head = null;

        for (var received = 1; head is null; received++)
        {
            var buffer = new ReadOnlySequence<byte>(raw, consumed, received - consumed);
            var part = Feed(parser, builder, ref buffer);
            consumed = received - (int)buffer.Length;
            if (part != HeadPart.End)
                continue;
            Assert.Equal(headLength, received);
            head = builder.Build();
        }

        Assert.Equal("1", head.Query["x"]);
        Assert.Equal(4, head.ContentLength);
        Assert.Equal("body", Encoding.Latin1.GetString(raw, consumed, raw.Length - consumed));
    }

    [Theory]
    [InlineData("Content-Length: 4\r\nContent-Length: 5\r\n", 400)]
    [InlineData("Content-Length: 4\r\nTransfer-Encoding: chunked\r\n", 400)]
    [InlineData("Host: other\r\n", 400)]
    [InlineData("X-Bad: a\n", 400)]
    [InlineData("Content-Length: 1025\r\n", 413)]
    public void AmbiguousOrOversizedRequest_IsRejected(string headers, int status)
    {
        var e = Assert.Throws<ProtocolException>(() => Parse($"POST / HTTP/1.1\r\nHost: localhost\r\n{headers}\r\n"));

        Assert.Equal(status, e.StatusCode);
    }

    [Fact]
    public async Task ResponseContentLength_IsMeasuredInBytes()
    {
        var settings = new HttpSettings(new JsonSerializerOptions(JsonSerializerDefaults.Web), MaxBodyLength);
        var response = new ResponseBuilder(settings).Text("Привет");
        using var output = new MemoryStream();

        await ResponseWriter.WriteAsync(output, response, HttpProtocols.Http11, isHead: false, keepAlive: true,
            CancellationToken.None);

        var raw = Encoding.UTF8.GetString(output.ToArray());
        var body = raw[(raw.IndexOf("\r\n\r\n", StringComparison.Ordinal) + 4)..];
        Assert.Equal("Привет", body);
        Assert.Contains($"Content-Length: {Encoding.UTF8.GetByteCount(body)}\r\n", raw);
    }

    internal static RequestHead Parse(string raw)
    {
        var parser = new HeadParser(MaxHeadLength);
        var builder = new RequestHeadBuilder(MaxBodyLength);
        var buffer = new ReadOnlySequence<byte>(Encoding.Latin1.GetBytes(raw));
        return Feed(parser, builder, ref buffer) == HeadPart.End
            ? builder.Build()
            : throw new InvalidOperationException("Head is incomplete.");
    }

    private static HeadPart Feed(HeadParser parser, RequestHeadBuilder builder, ref ReadOnlySequence<byte> buffer)
    {
        HeadPart part;
        while ((part = parser.Next(ref buffer)) is HeadPart.RequestLine or HeadPart.Header)
        {
            if (part == HeadPart.RequestLine)
                builder.SetRequestLine(parser);
            else
                builder.AddHeader(parser.HeaderName, parser.HeaderValue);
        }

        return part;
    }
}
