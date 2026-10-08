using TinyNet.Configurations;
using TinyNet.Http;
using TinyNet.Middlewares;

namespace TinyNet.Example.Filters;

// EN: A filter is an ordinary IMiddleware attached to a route group or endpoint instead of the whole app.
//     Like any middleware it is a singleton, so the constructor may take only singletons such as IConfiguration.
// RU: Фильтр — обычный IMiddleware, навешанный на группу маршрутов или эндпоинт, а не на всё приложение.
//     Как и любой middleware, он синглтон, поэтому в конструктор можно брать только синглтоны вроде IConfiguration.
public class ApiKeyFilter : IMiddleware
{
    private readonly string _apiKey;

    public ApiKeyFilter(IConfiguration configuration)
    {
        _apiKey = configuration["Example:ApiKey"]
                  ?? throw new InvalidOperationException("Configuration key 'Example:ApiKey' is not set");
    }

    public Task<HttpResponse> InvokeAsync(HttpRequest request, HttpContext context, RequestDelegate next)
    {
        if (request.GetFromHeaders("X-Api-Key") == _apiKey)
            return next(request, context);

        // EN: Returning a response without calling next() short-circuits the request: the handler never runs.
        // RU: Если вернуть ответ без вызова next(), запрос обрывается здесь, обработчик не выполняется.
        return Task.FromResult(context.Response()
            .Status(StatusCodes.Unauthorized)
            .Text("Missing or wrong X-Api-Key"));
    }
}
