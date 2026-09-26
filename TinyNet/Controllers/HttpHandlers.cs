using TinyNet.ActionResult;
using TinyNet.Http;

namespace TinyNet.Controllers;

public interface IGetHandler
{
    Task<IActionResult> Get(HttpContext context);
}

public interface IPostHandler
{
    Task<IActionResult> Post(HttpContext context);
}

public interface IPutHandler
{
    Task<IActionResult> Put(HttpContext context);
}

public interface IPatchHandler
{
    Task<IActionResult> Patch(HttpContext context);
}

public interface IDeleteHandler
{
    Task<IActionResult> Delete(HttpContext context);
}