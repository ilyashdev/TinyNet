using TinyNet.Http;

namespace TinyNet.Middleware;

public interface IMiddleware
{
    Task InvokeAsync(HttpContext context, RequestDelegate next);
}