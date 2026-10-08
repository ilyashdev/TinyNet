using TinyNet.Http;
using TinyNet.Transport;

namespace TinyNet.Protocols;

public interface IHttpProtocol
{
    Task ProcessAsync(Connection connection, RequestDelegate app, CancellationToken ct);
    Task RejectAsync(Connection connection, int statusCode, string reason, CancellationToken ct);
}