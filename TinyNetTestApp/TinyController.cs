using System.Text;
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
    public async Task<IActionResult> Index()
    {
        return new HtmlView($"<h1>TinyNet {SingletonService.Encounter++}</h1>");
    }
    [HttpMethod("POST")]
    public async Task<IActionResult> Post([FromQuery]int count)
    {
        List<int> ints = new List<int>(count);
        while (count > 0)
        {
            ints.Add(Random.Shared.Next());
            count--;
        }
        return new Ok(ints);
    }
}