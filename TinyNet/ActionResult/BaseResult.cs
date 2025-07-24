using System.Text.Json;
using TinyNet.Http;

namespace TinyNet.ActionResult;

public class BaseResult : ActionResult
{
    private object? _data;
    public BaseResult(int statusCode) : base(statusCode)
    {
    }

    public BaseResult(int statusCode, object data) : base(statusCode)
    {
        _data = data;
    }
    public override void ExecuteResult(ref HttpContext context)
    {
        if (_data != null)
        {
            context.Response = new HttpResponse(_statusCode, JsonSerializer.Serialize(_data, Options));
            context.Response.Headers.Add("Content-Type", "application/json; charset=utf-8");
            return;
        }
        context.Response = new HttpResponse(_statusCode);
    }
}