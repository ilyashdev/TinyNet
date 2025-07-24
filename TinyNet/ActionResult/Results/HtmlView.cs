using TinyNet.Http;

namespace TinyNet.ActionResult.Results;

public class HtmlView : ActionResult
{
    private string _htmlContent;
    public HtmlView(string htmlContent) : base( 200)
    {
        _htmlContent = htmlContent;
    }

    public override void ExecuteResult(ref HttpContext context)
    {
            context.Response = new HttpResponse(200, _htmlContent);
            context.Response.Headers.Add("Content-Type", "text/html; charset=utf-8");
            return;
    }
}