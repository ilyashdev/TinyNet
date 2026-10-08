using System.Buffers;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using TinyNet.Http;

namespace TinyNet.Protocols.Http1;

internal sealed class HeadParser
{
    public const int MaxRequestTargetLength = 8 * 1024;
    public const int MaxHeaderCount = 100;

    private const string Http10 = "HTTP/1.0";
    private const string Http11 = "HTTP/1.1";
    private const string Unreserved = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-._~";
    private const string SubDelims = "!$&'()*+,;=";

    private static readonly SearchValues<char> TokenChars =
        SearchValues.Create("!#$%&'*+-.^_`|~0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz");

    private static readonly SearchValues<char> FieldValueChars =
        SearchValues.Create(string.Concat(Enumerable.Range(0, 256)
            .Where(c => c == '\t' || c is >= 0x20 and not 0x7F).Select(c => (char)c)));

    private static readonly SearchValues<char> PathChars = SearchValues.Create(Unreserved + SubDelims + ":@/%");
    private static readonly SearchValues<char> QueryChars = SearchValues.Create(Unreserved + SubDelims + ":@/?%");
    private static readonly SearchValues<char> RegNameChars = SearchValues.Create(Unreserved + SubDelims + "%");
    private static readonly SearchValues<char> IpFutureChars = SearchValues.Create(Unreserved + SubDelims + ":");
    private static readonly SearchValues<char> HexDigits = SearchValues.Create("0123456789ABCDEFabcdef");

    private readonly long _maxBodyLength;
    private readonly int _maxHeadLength;
    private long _scanned;

    public HeadParser(long maxBodyLength, int maxHeadLength)
    {
        _maxBodyLength = maxBodyLength;
        _maxHeadLength = maxHeadLength;
    }

    public HeadParseResult TryParse(ref ReadOnlySequence<byte> buffer)
    {
        var reader = new SequenceReader<byte>(buffer);
        while (reader.IsNext("\r\n"u8, advancePast: true))
        {
        }

        var headStart = reader.Position;
        var skipped = reader.Consumed;
        reader.Advance(Math.Max(0, _scanned - 3 - skipped));

        if (!reader.TryReadTo(out ReadOnlySequence<byte> _, "\r\n\r\n"u8))
        {
            var start = Math.Max(skipped, _scanned);
            _scanned = buffer.Length;
            if (HasBareLineFeed(buffer, start, skipped))
                return HeadParseResult.Rejected(400, "Bare LF is not a valid line terminator.");
            return buffer.Length > _maxHeadLength
                ? HeadParseResult.Rejected(431, "Request header section is too large.")
                : HeadParseResult.Incomplete;
        }

        _scanned = 0;
        var raw = buffer.Slice(headStart, reader.Position);
        if (raw.Length - 4 > _maxHeadLength)
            return HeadParseResult.Rejected(431, "Request header section is too large.");

        try
        {
            var head = Parse(Encoding.Latin1.GetString(raw.Slice(0, raw.Length - 4)));
            buffer = buffer.Slice(reader.Position);
            return HeadParseResult.Complete(head);
        }
        catch (Rejection rejection)
        {
            return HeadParseResult.Rejected(rejection.StatusCode, rejection.Message);
        }
    }

    private static bool HasBareLineFeed(ReadOnlySequence<byte> buffer, long start, long headStart)
    {
        var previous = start > headStart ? buffer.Slice(start - 1, 1).FirstSpan[0] : (byte)0;
        var reader = new SequenceReader<byte>(buffer.Slice(start));
        while (reader.TryRead(out var b))
        {
            if (b == (byte)'\n' && previous != (byte)'\r')
                return true;
            previous = b;
        }

        return false;
    }

