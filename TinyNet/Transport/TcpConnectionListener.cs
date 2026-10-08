using System.Net;
using System.Net.Sockets;

namespace TinyNet.Transport;

public sealed class TcpConnectionListener : IConnectionListener
{
    private readonly Socket _listener;

    public TcpConnectionListener(int port, int backlog)
    {
        if (Socket.OSSupportsIPv6)
        {
            _listener = new Socket(AddressFamily.InterNetworkV6, SocketType.Stream, ProtocolType.Tcp);
            _listener.DualMode = true;
            _listener.Bind(new IPEndPoint(IPAddress.IPv6Any, port));
        }
        else
        {
            _listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            _listener.Bind(new IPEndPoint(IPAddress.Any, port));
        }

        _listener.Listen(backlog);
        EndPoint = _listener.LocalEndPoint
                   ?? throw new InvalidOperationException("Listener socket has no local endpoint after Bind");
    }

    public EndPoint EndPoint { get; }

    public async ValueTask<Connection> AcceptAsync(CancellationToken ct)
    {
        var socket = await _listener.AcceptAsync(ct);
        socket.NoDelay = true;
        return new Connection(new NetworkStream(socket, ownsSocket: true), socket.RemoteEndPoint);
    }

    public void Dispose()
    {
        _listener.Dispose();
    }
}