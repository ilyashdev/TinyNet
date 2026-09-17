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
    
    public int Port => ((IPEndPoint)_socket.LocalEndPoint!).Port;


    public NetClient Accept()
    {
        var clientSocket = _socket.Accept();
        clientSocket.SendTimeout = 5000;
        clientSocket.ReceiveTimeout = 5000;
        return new NetClient(clientSocket, _limits);
    }

    public void StopListening() => _socket.Dispose();
}