    private RequestHead Parse(string text)
    {
        var lines = text.Split("\r\n");
        if (lines.Length - 1 > MaxHeaderCount)
            throw new Rejection(431, "Too many header fields.");

        var (method, target, protocol) = ParseRequestLine(lines[0]);

        var headers = new HttpHeaders();
        var hostCount = 0;
        for (var i = 1; i < lines.Length; i++)
        {
            var name = ParseFieldLine(lines[i], headers);
            if (name.Equals(HeaderNames.Host, StringComparison.OrdinalIgnoreCase))
                hostCount++;
        }

        if (hostCount > 1)
            throw BadRequest("Multiple Host header fields.");
        if (protocol == Http11 && hostCount == 0)
            throw BadRequest("Host header field is required in HTTP/1.1.");
        var host = headers.GetOrNull(HeaderNames.Host);
        if (host is not null && !IsValidAuthority(host, requirePort: false))
            throw BadRequest("Invalid Host header field.");

        var (rawPath, query, targetAuthority) = ParseTarget(method, target);
        var (contentLength, isChunked) = ParseFraming(headers, protocol);

        var connection = SplitList(headers.GetOrNull(HeaderNames.Connection));
        var keepAlive = protocol == Http11
            ? !connection.Contains("close", StringComparer.OrdinalIgnoreCase)
            : connection.Contains("keep-alive", StringComparer.OrdinalIgnoreCase);

        var expectContinue = false;
        if (protocol == Http11)
        {
            foreach (var expectation in SplitList(headers.GetOrNull(HeaderNames.Expect)))
            {
                if (!expectation.Equals("100-continue", StringComparison.OrdinalIgnoreCase))
                    throw new Rejection(417, $"Expectation '{expectation}' is not supported.");
                expectContinue = true;
            }
        }

        return new RequestHead(method, PercentDecode(rawPath, plusAsSpace: false), rawPath, protocol,
            targetAuthority ?? host, headers, ParseQuery(query), contentLength, isChunked, keepAlive, expectContinue);
    }

    private static (string Method, string Target, string Protocol) ParseRequestLine(string line)
    {
        var first = line.IndexOf(' ');
        var last = line.LastIndexOf(' ');
        if (first <= 0 || last == first)
            throw BadRequest("Malformed request line.");

        var method = line[..first];
        var target = line[(first + 1)..last];
        var version = line[(last + 1)..];
        if (!IsToken(method))
            throw BadRequest("Malformed request method.");
        if (target.Length > MaxRequestTargetLength)
            throw new Rejection(414, "Request target is too long.");
        if (target.Length == 0)
            throw BadRequest("Malformed request target.");

        if (version.Length != 8
            || !version.StartsWith("HTTP/", StringComparison.Ordinal)
            || !char.IsAsciiDigit(version[5])
            || version[6] != '.'
            || !char.IsAsciiDigit(version[7]))
            throw BadRequest("Malformed HTTP version.");
        if (version[5] != '1')
            throw new Rejection(505, $"HTTP version '{version}' is not supported.");

        return (method, target, version[7] == '0' ? Http10 : Http11);
    }

    private static string ParseFieldLine(string line, HttpHeaders headers)
    {
        var colon = line.IndexOf(':');
        if (colon <= 0 || !IsToken(line.AsSpan(0, colon)))
            throw BadRequest("Malformed header field.");

        var name = line[..colon];
        var value = line.AsSpan(colon + 1).Trim(" \t");
        if (value.ContainsAnyExcept(FieldValueChars))
            throw BadRequest($"Invalid character in header field '{name}'.");

        headers.Add(name, value.ToString());
        return name;
    }

    private static (string RawPath, string? Query, string? Authority) ParseTarget(string method, string target)
    {
        if (method == "CONNECT")
        {
            if (!IsValidAuthority(target, requirePort: true))
                throw BadRequest("CONNECT requires an authority-form request target.");
            return ("", null, target);
        }

        if (target == "*")
        {
            if (method != "OPTIONS")
                throw BadRequest("Asterisk-form is only allowed for OPTIONS.");
            return ("*", null, null);
        }

        if (target[0] == '/')
        {
            var (path, query) = ParseOriginForm(target);
            return (path, query, null);
        }

        var schemeEnd = target.IndexOf("://", StringComparison.Ordinal);
        if (schemeEnd <= 0)
            throw BadRequest("Malformed request target.");
        var scheme = target[..schemeEnd];
        if (!scheme.Equals("http", StringComparison.OrdinalIgnoreCase)
            && !scheme.Equals("https", StringComparison.OrdinalIgnoreCase))
            throw BadRequest($"Scheme '{scheme}' is not supported.");

        var rest = target[(schemeEnd + 3)..];
        var pathStart = rest.IndexOfAny(['/', '?']);
        var authority = pathStart < 0 ? rest : rest[..pathStart];
        if (authority.Contains('@'))
            throw BadRequest("Userinfo is not allowed in the request target.");
        if (!IsValidAuthority(authority, requirePort: false))
            throw BadRequest("Invalid authority in the request target.");

        var pathAndQuery = pathStart < 0 ? "/" : rest[pathStart..];
        if (pathAndQuery[0] == '?')
            pathAndQuery = "/" + pathAndQuery;
        var (absolutePath, absoluteQuery) = ParseOriginForm(pathAndQuery);
        return (absolutePath, absoluteQuery, authority);
    }

