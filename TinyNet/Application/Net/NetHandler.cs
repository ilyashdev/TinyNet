using System.Net;
using System.Net.Sockets;

namespace TinyNet.Application;

public class NetHandler
{
    private Socket _socket;
    public NetHandler(int port)
    {
        IPEndPoint ipEndPoint = new IPEndPoint(IPAddress.Any, port);
        _socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        _socket.Bind(ipEndPoint);
        _socket.Listen(100);
    }

    public NetClient Accept()
    {
        return new NetClient(_socket.Accept());
    } 
}