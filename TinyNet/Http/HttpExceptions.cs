namespace TinyNet.Http;

public class ConnectionClosedException : Exception
{
    public ConnectionClosedException(string message) : base(message)
    {
    }
}

public class RequestTooLargeException : Exception
{
    public RequestTooLargeException(string message) : base(message)
    {
    }
}

public class RequestTimeoutException : Exception
{
    public RequestTimeoutException(string message) : base(message)
    {
    }
}

public class BadRequestException : Exception
{
    public BadRequestException(string message) : base(message)
    {
    }
}
