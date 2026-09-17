using TinyNet.ActionResult;
using TinyNet.ActionResult.Results;
using TinyNet.Controllers;

namespace TinyNet.Tests;

[Route("/query-string")]
public class QueryStringController : Controller
{
    [HttpMethod("GET")]
    public IActionResult Get([FromQuery] string term) => new Ok(new { term });
}

[Route("/body")]
public class BodyController : Controller
{
    [HttpMethod("POST")]
    public IActionResult Post([FromBody] string name) => new Ok(new { name });
}

[Route("/slow")]
public class SlowController : Controller
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

    [HttpMethod("GET")]
    public async Task<IActionResult> Get()
    {
        Entered.TrySetResult();
        await Release.Task;
        return new Ok();
    }
}