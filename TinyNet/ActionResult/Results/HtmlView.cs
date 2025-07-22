using TinyNet.Http;

namespace TinyNet.ActionResult.Results;

public class HtmlView : ActionResult
{
    private string _html;

    public HtmlView(string html)
    {
        _html = html;
    }

    public override void ExecuteResult(ref HttpContext context)
    {
            context.Response = new HttpResponse(200, _html);
            context.Response.Headers.Add("Content-Type", "text/html; charset=utf-8");
            return;
    }
}