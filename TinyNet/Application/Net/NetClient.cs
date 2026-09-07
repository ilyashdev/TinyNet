using System.Globalization;
using System.Net.Sockets;
using System.Text;
using TinyNet.Http;

namespace TinyNet.Application;

public class NetClient : IDisposable
{
    private static readonly byte[] HeadSeparator = "\r\n\r\n"u8.ToArray();
    private static readonly byte[] CrLf = "\r\n"u8.ToArray();

    private Socket _clientSocket;

    public NetClient(Socket clientSocket)
    {
        _clientSocket = clientSocket;
    }

    public async Task<HttpRequest> GetRequest()
    {
        using var timeout = new CancellationTokenSource(HttpLimits.ReadTimeout);
        try
        {
            return await GetRequest(timeout.Token);
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested)
        {
            throw new RequestTimeoutException(
                $"Client did not send a complete request within {HttpLimits.ReadTimeout.TotalSeconds:0} s");
        }
    }

    public async Task<HttpRequest> GetRequest(CancellationToken token)
    {
        var buffer = new byte[HttpLimits.ReceiveBufferSize];
        using var accumulated = new MemoryStream();

        int headEnd = -1;
        int searchFrom = 0;
        while (headEnd < 0)
        {
            await FillAsync(accumulated, buffer, token);

            if (accumulated.Length > HttpLimits.MaxHeadBytes)
                throw new RequestTooLargeException($"Request head exceeds {HttpLimits.MaxHeadBytes} bytes");

            headEnd = IndexOf(accumulated.GetBuffer(), (int)accumulated.Length, HeadSeparator, searchFrom);
            searchFrom = Math.Max(0, (int)accumulated.Length - (HeadSeparator.Length - 1));
        }

        var request = Http.Http.ParseHead(Encoding.UTF8.GetString(accumulated.GetBuffer(), 0, headEnd));

        var bodyStart = headEnd + HeadSeparator.Length;
        string bodyText = null;

        if (Http.Http.IsChunked(request.Headers))
            bodyText = await ReadChunkedBodyAsync(accumulated, buffer, bodyStart, token);
        else if (Http.Http.TryGetContentLength(request.Headers, out var contentLength))
            bodyText = await ReadFixedBodyAsync(accumulated, buffer, bodyStart, contentLength, token);

        request.Body = Http.Http.ParseBody(bodyText);
        return request;
    }

    private async Task FillAsync(MemoryStream accumulated, byte[] buffer, CancellationToken token)
    {
        int received = await _clientSocket.ReceiveAsync(buffer, SocketFlags.None, token);
        if (received == 0)
            throw new ConnectionClosedException("Client closed the connection before sending a complete request");

        accumulated.Write(buffer, 0, received);
    }

    private async Task<string> ReadFixedBodyAsync(
        MemoryStream accumulated, byte[] buffer, int bodyStart, int contentLength, CancellationToken token)
    {
        if (contentLength > HttpLimits.MaxBodyBytes)
            throw new RequestTooLargeException($"Body exceeds {HttpLimits.MaxBodyBytes} bytes");

        long required = (long)bodyStart + contentLength;
        while (accumulated.Length < required)
            await FillAsync(accumulated, buffer, token);

        return Encoding.UTF8.GetString(accumulated.GetBuffer(), bodyStart, contentLength);
    }

    private async Task<string> ReadChunkedBodyAsync(
        MemoryStream accumulated, byte[] buffer, int bodyStart, CancellationToken token)
    {
        using var body = new MemoryStream();
        int cursor = bodyStart;

        while (true)
        {
            int lineEnd = await EnsureLineAsync(accumulated, buffer, cursor, token);

            var sizeLine = Encoding.ASCII.GetString(accumulated.GetBuffer(), cursor, lineEnd - cursor);
            var extension = sizeLine.IndexOf(';');
            if (extension >= 0)
                sizeLine = sizeLine.Substring(0, extension);

            if (!int.TryParse(sizeLine.Trim(), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var chunkSize)
                || chunkSize < 0)
                throw new BadRequestException($"Invalid chunk size: {sizeLine}");

            cursor = lineEnd + CrLf.Length;
            if (chunkSize == 0)
                break;

            if (body.Length + chunkSize > HttpLimits.MaxBodyBytes)
                throw new RequestTooLargeException($"Body exceeds {HttpLimits.MaxBodyBytes} bytes");

            long required = (long)cursor + chunkSize + CrLf.Length;
            while (accumulated.Length < required)
                await FillAsync(accumulated, buffer, token);

            body.Write(accumulated.GetBuffer(), cursor, chunkSize);
            cursor += chunkSize + CrLf.Length;
        }

        return Encoding.UTF8.GetString(body.GetBuffer(), 0, (int)body.Length);
    }

    private async Task<int> EnsureLineAsync(
        MemoryStream accumulated, byte[] buffer, int from, CancellationToken token)
    {
        while (true)
        {
            int index = IndexOf(accumulated.GetBuffer(), (int)accumulated.Length, CrLf, from);
            if (index >= 0)
                return index;

            if (accumulated.Length > HttpLimits.MaxHeadBytes + HttpLimits.MaxBodyBytes)
                throw new RequestTooLargeException("Chunked body exceeds allowed size");

            await FillAsync(accumulated, buffer, token);
        }
    }

    private static int IndexOf(byte[] haystack, int length, byte[] needle, int from)
    {
        for (int i = Math.Max(0, from); i <= length - needle.Length; i++)
        {
            bool match = true;
            for (int j = 0; j < needle.Length; j++)
            {
                if (haystack[i + j] != needle[j])
                {
                    match = false;
                    break;
                }
            }

            if (match)
                return i;
        }

        return -1;
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
        ShutdownQuietly();
    }

    public bool IsConnected() => _clientSocket.Connected;

    public void Dispose()
    {
        _clientSocket.Dispose();
    }
}
