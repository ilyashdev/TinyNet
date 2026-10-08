namespace TinyNet.Exceptions;

public sealed class ProtocolException : RequestException
{
    internal ProtocolException(string message, int statusCode) : base(message, statusCode)
    {
    }
}