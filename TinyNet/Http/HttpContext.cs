using TinyNet.DI;

namespace TinyNet.Http;

public class HttpContext
{
    public HttpRequest Request { get; }
    public HttpResponse Response { get; }
    private DIScope _scope { get; }

    public HttpContext(HttpRequest request, HttpResponse response, DIScope scope)
    {
        Request = request;
        Response = response;
        _scope = scope;
    }
    public T GetService<T>() => _scope.GetService<T>();
    
}
