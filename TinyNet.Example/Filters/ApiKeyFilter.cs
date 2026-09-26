using TinyNet.ActionResult;
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

    public Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        if (context.GetFromHeader("X-Api-Key") == _apiKey)
            return next(context);

        // EN: Not calling next() short-circuits the request: the controller never runs.
        // RU: Без вызова next() запрос обрывается здесь, контроллер не выполняется.
        new BaseResult(401, "Missing or wrong X-Api-Key").ExecuteResult(context);
        return Task.CompletedTask;
    }
}