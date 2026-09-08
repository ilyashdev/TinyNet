namespace TinyNet.Http;

public static class HttpLimits
{
    public const int MaxHeadBytes = 16 * 1024;
    public const int MaxBodyBytes = 8 * 1024 * 1024;
    public const int ReceiveBufferSize = 8 * 1024;
    public const int MaxQueuedConnections = 1024;
    public static readonly TimeSpan ReadTimeout = TimeSpan.FromSeconds(15);
}
