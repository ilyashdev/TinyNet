namespace TinyNet.Example.Services;

public class RequestId
{
    public string Value { get; } = Guid.NewGuid().ToString("N")[..8];
}