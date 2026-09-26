using TinyNet.ActionResult;
using TinyNet.Http;
using TinyNet.Middlewares;

namespace TinyNet.Routing;

internal sealed class RouteDispatcher
{
    private readonly UrlRouter _router;
    private readonly RequestDelegate _fallback;

    public RouteDispatcher(UrlRouter router, RequestDelegate fallback)
    {
        _router = router;
        _fallback = fallback;
    }

    public Task InvokeAsync(HttpContext context)
    {
        var request = context.Request;
        var match = _router.Match(request.Method, request.Url);
        switch (match.Status)
        {
            case RouteStatus.Found:
                request.Route = match.Values;
                return match.Endpoint!.InvokeAsync(context);
            case RouteStatus.MethodNotAllowed:
                new BaseResult(405).ExecuteResult(context);
                return Task.CompletedTask;
            default:
                return _fallback(context);
        }
    }
}