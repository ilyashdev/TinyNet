using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Unicode;
using TinyNet.Http;

namespace TinyNet.ActionResult;

public abstract class ActionResult : IActionResult
{
    protected static readonly JsonSerializerOptions Options = new()
    {
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
        WriteIndented = false,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        NumberHandling = JsonNumberHandling.AllowReadingFromString
    };

    protected int _statusCode;
    public ActionResult(int statusCode)
    {
        _statusCode = statusCode;
    }
    
    public virtual void ExecuteResult(ref HttpContext context)
    {
        context.Response = new HttpResponse(_statusCode);
    }
}