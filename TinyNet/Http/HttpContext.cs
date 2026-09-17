namespace TinyNet.Http;

public class HttpContext
{
    public HttpRequest? Request;
    public HttpResponse? Response;
    public CancellationToken RequestAborted;

    public HttpContext(
        HttpRequest? request = null, HttpResponse? response = null, CancellationToken requestAborted = default)
    {
        Response = response;
        Request = request;
        RequestAborted = requestAborted;
    }
    
    
}