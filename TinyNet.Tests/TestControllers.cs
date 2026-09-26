using TinyNet.ActionResult;
using TinyNet.ActionResult.Results;
using TinyNet.Controllers;
using TinyNet.Http;

namespace TinyNet.Tests;

public class SlowController : IGetHandler
{
    internal static TaskCompletionSource Entered = NewSource();
    internal static TaskCompletionSource Release = NewSource();

    internal static void Reset()
    {
        Entered = NewSource();
        Release = NewSource();
    }

    private static TaskCompletionSource NewSource()
        => new(TaskCreationOptions.RunContinuationsAsynchronously);

    public async Task<IActionResult> Get(HttpContext context)
    {
        Entered.TrySetResult();
        await Release.Task;
        return new Ok();
    }
}