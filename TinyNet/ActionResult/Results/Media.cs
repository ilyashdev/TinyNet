using System.Text;
using TinyNet.Http;

namespace TinyNet.ActionResult.Results;

public class Media : ActionResult
{
    
    private readonly string _data;
    private readonly string _contentType;

    public Media(string Data, string contentType) : base(200)
    {
        _data = Data;
        _contentType = contentType;
    }
    
    public override void ExecuteResult(ref HttpContext context)
    {
        context.Response = new HttpResponse(_statusCode, _data);
        context.Response.Headers.Add("Content-Type", _contentType);
        context.Response.Headers.Add("Content-Length", _data.Length.ToString());
    }
    
}