namespace TinyNet.Application;

public static class FrameworkDefaults
{
    public const string ServerPort = "Server:Port";
    public const string ServerMaxConcurrentRequests = "Server:MaxConcurrentRequests";
    public const string ServerMaxQueuedConnections = "Server:MaxQueuedConnections";
    public const string ServerMaxHeadBytes = "Server:MaxHeadBytes";
    public const string ServerMaxBodyBytes = "Server:MaxBodyBytes";
    public const string ServerReceiveBufferSize = "Server:ReceiveBufferSize";
    public const string ServerReadTimeoutSeconds = "Server:ReadTimeoutSeconds";

    public const string ServerKeepAliveMax = "Server:KeepAliveMax";
    public const string ServerKeepAliveTimeout = "Server:KeepAliveTimeout";
    
    public const string WebRootPath = "WebRoot:Path";

    public static readonly KeyValuePair<string, string>[] All =
    [
        new(ServerPort, "5000"),
        new(ServerMaxConcurrentRequests, "256"),
        new(ServerMaxQueuedConnections, "1024"),
        new(ServerMaxHeadBytes, "16384"),
        new(ServerMaxBodyBytes, "8388608"),
        new(ServerReceiveBufferSize, "8192"),
        new(ServerReadTimeoutSeconds, "15"),
        
        new(ServerKeepAliveMax, "1000"),
        new(ServerKeepAliveTimeout, "5"),
        
        new(WebRootPath, "./WebRoot")
    ];
}