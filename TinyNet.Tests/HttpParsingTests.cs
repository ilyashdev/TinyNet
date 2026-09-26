using System.Text;
using TinyNet.Http;
using HttpParser = TinyNet.Http.Http;

namespace TinyNet.Tests;

public class HttpParsingTests
{
    [Fact]
    public void ParseHead_QueryValueContainingEquals_IsNotTruncated()
    {
        var request = HttpParser.ParseHead("GET /callback?token=abc== HTTP/1.1\r\nHost: localhost");

        Assert.Equal("/callback", request.Url);
        Assert.Equal("abc==", request.Query["token"]);
    }

    [Theory]
    [InlineData("{broken")]
    [InlineData("[1, 2]")]
    [InlineData("null")]
    public void ParseBody_JsonContentTypeWithInvalidObject_IsRejected(string body)
    {
        var headers = new Dictionary<string, string> { ["Content-Type"] = "application/json; charset=utf-8" };

        Assert.Throws<BadRequestException>(() => HttpParser.ParseBody(body, headers));
    }

    [Fact]
    public void ParseBody_OtherContentType_IsNotParsedAsJson()
    {
        var headers = new Dictionary<string, string> { ["Content-Type"] = "text/plain" };

        Assert.Null(HttpParser.ParseBody("{broken", headers));
    }

    [Fact]
    public void ToHttpResponse_ContentLength_IsMeasuredInBytes()
    {
        var response = new HttpResponse(200, "Привет");

        var raw = response.ToHttpResponse();

        var separator = raw.IndexOf("\r\n\r\n", StringComparison.Ordinal);
        var body = raw[(separator + 4)..];
        Assert.Equal("Привет", body);
        Assert.Contains($"Content-Length: {Encoding.UTF8.GetByteCount(body)}\r\n", raw);
    }
}