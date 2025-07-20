using System.Net.Sockets;
using System.Text;
using TinyNet.Http;

namespace TinyNet.Application;

public class NetClient : IDisposable
{
    private Socket _clientSocket;

    public NetClient(Socket clientSocket)
    {
        _clientSocket = clientSocket;
    }

    public async Task<HttpRequest> GetRequest()
    {
        var buffer = new byte[_clientSocket.ReceiveBufferSize];
        var received = await _clientSocket.ReceiveAsync(buffer);
        var request = Encoding.ASCII.GetString(buffer, 0, received);
        return Http.Http.ParseRequest(request);
    }

    public void SendResponse(HttpResponse response)
    {
        _clientSocket.Send(Encoding.UTF8.GetBytes(response.ToHttpResponse()));
        _clientSocket.Shutdown(SocketShutdown.Both);
    }

    public void Dispose()
    {
        _clientSocket.Dispose();
    }
}