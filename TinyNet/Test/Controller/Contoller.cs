using TinyNet.Controllers;
using TinyNet.Result;

namespace TinyNet.Test.Controller;

[Route("/xd")]
public class TestContoller : Controllers.Controller
{
    [HttpMethod("GET")]
    public async Task<IResult> Get([FromQuery] string name)
    {
        return new Ok(name);
    }
}