using System.Text.Json;
using TinyNet.Http;

namespace TinyNet.ActionResult.Results;

public class NotFound : BaseResult
{
    public NotFound() : base(404)
    {
    }

    public NotFound(object data) : base(404, data)
    {
    }
}