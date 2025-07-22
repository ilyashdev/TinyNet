using System.Text.Json;
using TinyNet.Http;

namespace TinyNet.ActionResult.Results;

public class InternalError : ActionResult
{
    private readonly object? _response;

    public InternalError(object? response = null)
    {
        _response = response;
    }
    
    public override void ExecuteResult(ref HttpContext context)
    {
        if (_response != null)
        {
            context.Response = new HttpResponse(500, JsonSerializer.Serialize(_response, Options));
            context.Response.Headers.Add("Content-Type", "application/json; charset=utf-8");
            return;
        }
        context.Response = new HttpResponse(500);
    }
}