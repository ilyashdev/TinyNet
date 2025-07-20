using System.Reflection;
using TinyNet.Controllers;

namespace TinyNet.Http;

public class HttpHandler
{
    private ControllerHandler _controllers;
    public HttpHandler(ControllerHandler controllers)
    {
        _controllers = controllers;
    }

    public void HttpHandle(HttpRequest request)
    {
        
    }
    
}