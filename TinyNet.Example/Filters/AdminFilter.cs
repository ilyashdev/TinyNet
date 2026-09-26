using TinyNet.ActionResult;
using TinyNet.Http;
using TinyNet.Middlewares;

namespace TinyNet.Example.Filters;

public class AdminFilter : IMiddleware
{
    public Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        if (context.GetFromHeader("X-Role") == "admin")
            return next(context);
        new BaseResult(403, "Only X-Role: admin may delete notes").ExecuteResult(context);
        return Task.CompletedTask;
    }
}