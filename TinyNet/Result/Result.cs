using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Unicode;
using TinyNet.Http;

namespace TinyNet.Result;

public abstract class Result : IResult
{
    protected static readonly JsonSerializerOptions Options = new()
    {
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
        WriteIndented = false,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        NumberHandling = JsonNumberHandling.AllowReadingFromString
    };
    public abstract void ExecuteResult(ref HttpContext context);
}