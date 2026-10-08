using System.Text.Json;

namespace TinyNet.Exceptions;

public sealed class RequestJsonException : RequestException
{
    internal RequestJsonException(JsonException inner) : base("Request body is not valid JSON", 400 , inner)
    {
    }
}