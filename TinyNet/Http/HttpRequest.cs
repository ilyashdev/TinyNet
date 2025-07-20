using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace TinyNet.Http;

public class HttpRequest
{
    public string Method {get; set;}
    public string Url {get; set;}
    public Dictionary<string, string> Headers {get; set;}
    public Dictionary<string, string> Query {get; set;}
    public JsonObject Body {get; set;}
    public HttpRequest(string method,
        string url,
        Dictionary<string, string> headers,
        Dictionary<string, string> query,
        JsonObject body)
    {
        Method = method;
        Url = url;
        Headers = headers;
        Query = query;
        Body = body;
    }
}

