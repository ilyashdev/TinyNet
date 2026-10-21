using TinyNet.Http;
using TinyNet.Middlewares;

namespace TinyNet.Example.Filters;

// EN: A filter is an ordinary IMiddleware attached to a route group or endpoint instead of the whole app.
//     Like any middleware it is a singleton, shared by parallel requests.
// RU: Фильтр — обычный IMiddleware, навешанный на группу маршрутов или эндпоинт, а не на всё приложение.
//     Как и любой middleware, он синглтон и общий для параллельных запросов.
public class SameOriginFilter : IMiddleware
{
    public Task<HttpResponse> InvokeAsync(HttpRequest request, HttpContext context, RequestDelegate next)
    {
        var origin = request.GetFromHeaders("Origin");
        if (origin is null || origin == $"http://{request.GetFromHeaders(HeaderNames.Host)}")
            return next(request, context);

        // EN: Returning a response without calling next() short-circuits the request: the handler never runs.
        // RU: Если вернуть ответ без вызова next(), запрос обрывается здесь, обработчик не выполняется.
        return Task.FromResult(context.Response()
            .Status(StatusCodes.Forbidden)
            .Text("Forms from other sites are not accepted"));
    }
}