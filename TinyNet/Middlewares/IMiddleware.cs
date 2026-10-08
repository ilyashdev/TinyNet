using TinyNet.Http;

namespace TinyNet.Middlewares;

public interface IMiddleware
{
    Task<HttpResponse> InvokeAsync(HttpRequest request,HttpContext context, RequestDelegate next);

}