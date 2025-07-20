using System.Text;

namespace TinyNet.Http;

public class HttpResponse
{
    
    
    public int? StatusCode { get; set; }
    public Dictionary<string, string> Headers { get; set; }
    public string? Body { get; set; }
    
    private static readonly Dictionary<int, string> StatusTexts = new()
    {
        {200, "OK"},
        {201, "Created"},
        {202, "Accepted"},
        {204, "No Content"},
        {301, "Moved Permanently"},
        {302, "Found"},
        {400, "Bad Request"},
        {401, "Unauthorized"},
        {403, "Forbidden"},
        {404, "Not Found"},
        {405, "Method Not Allowed"},
        {500, "Internal Server Error"},
        {502, "Bad Gateway"},
        {503, "Service Unavailable"}
    };
    public HttpResponse(int statusCode, string body)
    {
        StatusCode = statusCode;
        Headers = new();
        Body = body;
    }

    public HttpResponse(int statusCode)
    {
        StatusCode = statusCode;
        Headers = new();
    }

    public string ToHttpResponse()
    {
        if (StatusCode == null)
            throw new InvalidOperationException("HTTP status code is required");
        string statusText = StatusTexts.TryGetValue(StatusCode.Value, out string text) 
            ? text 
            : "Unknown Status";
        
        var response = new StringBuilder();
        response.AppendLine($"HTTP/1.1 {StatusCode} {statusText}");
        if (!Headers.ContainsKey("Content-Length"))
        {
            int length = Body?.Length > 0
                ? Encoding.UTF8.GetByteCount(Body)
                : 0;
            Headers["Content-Length"] = length.ToString();
        }

        foreach (var header in Headers)
        {
            response.AppendLine($"{header.Key}: {header.Value}");
        }
        response.AppendLine();
        if (!string.IsNullOrEmpty(Body))
        {
            response.Append(Body);
        }
        return response.ToString();
    }
}