    private static (string Path, string? Query) ParseOriginForm(string target)
    {
        var question = target.IndexOf('?');
        var path = question < 0 ? target : target[..question];
        var query = question < 0 ? null : target[(question + 1)..];

        if (path.AsSpan().ContainsAnyExcept(PathChars) || !IsPercentEncodingValid(path))
            throw BadRequest("Invalid character in request path.");
        if (query is not null && (query.AsSpan().ContainsAnyExcept(QueryChars) || !IsPercentEncodingValid(query)))
            throw BadRequest("Invalid character in request query.");

        return (NormalizePath(path), query);
    }

    private (long? ContentLength, bool IsChunked) ParseFraming(HttpHeaders headers, string protocol)
    {
        var transferEncoding = headers.GetOrNull(HeaderNames.TransferEncoding);
        var contentLength = headers.GetOrNull(HeaderNames.ContentLength);

        if (transferEncoding is not null)
        {
            if (protocol == Http10)
                throw BadRequest("Transfer-Encoding is not allowed in HTTP/1.0.");
            if (contentLength is not null)
                throw BadRequest("Both Transfer-Encoding and Content-Length are present.");

            var codings = SplitList(transferEncoding);
            if (codings.Count == 0 || !codings[^1].Equals("chunked", StringComparison.OrdinalIgnoreCase))
                throw BadRequest("chunked must be the final transfer coding.");
            if (codings.Count > 1)
                throw codings.Take(codings.Count - 1).Any(c => c.Equals("chunked", StringComparison.OrdinalIgnoreCase))
                    ? BadRequest("chunked is applied more than once.")
                    : new Rejection(501, $"Transfer-Encoding '{transferEncoding}' is not supported.");
            return (null, true);
        }

        if (contentLength is null)
            return (null, false);

        long? length = null;
        foreach (var item in contentLength.Split(','))
        {
            var digits = item.AsSpan().Trim(" \t");
            if (digits.IsEmpty || digits.ContainsAnyExceptInRange('0', '9'))
                throw BadRequest("Invalid Content-Length.");
            if (!long.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out var value))
                throw new Rejection(413, "Request body is too large.");
            if (length is not null && length != value)
                throw BadRequest("Conflicting Content-Length values.");
            length = value;
        }

