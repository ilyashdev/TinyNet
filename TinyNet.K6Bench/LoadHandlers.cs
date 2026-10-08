using System.Diagnostics;
using TinyNet.Http;

namespace TinyNet.K6Bench;

public static class LoadHandlers
{
    public static Task<HttpResponse> Ping(HttpRequest request, HttpContext context)
        => Task.FromResult(context.Response().Text("ok"));

    public static Task<HttpResponse> Cpu(HttpRequest request, HttpContext context)
    {
        if (request.GetFromQuery<int>("ms") is not { } ms)
            return MsRequired(context);

        var sw = Stopwatch.StartNew();
        long acc = 0;
        while (sw.ElapsedMilliseconds < ms)
            for (int i = 0; i < 5_000; i++)
                acc = HashCode.Combine(acc, i);

        return Task.FromResult(context.Response().Json(new { profile = "cpu", ms, acc }));
    }

    public static async Task<HttpResponse> Io(HttpRequest request, HttpContext context)
    {
        if (request.GetFromQuery<int>("ms") is not { } ms)
            return await MsRequired(context);

        await Task.Delay(ms);
        return context.Response().Json(new { profile = "io", ms });
    }

    public static Task<HttpResponse> Block(HttpRequest request, HttpContext context)
    {
        if (request.GetFromQuery<int>("ms") is not { } ms)
            return MsRequired(context);

        Thread.Sleep(ms);
        return Task.FromResult(context.Response().Json(new { profile = "block", ms }));
    }

    private static Task<HttpResponse> MsRequired(HttpContext context)
        => Task.FromResult(context.Response().Status(StatusCodes.BadRequest).Text("ms is required"));
}
