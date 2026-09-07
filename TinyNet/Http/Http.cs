using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace TinyNet.Http;

public static class Http
{
    public const string HeadSeparator = "\r\n\r\n";

    public static HttpRequest ParseRequest(string rawRequest)
    {
        if (string.IsNullOrEmpty(rawRequest))
            throw new BadRequestException("Raw request cannot be empty");

        var separator = rawRequest.IndexOf(HeadSeparator, StringComparison.Ordinal);
        var headText = separator < 0 ? rawRequest : rawRequest.Substring(0, separator);
        var bodyText = separator < 0 ? null : rawRequest.Substring(separator + HeadSeparator.Length);

        var request = ParseHead(headText);
        request.Body = ParseBody(bodyText);
        return request;
    }

    public static HttpRequest ParseHead(string headText)
    {
        if (string.IsNullOrEmpty(headText))
            throw new BadRequestException("Request head cannot be empty");

        var lines = headText.Split("\r\n");

        var startLine = lines[0].Split(' ');
        if (startLine.Length < 3)
            throw new BadRequestException($"Invalid start line: {lines[0]}");

        var method = startLine[0];
        var url = startLine[1];

        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 1; i < lines.Length; i++)
        {
            if (string.IsNullOrEmpty(lines[i]))
                continue;

            var colonIndex = lines[i].IndexOf(':');
            if (colonIndex <= 0)
                continue;

            var key = lines[i].Substring(0, colonIndex).Trim();
            var value = lines[i].Substring(colonIndex + 1).Trim();
            headers[key] = value;
        }

        var query = new Dictionary<string, string>();
        var queryIndex = url.IndexOf('?');
        if (queryIndex > 0)
        {
            foreach (var pair in url.Substring(queryIndex + 1).Split('&'))
            {
                if (pair.Length == 0)
                    continue;

                var parts = pair.Split('=');
                var key = Uri.UnescapeDataString(parts[0]);
                query[key] = parts.Length > 1 ? Uri.UnescapeDataString(parts[1]) : "";
            }

            url = url.Substring(0, queryIndex);
        }

        return new HttpRequest(method, url, headers, query, null);
    }

    public static JsonObject ParseBody(string bodyText)
    {
        if (string.IsNullOrWhiteSpace(bodyText))
            return null;

        try
        {
            return JsonSerializer.Deserialize<JsonObject>(bodyText);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public static bool IsChunked(IDictionary<string, string> headers)
        => headers.TryGetValue("Transfer-Encoding", out var encoding)
           && encoding.Contains("chunked", StringComparison.OrdinalIgnoreCase);

    public static bool TryGetContentLength(IDictionary<string, string> headers, out int length)
    {
        length = 0;
        if (!headers.TryGetValue("Content-Length", out var raw))
            return false;

        if (!int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out length) || length < 0)
            throw new BadRequestException($"Invalid Content-Length: {raw}");

        return true;
    }
}