        if (length > _maxBodyLength)
            throw new Rejection(413, "Request body is too large.");
        return (length, false);
    }

    private static bool IsValidAuthority(string value, bool requirePort)
    {
        string port;
        if (value.StartsWith('['))
        {
            var close = value.IndexOf(']');
            if (close < 0 || !IsValidIpLiteral(value[1..close]))
                return false;
            port = value[(close + 1)..];
        }
        else
        {
            var colon = value.LastIndexOf(':');
            var host = colon < 0 ? value : value[..colon];
            if (host.Length == 0 || host.AsSpan().ContainsAnyExcept(RegNameChars) || !IsPercentEncodingValid(host))
                return false;
            port = colon < 0 ? "" : value[colon..];
        }

        if (port.Length == 0)
            return !requirePort;
        return port[0] == ':'
               && !port.AsSpan(1).ContainsAnyExceptInRange('0', '9')
               && (!requirePort || port.Length > 1);
    }

    private static bool IsValidIpLiteral(string literal)
    {
        if (literal.Length > 0 && (literal[0] == 'v' || literal[0] == 'V'))
        {
            var dot = literal.IndexOf('.');
            return dot > 1
                   && dot < literal.Length - 1
                   && !literal.AsSpan(1, dot - 1).ContainsAnyExcept(HexDigits)
                   &&!literal.AsSpan(dot + 1).ContainsAnyExcept(IpFutureChars);
        }

        return !literal.Contains('%')
               && IPAddress.TryParse(literal, out var address)
               && address.AddressFamily == AddressFamily.InterNetworkV6;
    }

    private static bool IsPercentEncodingValid(ReadOnlySpan<char> s)
    {
        for (var i = s.IndexOf('%'); i >= 0; i = s.IndexOf('%'))
        {
            if (i + 2 >= s.Length || !char.IsAsciiHexDigit(s[i + 1]) || !char.IsAsciiHexDigit(s[i + 2]))
                return false;
            s = s[(i + 3)..];
        }

        return true;
    }

    private static string NormalizePath(string path)
    {
        if (path.Contains('%'))
        {
            var builder = new StringBuilder(path.Length);
            for (var i = 0; i < path.Length; i++)
            {
                if (path[i] != '%')
                {
                    builder.Append(path[i]);
                    continue;
                }

                var c = (char)byte.Parse(path.AsSpan(i + 1, 2), NumberStyles.AllowHexSpecifier);
                if (Unreserved.Contains(c))
                    builder.Append(c);
                else
                    builder.Append('%').Append(char.ToUpperInvariant(path[i + 1])).Append(char.ToUpperInvariant(path[i + 2]));
                i += 2;
            }

            path = builder.ToString();
        }

        return path.Contains("/.") ? RemoveDotSegments(path) : path;
    }

    private static string RemoveDotSegments(string path)
    {
        var segments = path.Split('/');
        var output = new List<string>(segments.Length);
        for (var i = 1; i < segments.Length; i++)
        {
            var segment = segments[i];
            var isLast = i == segments.Length - 1;
            if (segment == "..")
            {
                if (output.Count > 0)
                    output.RemoveAt(output.Count - 1);
                if (isLast)
                    output.Add("");
            }
            else if (segment == ".")
            {
                if (isLast)
                    output.Add("");
            }
            else
                output.Add(segment);
        }

        return "/" + string.Join('/', output);
    }

    private static Dictionary<string, string> ParseQuery(string? query)
    {
        var result = new Dictionary<string, string>();
        if (query is null)
            return result;

        foreach (var pair in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var eq = pair.IndexOf('=');
            var key = PercentDecode(eq < 0 ? pair : pair[..eq], plusAsSpace: true);
            var value = eq < 0 ? "" : PercentDecode(pair[(eq + 1)..], plusAsSpace: true);
            result.TryAdd(key, value);
        }

        return result;
    }

    private static string PercentDecode(string s, bool plusAsSpace)
    {
        if (!s.Contains('%') && !(plusAsSpace && s.Contains('+')))
            return s;

        var bytes = new byte[s.Length];
        var count = 0;
        for (var i = 0; i < s.Length; i++)
        {
            var c = s[i];
            if (c == '%')
            {
                bytes[count++] = byte.Parse(s.AsSpan(i + 1, 2), NumberStyles.AllowHexSpecifier);
                i += 2;
            }
            else
                bytes[count++] = c == '+' && plusAsSpace ? (byte)' ' : (byte)c;
        }

        return Encoding.UTF8.GetString(bytes, 0, count);
    }

    private static List<string> SplitList(string? value)
    {
        if (value is null)
            return [];
        return value.Split(',')
            .Select(item => item.Trim(' ', '\t'))
            .Where(item => item.Length > 0)
            .ToList();
    }

    private static bool IsToken(ReadOnlySpan<char> s) => !s.IsEmpty && !s.ContainsAnyExcept(TokenChars);

    private static Rejection BadRequest(string reason) => new(400, reason);

    private sealed class Rejection(int statusCode, string reason) : Exception(reason)
    {
        public int StatusCode { get; } = statusCode;
    }
}