using System.Diagnostics;
using TinyNet.ActionResult;
using TinyNet.ActionResult.Results;
using TinyNet.Controllers;

namespace TinyNetTestApp;

[Route("/load/cpu")]
public class CpuLoadController : Controller
{
    [HttpMethod("GET")]
    public async Task<IActionResult> Burn([FromQuery] int ms)
    {
        var sw = Stopwatch.StartNew();
        long acc = 0;
        while (sw.ElapsedMilliseconds < ms)
            for (int i = 0; i < 5_000; i++)
                acc = HashCode.Combine(acc, i);

        return new Ok(new { profile = "cpu", ms, acc });
    }
}

[Route("/load/io")]
public class IoLoadController : Controller
{
    [HttpMethod("GET")]
    public async Task<IActionResult> Wait([FromQuery] int ms)
    {
        await Task.Delay(ms);
        return new Ok(new { profile = "io", ms });
    }
}


[Route("/load/block")]
public class BlockLoadController : Controller
{
    [HttpMethod("GET")]
    public async Task<IActionResult> Block([FromQuery] int ms)
    {
        Thread.Sleep(ms);
        return new Ok(new { profile = "block", ms });
    }
}
