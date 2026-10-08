namespace TinyNet.Protocols.Http1;

internal sealed record ServerLimits(
    TimeSpan KeepAliveTimeout,
    TimeSpan HeadersTimeout,
    TimeSpan BodyGracePeriod,
    double MinBodyBytesPerSecond,
    int MaxRequestsPerConnection,
    int MaxHeadLength);