using System.Buffers;
using System.Net;
using System.Net.Sockets;

namespace TinyNet.Transport;

public sealed class Connection : IAsyncDisposable
{
    private const int MaxLingerBytes = 64 * 1024;
    private static readonly TimeSpan LingerTimeout = TimeSpan.FromSeconds(1);

    public Connection(Stream transport, EndPoint? remoteEndPoint)
    {
        Transport = transport;
        RemoteEndPoint = remoteEndPoint;
    }

    public Stream Transport { get; }
    public EndPoint? RemoteEndPoint { get; }

    public async ValueTask CloseGracefullyAsync()
    {
        if (Transport is not NetworkStream network)
            return;

        var buffer = ArrayPool<byte>.Shared.Rent(4096);
        try
        {
            network.Socket.Shutdown(SocketShutdown.Send);
            using var timeout = new CancellationTokenSource(LingerTimeout);
            var drained = 0;
            while (drained < MaxLingerBytes)
            {
                var read = await network.ReadAsync(buffer, timeout.Token);
                if (read == 0)
                    return;
                drained += read;
            }
        }
        catch (Exception e) when (e is IOException or SocketException or OperationCanceledException
                                      or ObjectDisposedException)
        {
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    public ValueTask DisposeAsync() => Transport.DisposeAsync();
}