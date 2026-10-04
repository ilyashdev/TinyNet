using System.Text.Json;

namespace TinyNet.Http.Exceptions;

public sealed class RequestJsonException : JsonException
{
    internal RequestJsonException(JsonException inner) : base("Request body is not valid JSON", inner.Path, inner.LineNumber, inner.BytePositionInLine, inner)
    {
    }
}