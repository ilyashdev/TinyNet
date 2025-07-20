using TinyNet.Http;
using TinyNet.Middleware;

namespace TinyNet.Test.Middleware;

public class TestMiddleware : IMiddleware
{
    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        
        await next.Invoke(context, next);
    }
}