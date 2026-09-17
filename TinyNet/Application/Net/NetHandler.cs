using System.Net;
using System.Net.Sockets;
using TinyNet.Http;

namespace TinyNet.Application;

public class NetHandler
{
    private Socket _socket;
    private readonly HttpLimits _limits;

    public NetHandler(int port, HttpLimits limits)
    {
        _limits = limits;
        IPEndPoint ipEndPoint = new IPEndPoint(IPAddress.Any, port);
        _socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        _socket.ReceiveTimeout = 5000;
        _socket.SendTimeout = 5000;
        _socket.Bind(ipEndPoint);
        _socket.Listen(1000);

    }

    public async Task<NetClient> AcceptAsync()
    {
        return new NetClient(await _socket.AcceptAsync(), _limits);
    }
}