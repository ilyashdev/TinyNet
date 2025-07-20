namespace TinyNet.Http;

public class HttpContext
{
    public HttpRequest? Request;
    public HttpResponse? Response;

    public HttpContext(HttpRequest? request = null,HttpResponse? response = null)
    {
        Response = response;
        Request = request;
    }
    
    
}