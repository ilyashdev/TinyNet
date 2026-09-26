using System.Text.Json;
using TinyNet.Http;

namespace TinyNet.ActionResult;

public abstract class ActionResult : IActionResult
{
    protected static readonly JsonSerializerOptions Options = JsonDefaults.Options;

    protected int _statusCode;
    public ActionResult(int statusCode)
    {
        _statusCode = statusCode;
    }
    
    public virtual void ExecuteResult(HttpContext context)
    {
        context.Response = new HttpResponse(_statusCode);
    }
}