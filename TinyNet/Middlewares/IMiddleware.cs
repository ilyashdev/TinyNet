using TinyNet.Http;

namespace TinyNet.Middlewares;

public interface IMiddleware
{
    Task InvokeAsync(HttpContext context);
}