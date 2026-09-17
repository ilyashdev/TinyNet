namespace TinyNet.Http;

public sealed record HttpLimits(
    int MaxHeadBytes,
    int MaxBodyBytes,
    int ReceiveBufferSize,
    TimeSpan ReadTimeout);