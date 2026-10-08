using TinyNet.Http;
using TinyNet.Middlewares;

namespace TinyNet.Example.Filters;

public class AdminFilter : IMiddleware
{
    public Task<HttpResponse> InvokeAsync(HttpRequest request, HttpContext context, RequestDelegate next)
    {
        if (request.GetFromHeaders("X-Role") == "admin")
            return next(request, context);
        return Task.FromResult(context.Response()
            .Status(StatusCodes.Forbidden)
            .Text("Only X-Role: admin may delete notes"));
    }
}
