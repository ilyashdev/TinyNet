using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace TinyNet.Http;

public static class Http
{
    public static HttpRequest ParseRequest(string rawRequest)
        {
            if (string.IsNullOrEmpty(rawRequest))
                throw new ArgumentException("Raw request cannot be empty");

            var lines = rawRequest.Split("\r\n");
            if (lines.Length == 0)
                throw new FormatException("Empty request");

            var startLine = lines[0].Split(' ');
            if (startLine.Length < 3)
                throw new FormatException("Invalid start line");

            var method = startLine[0];
            var url = startLine[1];
            var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            int i = 1;
            for (; i < lines.Length; i++)
            {
                if (string.IsNullOrEmpty(lines[i])) break; // Конец заголовков
                
                var colonIndex = lines[i].IndexOf(':');
                if (colonIndex <= 0) continue;
                
                var key = lines[i].Substring(0, colonIndex).Trim();
                var value = lines[i].Substring(colonIndex + 1).Trim();
                headers[key] = value;
            }
            JsonObject body = null;
            if (i < lines.Length - 1)
            {
                var bodyContent = string.Join("\r\n", lines, i + 1, lines.Length - i - 1);
                if (!string.IsNullOrEmpty(bodyContent))
                {
                    try
                    {
                        body = JsonSerializer.Deserialize<JsonObject>(bodyContent);
                    }
                    catch
                    {
                    }
                }
            }
            var query = new Dictionary<string, string>();
            var queryIndex = url.IndexOf('?');
            if (queryIndex > 0)
            {
                var queryString = url.Substring(queryIndex + 1);
                foreach (var pair in queryString.Split('&'))
                {
                    var parts = pair.Split('=');
                    if (parts.Length == 0) continue;
                    
                    var key = Uri.UnescapeDataString(parts[0]);
                    var value = parts.Length > 1 ? Uri.UnescapeDataString(parts[1]) : "";
                    query[key] = value;
                }
                url = url.Substring(0, queryIndex);
            }

            return new HttpRequest(method, url, headers, query, body);
        }

    private static (string Method, string Path, string Query) ParseStartLine(string startLine)
    {
        var parts = startLine.Split(' ');
        if (parts.Length < 3) throw new FormatException("Invalid HTTP request format");
        if(!HttpMethod.IsAllowed(parts[0]))
            throw new FormatException($"Invalid HTTP method: {parts[0]}");
        var urlParts = parts[1].Split('?', 2);
        var path = urlParts[0];
        var query = urlParts.Length > 1 ? urlParts[1] : "";

        return (parts[0], path, query);
    }

    private static Dictionary<string, string> ParseHeaders(IEnumerable<string> headerLines)
    {
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var line in headerLines)
        {
            if (string.IsNullOrWhiteSpace(line)) break;

            var separatorIndex = line.IndexOf(':');
            if (separatorIndex < 0) continue;

            var key = line[..separatorIndex].Trim();
            var value = line[(separatorIndex + 1)..].Trim();
            headers[key] = value;
        }

        return headers;
    }

    private static Dictionary<string, string> ParseQuery(string queryString)
    {
        var query = new Dictionary<string, string>();
        if (string.IsNullOrWhiteSpace(queryString)) return query;

        var pairs = queryString.Split('&');
        foreach (var pair in pairs)
        {
            var parts = pair.Split('=', 2);
            var key = WebUtility.UrlDecode(parts[0]);
            var value = parts.Length > 1 ? WebUtility.UrlDecode(parts[1]) : "";
            query[key] = value;
        }

        return query;
    }

    private static JsonObject? ParseBody(string[] lines, Dictionary<string, string> headers)
    {
        var emptyLineIndex = Array.FindIndex(lines, string.IsNullOrWhiteSpace);
        if (emptyLineIndex < 0 || emptyLineIndex == lines.Length - 1) return null;

        var bodyLines = lines.Skip(emptyLineIndex + 1);
        var bodyContent = string.Join("\r\n", bodyLines.Where(l => l != null));

        if (string.IsNullOrWhiteSpace(bodyContent)) return null;
        if (headers.TryGetValue("Content-Type", out var contentType) &&
            !contentType.Contains("application/json", StringComparison.OrdinalIgnoreCase))
        {
            throw new NotSupportedException($"Unsupported content type: {contentType}");
        }

        try
        {
            return JsonNode.Parse(bodyContent) as JsonObject;
        }
        catch (JsonException ex)
        {
            throw new FormatException("Invalid JSON format in request body", ex);
        }
    }
}