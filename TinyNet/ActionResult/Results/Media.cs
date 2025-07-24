using System.Text;
using TinyNet.Http;

namespace TinyNet.ActionResult.Results;

public class Media : ActionResult
{
    
    private readonly byte[] _fileData;
    private readonly string _contentType;

    public Media(byte[] fileData, string contentType) : base(200)
    {
        _fileData = fileData;
        _contentType = contentType;
    }
    
    public override void ExecuteResult(ref HttpContext context)
    {
        context.Response = new HttpResponse(_statusCode, Encoding.UTF8.GetString(_fileData));
        context.Response.Headers.Add("Content-Type", _contentType);
        context.Response.Headers.Add("Content-Length", _fileData.Length.ToString());
        //context.Response.Headers.Add("Content-Encoding", "gzip");
    }
    
}