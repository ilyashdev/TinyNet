using TinyNet.Http;

namespace TinyNet.ActionResult;


public interface IActionResult
{ 
    void ExecuteResult(ref HttpContext context);
}