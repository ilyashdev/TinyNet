using TinyNet.Http;

namespace TinyNet.Middleware;

public abstract class Middleware : IMiddleware
{
    protected RequestDelegate _next;

    protected Middleware(RequestDelegate next)
    {
        _next = next;
    }

    public abstract Task InvokeAsync(HttpContext context);
}