using System.Net;

namespace TinyNet.Transport;

public interface IConnectionListener : IDisposable
{
    EndPoint EndPoint { get; }
    ValueTask<Connection> AcceptAsync(CancellationToken ct);
}