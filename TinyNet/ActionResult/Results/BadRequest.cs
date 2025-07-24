using System.Text.Json;
using TinyNet.Http;

namespace TinyNet.ActionResult.Results;

public class BadRequest : BaseResult
{
    public BadRequest() : base(400)
    {
    }

    public BadRequest(object data) : base(400, data)
    {
    }
}