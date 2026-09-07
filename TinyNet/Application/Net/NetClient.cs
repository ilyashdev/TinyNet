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
        using var ms = new MemoryStream();                                                                                                                                                                                                            
        var buffer = new byte[4096]; 
        int received; 
        do 
        {                                                                                                                                                                                                                                             
          received = await _clientSocket.ReceiveAsync(buffer);                                                                                                                                                                                      
          ms.Write(buffer, 0, received); 
        } while (received == buffer.Length && _clientSocket.Available > 0);
        var request = Encoding.UTF8.GetString(ms.ToArray());  
        return Http.Http.ParseRequest(request);
    }

    public async Task SendResponse(HttpResponse response)                                                                                                                                                                                             
    {                                                                                                                                                                                                                                                 
        var data = response.BinaryBody != null                                                                                                                                                                                                        
            ? response.ToHttpResponseBytes()                                                                                                                                                                                                          
            : Encoding.UTF8.GetBytes(response.ToHttpResponse());                                                                                                                                                                                      
        await _clientSocket.SendAsync(data);
        ShutdownQuietly();
    }

    private void ShutdownQuietly()
    {
        try
        {
            _clientSocket.Shutdown(SocketShutdown.Both);
        }
        catch (SocketException)
        {
        }
        catch (ObjectDisposedException)
        {
        }
    }

    public async Task SendOverloadedResponse()                                                                                                                                                                                                        
    {                                                                                                                                                                                                                                                 
        var response = new HttpResponse(503, "Service Unavailable");                                                                                                                                                                                  
        await _clientSocket.SendAsync(Encoding.UTF8.GetBytes(response.ToHttpResponse()));                                                                                                                                                             
        _clientSocket.Shutdown(SocketShutdown.Both);                                                                                                                                                                                                  
    }  
    
    public bool IsConnected() => _clientSocket.Connected;

    public void Dispose()
    {
        _clientSocket.Dispose();
    }
    
    
}