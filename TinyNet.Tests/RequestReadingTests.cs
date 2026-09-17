using System.Net;
using System.Net.Sockets;
using System.Text;
using TinyNet.Application;
using TinyNet.Http;

namespace TinyNet.Tests;

public class RequestReadingTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(8192)]
    public async Task GetRequest_HeadSplitAcrossReads_IsParsedCorrectly(int receiveBufferSize)
    {
        var limits = new HttpLimits(16384, 8192, receiveBufferSize, TimeSpan.FromSeconds(10));
        var (client, server) = await CreatePairAsync(limits);

        using (client)
        using (server)
        {
            const string body = """{"name":"Bob"}""";
            var raw = "POST /echo?x=1 HTTP/1.1\r\n" +
                      "Host: localhost\r\n" +
                      $"Content-Length: {Encoding.UTF8.GetByteCount(body)}\r\n" +
                      "\r\n" +
                      body;

            var reading = server.GetRequest();

            foreach (var octet in Encoding.UTF8.GetBytes(raw))
                await client.SendAsync(new[] { octet });

            var request = await reading.WaitAsync(TimeSpan.FromSeconds(10));

            Assert.Equal("POST", request.Method);
            Assert.Equal("/echo", request.Url);
            Assert.Equal("1", request.Query["x"]);
            Assert.Equal("localhost", request.Headers["host"]);
            Assert.Equal("Bob", request.Body!["name"]!.GetValue<string>());
        }
    }

    private static async Task<(Socket Client, NetClient Server)> CreatePairAsync(HttpLimits limits)
    {
        using var listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        listener.Bind(new IPEndPoint(IPAddress.Loopback, 0));
        listener.Listen(1);

        var client = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)
        {
            NoDelay = true
        };
        var connecting = client.ConnectAsync((IPEndPoint)listener.LocalEndPoint!);
        var accepted = await listener.AcceptAsync();
        await connecting;

        return (client, new NetClient(accepted, limits));
    }
}