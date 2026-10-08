using TinyNet.Http;

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

    public Task<HttpResponse> InvokeAsync(HttpRequest request, HttpContext context)
    {
        var match = _router.Match(request.Method, request.RawPath);
        if (request.Method == HttpMethods.HEAD.ToString() && match.Status != RouteStatus.Found)
        {
            var get = _router.Match(HttpMethods.GET.ToString(), request.RawPath);
            if (get.Status == RouteStatus.Found)
                match = get;
        }

        switch (match.Status)
        {
            case RouteStatus.Found:
                request.SetRoute(match.Values);
                return match.Endpoint!.InvokeAsync(request, context);
            case RouteStatus.MethodNotAllowed:
                return Task.FromResult(context.Response()
                    .Status(StatusCodes.MethodNotAllowed)
                    .AddHeader(HeaderNames.Allow, AllowHeader(match.AllowedMethods))
                    .Empty());
            default:
                return _fallback(request, context);
        }
    }

    private static string AllowHeader(IReadOnlyCollection<string> methods)
    {
        var get = HttpMethods.GET.ToString();
        var head = HttpMethods.HEAD.ToString();
        IEnumerable<string> allowed = methods.Contains(get) && !methods.Contains(head) ? methods.Append(head) : methods;
        return string.Join(", ", allowed);
    }
}