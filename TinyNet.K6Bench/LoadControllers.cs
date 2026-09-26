using System.Diagnostics;
using TinyNet.ActionResult;
using TinyNet.ActionResult.Results;
using TinyNet.Controllers;
using TinyNet.Http;

namespace TinyNet.K6Bench;

public class PingController : IGetHandler
{
    public Task<IActionResult> Get(HttpContext context)
        => Task.FromResult<IActionResult>(new Ok("ok"));
}

public class CpuLoadController : IGetHandler
{
    public Task<IActionResult> Get(HttpContext context)
    {
        if (context.GetFromQuery<int>("ms") is not { } ms)
            return Task.FromResult<IActionResult>(new BadRequest("ms is required"));

        var sw = Stopwatch.StartNew();
        long acc = 0;
        while (sw.ElapsedMilliseconds < ms)
            for (int i = 0; i < 5_000; i++)
                acc = HashCode.Combine(acc, i);

        return Task.FromResult<IActionResult>(new Ok(new { profile = "cpu", ms, acc }));
    }
}

public class IoLoadController : IGetHandler
{
    public async Task<IActionResult> Get(HttpContext context)
    {
        if (context.GetFromQuery<int>("ms") is not { } ms)
            return new BadRequest("ms is required");

        await Task.Delay(ms);
        return new Ok(new { profile = "io", ms });
    }
}

public class BlockLoadController : IGetHandler
{
    public Task<IActionResult> Get(HttpContext context)
    {
        if (context.GetFromQuery<int>("ms") is not { } ms)
            return Task.FromResult<IActionResult>(new BadRequest("ms is required"));

        Thread.Sleep(ms);
        return Task.FromResult<IActionResult>(new Ok(new { profile = "block", ms }));
    }
}