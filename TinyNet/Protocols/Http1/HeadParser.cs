using System.Buffers;
using System.Text;
using TinyNet.Exceptions;
using TinyNet.Http;

namespace TinyNet.Protocols.Http1;

internal sealed class HeadParser
{
    public const int MaxRequestTargetLength = 8 * 1024;
    public const int MaxHeaderCount = 100;

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

    private readonly int _maxHeadLength;
    private bool _inHeaders;
    private long _length;
    private int _headerCount;

    public HeadParser(int maxHeadLength)
    {
        _maxHeadLength = maxHeadLength;
    }

    public string Method { get; private set; } = "";
    public string Protocol { get; private set; } = "";
    public string RawPath { get; private set; } = "";
    public string Path { get; private set; } = "";
    public Dictionary<string, string> Query { get; private set; } = [];
    public string? Authority { get; private set; }
    public string HeaderName { get; private set; } = "";
    public string HeaderValue { get; private set; } = "";

    public HeadPart Next(ref ReadOnlySequence<byte> buffer)
    {
        while (true)
        {
            var reader = new SequenceReader<byte>(buffer);
            if (!reader.TryReadTo(out ReadOnlySequence<byte> line, (byte)'\n'))
            {
                if (_length + buffer.Length > _maxHeadLength)
                    throw new ProtocolException("Request header section is too large.",
                        StatusCodes.RequestHeaderFieldsTooLarge);
                return HeadPart.NeedMore;
            }

            _length += reader.Consumed;
            if (_length > _maxHeadLength)
                throw new ProtocolException("Request header section is too large.",
                    StatusCodes.RequestHeaderFieldsTooLarge);
            if (line.IsEmpty || line.Slice(line.Length - 1).FirstSpan[0] != (byte)'\r')
                throw BadRequest("Bare LF is not a valid line terminator.");

            buffer = buffer.Slice(reader.Position);
            var text = Encoding.Latin1.GetString(line.Slice(0, line.Length - 1));

            if (!_inHeaders)
            {
                if (text.Length == 0)
                    continue;
                ParseRequestLine(text);
                _inHeaders = true;
                return HeadPart.RequestLine;
            }

            if (text.Length == 0)
            {
                _inHeaders = false;
                _length = 0;
                _headerCount = 0;
                return HeadPart.End;
            }

            if (++_headerCount > MaxHeaderCount)
                throw new ProtocolException("Too many header fields.", StatusCodes.RequestHeaderFieldsTooLarge);
            ParseHeader(text);
            return HeadPart.Header;
        }
    }

    public static bool IsValidHost(string host)
    {
        var colon = host.LastIndexOf(':');
        if (colon > host.LastIndexOf(']'))
        {
            if (host.AsSpan(colon + 1).ContainsAnyExceptInRange('0', '9'))
                return false;
            host = host[..colon];
        }

        return host.StartsWith('[')
            ? Uri.CheckHostName(host) == UriHostNameType.IPv6
            : host.Length > 0 && !host.AsSpan().ContainsAnyExcept(RegNameChars);
    }

    private void ParseRequestLine(string line)
    {
        var parts = line.Split(' ');
        if (parts.Length != 3 || !IsToken(parts[0]) || parts[1].Length == 0)
            throw BadRequest("Malformed request line.");
        if (parts[1].Length > MaxRequestTargetLength)
            throw new ProtocolException("Request target is too long.", StatusCodes.RequestUriTooLong);

        var version = parts[2];
        if (version.Length != 8 || !version.StartsWith("HTTP/", StringComparison.Ordinal)
            || !char.IsAsciiDigit(version[5]) || version[6] != '.' || !char.IsAsciiDigit(version[7]))
            throw BadRequest("Malformed HTTP version.");
        if (version[5] != '1')
            throw new ProtocolException($"HTTP version '{version}' is not supported.",
                StatusCodes.HttpVersionNotSupported);

        Method = parts[0];
        Protocol = version[7] == '0' ? HttpProtocols.Http10 : HttpProtocols.Http11;
        ParseTarget(parts[1]);
    }

    private void ParseTarget(string target)
    {
        if (Method == "CONNECT")
            throw new ProtocolException("CONNECT is not supported.", StatusCodes.NotImplemented);

        Authority = null;
        if (target == "*")
        {
            if (Method != "OPTIONS")
                throw BadRequest("Asterisk-form is only allowed for OPTIONS.");
            RawPath = "*";
            Path = "*";
            Query = [];
            return;
        }

        if (target[0] != '/')
        {
            var authorityStart = target.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ? 7
                : target.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ? 8
                : throw BadRequest("Malformed request target.");
            var pathStart = target.IndexOfAny(['/', '?'], authorityStart);
            Authority = pathStart < 0 ? target[authorityStart..] : target[authorityStart..pathStart];
            if (!IsValidHost(Authority))
                throw BadRequest("Invalid authority in the request target.");
            target = pathStart < 0 ? "/" : target[pathStart] == '?' ? "/" + target[pathStart..] : target[pathStart..];
        }

        var question = target.IndexOf('?');
        var rawPath = question < 0 ? target : target[..question];
        var query = question < 0 ? "" : target[(question + 1)..];
        if (rawPath.AsSpan().ContainsAnyExcept(PathChars) || !IsPercentEncodingValid(rawPath)
            || query.AsSpan().ContainsAnyExcept(QueryChars) || !IsPercentEncodingValid(query))
            throw BadRequest("Invalid character in request target.");

        var path = Uri.UnescapeDataString(rawPath);
        RawPath = rawPath;
        Path = path.Contains("/.") ? RemoveDotSegments(path) : path;
        Query = ParseQuery(query);
    }

    private void ParseHeader(string line)
    {
        var colon = line.IndexOf(':');
        if (colon <= 0 || !IsToken(line.AsSpan(0, colon)))
            throw BadRequest("Malformed header field.");

        var value = line.AsSpan(colon + 1).Trim(" \t");
        if (value.ContainsAnyExcept(FieldValueChars))
            throw BadRequest($"Invalid character in header field '{line[..colon]}'.");

        HeaderName = line[..colon];
        HeaderValue = value.ToString();
    }

    private static Dictionary<string, string> ParseQuery(string query)
    {
        var result = new Dictionary<string, string>();
        foreach (var pair in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var eq = pair.IndexOf('=');
            result.TryAdd(Decode(eq < 0 ? pair : pair[..eq]), eq < 0 ? "" : Decode(pair[(eq + 1)..]));
        }

        return result;

        static string Decode(string s) => Uri.UnescapeDataString(s.Replace('+', ' '));
    }

    private static string RemoveDotSegments(string path)
    {
        var segments = path.Split('/');
        var output = new List<string>(segments.Length);
        for (var i = 1; i < segments.Length; i++)
        {
            if (segments[i] == ".." && output.Count > 0)
                output.RemoveAt(output.Count - 1);
            if (segments[i] is not ("." or ".."))
                output.Add(segments[i]);
            else if (i == segments.Length - 1)
                output.Add("");
        }

        return "/" + string.Join('/', output);
    }

    private static bool IsPercentEncodingValid(string s)
    {
        for (var i = s.IndexOf('%'); i >= 0; i = s.IndexOf('%', i + 1))
            if (!Uri.IsHexEncoding(s, i))
                return false;
        return true;
    }

    private static bool IsToken(ReadOnlySpan<char> s) => !s.IsEmpty && !s.ContainsAnyExcept(TokenChars);

    private static ProtocolException BadRequest(string reason) => new(reason, StatusCodes.BadRequest);
}
