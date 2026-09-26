using System.Diagnostics;
using System.Globalization;
using TinyNet.Example.Services;
using TinyNet.Http;
using TinyNet.Middlewares;

namespace TinyNet.Example.Middlewares;

// EN: Middleware is always a singleton created once at startup and shared by parallel requests.
//     Per-request services are taken with context.GetService inside InvokeAsync, never through the constructor.
// RU: Middleware всегда синглтон: создаётся один раз при старте и обслуживает параллельные запросы.
//     Сервисы запроса берутся через context.GetService внутри InvokeAsync, а не через конструктор.
public class RequestIdMiddleware : IMiddleware
{
    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        var requestId = context.GetService<RequestId>();
        var started = Stopwatch.GetTimestamp();

        // EN: Everything after next() runs once routing, filters and the controller have produced context.Response.
        // RU: Всё после next() выполняется, когда роутинг, фильтры и контроллер уже заполнили context.Response.
        await next(context);

        if (context.Response is null)
            return;
        context.Response.Headers["X-Request-Id"] = requestId.Value;
        context.Response.Headers["X-Elapsed-Ms"] =
            Stopwatch.GetElapsedTime(started).TotalMilliseconds.ToString("F2", CultureInfo.InvariantCulture);
    }
}