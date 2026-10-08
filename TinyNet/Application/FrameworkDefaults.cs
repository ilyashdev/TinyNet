namespace TinyNet.Application;

public static class FrameworkDefaults
{
    public const string ServerPort = "Server:Port";
    public const string ServerListenBacklog = "Server:ListenBacklog";
    public const string ServerAcceptLoops = "Server:AcceptLoops";
    public const string ServerMaxConnections = "Server:MaxConnections";
    public const string ServerMaxConcurrentRequests = "Server:MaxConcurrentRequests";
    public const string ServerMaxQueuedRequests = "Server:MaxQueuedRequests";
    public const string ServerRequestQueueTimeoutSeconds = "Server:RequestQueueTimeoutSeconds";
    public const string ServerMaxHeadBytes = "Server:MaxHeadBytes";
    public const string ServerMaxBodyBytes = "Server:MaxBodyBytes";
    public const string ServerHeadersTimeoutSeconds = "Server:HeadersTimeoutSeconds";


    public const string ServerBodyGracePeriodSeconds = "Server:BodyGracePeriodSeconds";
    public const string ServerMinBodyBytesPerSecond = "Server:MinBodyBytesPerSecond";

    public const string ServerKeepAliveMax = "Server:KeepAliveMax";
    public const string ServerKeepAliveTimeoutSeconds = "Server:KeepAliveTimeoutSeconds";

    public static readonly KeyValuePair<string, string>[] All =
    [
        new(ServerPort, "5000"),
        new(ServerListenBacklog, "512"),
        new(ServerAcceptLoops, "1"),
        new(ServerMaxConnections, "10000"),
        new(ServerMaxConcurrentRequests, "256"),
        new(ServerMaxQueuedRequests, "1024"),
        new(ServerRequestQueueTimeoutSeconds, "2"),
        new(ServerMaxHeadBytes, "32768"),
        new(ServerMaxBodyBytes, "1048576"),
        new(ServerHeadersTimeoutSeconds, "10"),
        new(ServerBodyGracePeriodSeconds, "5"),
        new(ServerMinBodyBytesPerSecond, "240"),

        new(ServerKeepAliveMax, "1000"),
        new(ServerKeepAliveTimeoutSeconds, "60")
    ];
}
