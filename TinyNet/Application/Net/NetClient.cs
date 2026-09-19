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
    private readonly HttpLimits _limits;
    private readonly byte[] _buffer;
    private readonly MemoryStream _accumulated = new();
    private bool _requestStarted;
    public readonly DateTime ConnectTime;

    public NetClient(Socket clientSocket, HttpLimits limits)
    {
        ConnectTime = DateTime.UtcNow;
        _clientSocket = clientSocket;
        _limits = limits;
        _buffer = new byte[limits.ReceiveBufferSize];
    }

    public Task<HttpRequest> GetRequest(CancellationToken ct = default)
        => GetRequest(_limits.ReadTimeout, ct);

    public async Task<HttpRequest> GetRequest(TimeSpan idleTimeout, CancellationToken ct = default)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _requestStarted = false;
        timeout.CancelAfter(idleTimeout);
        try
        {
            return await ReadRequest(timeout);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            if (!_requestStarted)
                throw new ConnectionClosedException(
                    $"Client sent nothing within {idleTimeout.TotalSeconds:0} s while the connection was idle");
            throw new RequestTimeoutException(
                $"Client did not send a complete request within {_limits.ReadTimeout.TotalSeconds:0} s");
        }
    }

    private async Task<HttpRequest> ReadRequest(CancellationTokenSource timeout)
    {
        if (_accumulated.Length > 0)
        {
            _requestStarted = true;
            timeout.CancelAfter(_limits.ReadTimeout);
        }

        int searchFrom = 0;
        int headEnd = IndexOf(_accumulated.GetBuffer(), (int)_accumulated.Length, HeadSeparator, searchFrom);
        while (headEnd < 0)
        {
            searchFrom = Math.Max(0, (int)_accumulated.Length - (HeadSeparator.Length - 1));
            await FillAsync(timeout);

            if (_accumulated.Length > _limits.MaxHeadBytes)
                throw new RequestTooLargeException($"Request head exceeds {_limits.MaxHeadBytes} bytes");

            headEnd = IndexOf(_accumulated.GetBuffer(), (int)_accumulated.Length, HeadSeparator, searchFrom);
        }

        var request = Http.Http.ParseHead(Encoding.UTF8.GetString(_accumulated.GetBuffer(), 0, headEnd));

        var bodyStart = headEnd + HeadSeparator.Length;
        var requestEnd = bodyStart;
        string bodyText = null;

        if (Http.Http.IsChunked(request.Headers))
        {
            (bodyText, requestEnd) = await ReadChunkedBodyAsync(bodyStart, timeout);
        }
        else if (Http.Http.TryGetContentLength(request.Headers, out var contentLength))
        {
            bodyText = await ReadFixedBodyAsync(bodyStart, contentLength, timeout);
            requestEnd = bodyStart + contentLength;
        }

        request.Body = Http.Http.ParseBody(bodyText);
        Consume(requestEnd);
        return request;
    }

    private void Consume(int requestEnd)
    {
        var remaining = (int)_accumulated.Length - requestEnd;
        if (remaining > 0)
            Buffer.BlockCopy(_accumulated.GetBuffer(), requestEnd, _accumulated.GetBuffer(), 0, remaining);

        _accumulated.SetLength(remaining);
        _accumulated.Position = remaining;

        if (remaining == 0 && _accumulated.Capacity > _limits.ReceiveBufferSize)
            _accumulated.Capacity = _limits.ReceiveBufferSize;
    }

    private async Task FillAsync(CancellationTokenSource timeout)
    {
        int received;
        try
        {
            received = await _clientSocket.ReceiveAsync(_buffer, SocketFlags.None, timeout.Token);
        }
        catch (SocketException ex) when (ex.SocketErrorCode is SocketError.ConnectionReset
                                             or SocketError.ConnectionAborted
                                             or SocketError.Shutdown)
        {
            throw new ConnectionClosedException($"Client dropped the connection ({ex.SocketErrorCode})");
        }

        if (received == 0)
            throw new ConnectionClosedException("Client closed the connection before sending a complete request");

        if (!_requestStarted)
        {
            _requestStarted = true;
            timeout.CancelAfter(_limits.ReadTimeout);
        }

        _accumulated.Write(_buffer, 0, received);
    }

    private async Task<string> ReadFixedBodyAsync(int bodyStart, int contentLength, CancellationTokenSource timeout)
    {
        if (contentLength > _limits.MaxBodyBytes)
            throw new RequestTooLargeException($"Body exceeds {_limits.MaxBodyBytes} bytes");

        long required = (long)bodyStart + contentLength;
        while (_accumulated.Length < required)
            await FillAsync(timeout);

        return Encoding.UTF8.GetString(_accumulated.GetBuffer(), bodyStart, contentLength);
    }

    private async Task<(string Body, int RequestEnd)> ReadChunkedBodyAsync(int bodyStart, CancellationTokenSource timeout)
    {
        using var body = new MemoryStream();
        int cursor = bodyStart;

        while (true)
        {
            int lineEnd = await EnsureLineAsync(cursor, timeout);

            var sizeLine = Encoding.ASCII.GetString(_accumulated.GetBuffer(), cursor, lineEnd - cursor);
            var extension = sizeLine.IndexOf(';');
            if (extension >= 0)
                sizeLine = sizeLine.Substring(0, extension);

            if (!int.TryParse(sizeLine.Trim(), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var chunkSize)
                || chunkSize < 0)
                throw new BadRequestException($"Invalid chunk size: {sizeLine}");

            cursor = lineEnd + CrLf.Length;
            if (chunkSize == 0)
                break;

            if (body.Length + chunkSize > _limits.MaxBodyBytes)
                throw new RequestTooLargeException($"Body exceeds {_limits.MaxBodyBytes} bytes");

            long required = (long)cursor + chunkSize + CrLf.Length;
            while (_accumulated.Length < required)
                await FillAsync(timeout);

            body.Write(_accumulated.GetBuffer(), cursor, chunkSize);
            cursor += chunkSize + CrLf.Length;
        }

        while (true)
        {
            int lineEnd = await EnsureLineAsync(cursor, timeout);
            var trailerEnd = lineEnd == cursor;
            cursor = lineEnd + CrLf.Length;
            if (trailerEnd)
                break;
        }

        return (Encoding.UTF8.GetString(body.GetBuffer(), 0, (int)body.Length), cursor);
    }

    private async Task<int> EnsureLineAsync(int from, CancellationTokenSource timeout)
    {
        while (true)
        {
            int index = IndexOf(_accumulated.GetBuffer(), (int)_accumulated.Length, CrLf, from);
            if (index >= 0)
                return index;

            if (_accumulated.Length > _limits.MaxHeadBytes + _limits.MaxBodyBytes)
                throw new RequestTooLargeException("Chunked body exceeds allowed size");

            await FillAsync(timeout);
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
        var sent = 0;
        while (sent < data.Length)
            sent += await _clientSocket.SendAsync(data.AsMemory(sent), SocketFlags.None);
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


    public void SendOverloadedResponse()
    {
        var response = new HttpResponse(503, "Service Unavailable");
        _clientSocket.SendTimeout = 250;
        _clientSocket.Send(Encoding.UTF8.GetBytes(response.ToHttpResponse()));
        DrainQuietly();
        ShutdownQuietly();
    }

    private void DrainQuietly()
    {
        try
        {
            var sink = new byte[1024];
            while (_clientSocket.Available > 0 && _clientSocket.Receive(sink) > 0)
            {
            }
        }
        catch (SocketException)
        {
        }
        catch (ObjectDisposedException)
        {
        }
    }

    public bool IsConnected() => _clientSocket.Connected;

    public void Dispose()
    {
        ShutdownQuietly();
        _accumulated.Dispose();
        _clientSocket.Dispose();
    }
}
