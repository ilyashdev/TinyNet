using System.Text.Json;
using TinyNet.Http;

namespace TinyNet.ActionResult.Results;

public class InternalError : ActionResult
{
    public InternalError() : base( 500)
    {
    }
}