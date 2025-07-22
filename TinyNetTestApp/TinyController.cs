using TinyNet.ActionResult;
using TinyNet.ActionResult.Results;
using TinyNet.Controllers;

namespace TinyNetTestApp;

[Route("/")]
public class TinyController : Controller
{
    [HttpMethod("GET")]
    public IActionResult Index()
    {
        return new HtmlView("<h1>TinyNet</h1>");
    }
}