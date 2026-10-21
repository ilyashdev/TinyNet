using System.Globalization;
using TinyNet.Exceptions;
using TinyNet.Http;

namespace TinyNet.Protocols.Http1;

internal sealed class RequestHeadBuilder
{
    private readonly long _maxBodyLength;
    private RequestHead _head = Create();
    private int _hostCount;

    public RequestHeadBuilder(long maxBodyLength)
    {
        _maxBodyLength = maxBodyLength;
    }

    public void SetRequestLine(HeadParser parser)
    {
        _head.Method = parser.Method;
        _head.Protocol = parser.Protocol;
        _head.RawPath = parser.RawPath;
        _head.Path = parser.Path;
        _head.Query = parser.Query;
        _head.Authority = parser.Authority;
    }

    public void AddHeader(string name, string value)
    {
        _head.Headers.Add(name, value);
        if (name.Equals(HeaderNames.Host, StringComparison.OrdinalIgnoreCase))
            _hostCount++;
    }

    public RequestHead Build()
    {
        var head = _head;
        var hostCount = _hostCount;
        _head = Create();
        _hostCount = 0;

        var headers = head.Headers;
        var http11 = head.Protocol == HttpProtocols.Http11;

        var host = headers.GetOrNull(HeaderNames.Host);
        if (hostCount > 1 || http11 && hostCount == 0)
            throw new ProtocolException("Exactly one Host header field is required.", StatusCodes.BadRequest);
        if (host is not null && !HeadParser.IsValidHost(host))
            throw new ProtocolException("Invalid Host header field.", StatusCodes.BadRequest);
        head.Authority ??= host;

        var transferEncoding = headers.GetOrNull(HeaderNames.TransferEncoding);
        var contentLength = headers.GetOrNull(HeaderNames.ContentLength);
        if (transferEncoding is not null)
        {
            if (!http11 || contentLength is not null)
                throw new ProtocolException("Transfer-Encoding is not allowed with HTTP/1.0 or Content-Length.",
                    StatusCodes.BadRequest);
            var codings = transferEncoding.Split(',', StringSplitOptions.TrimEntries);
            if (!codings[^1].Equals("chunked", StringComparison.OrdinalIgnoreCase))
                throw new ProtocolException("chunked must be the final transfer coding.", StatusCodes.BadRequest);
            if (codings.Length > 1)
                throw new ProtocolException($"Transfer-Encoding '{transferEncoding}' is not supported.",
                    StatusCodes.NotImplemented);
            head.IsChunked = true;
        }
        else if (contentLength is not null)
        {
            if (!long.TryParse(contentLength, NumberStyles.None, CultureInfo.InvariantCulture, out var length))
                throw new ProtocolException("Invalid Content-Length.", StatusCodes.BadRequest);
            if (length > _maxBodyLength)
                throw new ProtocolException("Request body is too large.", StatusCodes.RequestEntityTooLarge);
            head.ContentLength = length;
        }

        var connection = (headers.GetOrNull(HeaderNames.Connection) ?? "").Split(',', StringSplitOptions.TrimEntries);
        head.KeepAlive = http11
            ? !connection.Contains("close", StringComparer.OrdinalIgnoreCase)
            : connection.Contains("keep-alive", StringComparer.OrdinalIgnoreCase);

        var expect = http11 ? headers.GetOrNull(HeaderNames.Expect) : null;
        if (expect is not null && !expect.Equals("100-continue", StringComparison.OrdinalIgnoreCase))
            throw new ProtocolException($"Expectation '{expect}' is not supported.", StatusCodes.ExpectationFailed);
        head.ExpectContinue = expect is not null;

        return head;
    }

    private static RequestHead Create() => new() { Headers = new HttpHeaders() };
}