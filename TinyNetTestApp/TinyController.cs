using TinyNet.ActionResult;
using TinyNet.ActionResult.Results;
using TinyNet.Controllers;

namespace TinyNetTestApp;

[Route("/")]
public class TinyController : Controller
{
    public TinyController(SingletonService singletonService)
    {
        SingletonService = singletonService;
    }

    public SingletonService SingletonService {get; set;}
    [HttpMethod("GET")]
    public IActionResult Index()
    {
        return new HtmlView($"<h1>TinyNet {SingletonService.Encounter++}</h1>");
    }
}