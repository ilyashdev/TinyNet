using System.Diagnostics;
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
    public async Task<HttpResponse> InvokeAsync(HttpRequest request, HttpContext context, RequestDelegate next)
    {
        var requestId = context.GetService<RequestId>();
        var started = Stopwatch.GetTimestamp();

        // EN: next() returns the response produced by routing, filters and the handler.
        //     An exception thrown further down (a malformed body, a non-numeric {id}) passes through here.
        // RU: next() возвращает ответ, созданный роутингом, фильтрами и обработчиком.
        //     Исключение, брошенное глубже (битое тело, нечисловой {id}), проходит через это место.
        try
        {
            var response = await next(request, context);
            Log(requestId, request, response.StatusCode.ToString(), started);
            return response;
        }
        catch (Exception e)
        {
            Log(requestId, request, e.GetType().Name, started);
            throw;
        }
    }

    private static void Log(RequestId requestId, HttpRequest request, string outcome, long started)
        => Console.WriteLine(
            $"[{requestId.Value}] {request.Method} {request.Path} -> {outcome} " +
            $"in {Stopwatch.GetElapsedTime(started).TotalMilliseconds:F2} ms");
}
