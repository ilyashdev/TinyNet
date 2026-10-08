namespace TinyNet.Exceptions;

public abstract class RequestException : Exception
{
    public int StatusCode { get; }
    protected RequestException(string message, int statusCode, Exception? innerException = null)
        : base(message, innerException)
    {
        StatusCode = statusCode;
    }
}