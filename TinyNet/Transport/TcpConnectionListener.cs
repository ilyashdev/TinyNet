using System.Net;
using System.Net.Sockets;

namespace TinyNet.Transport;

public class TcpConnectionListener : IConnectionListener
{
    private readonly Socket _client;
    public TcpConnectionListener(int port)
    {
        _client = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        EndPoint = _client.LocalEndPoint                                                                                                                                                                                                             
                   ?? throw new InvalidOperationException("Listener socket has no local endpoint after Bind");     
    }
    
    public EndPoint EndPoint { get; }
    
    public Connection Accept()
    {
        var connection = _client.Accept();
        connection.NoDelay = true;
        return new Connection(new NetworkStream(connection, true), _client.RemoteEndPoint);
    }
    
    public void Dispose()
    {
        _client.Dispose();
    }
}