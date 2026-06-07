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
}