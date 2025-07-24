using System.Text.Json;
using TinyNet.Http;

namespace TinyNet.ActionResult.Results;

public class Ok : BaseResult
{
    public Ok() : base(200)
    {
    }

    public Ok(object data) : base(200, data)
    {
    }
}