using System.IO.Pipelines;
using TinyNet.DI;
using TinyNet.Exceptions;
using TinyNet.Http;
using TinyNet.Protocols.Streams;
using TinyNet.Transport;

namespace TinyNet.Protocols.Http1;

internal sealed class ConnectionHandler : IHttpProtocol
{
    private readonly HttpSettings _settings;
    private readonly DIContainer _container;
    private readonly ServerLimits _limits;

    public ConnectionHandler(HttpSettings settings, DIContainer container, ServerLimits limits)
    {
        _settings = settings;
        _container = container;
        _limits = limits;
    }

    public async Task ProcessAsync(Connection connection, RequestDelegate app, CancellationToken ct)
    {
        var reader = new TimedPipeReader(PipeReader.Create(connection.Transport));
        var output = connection.Transport;
        var parser = new HeadParser(_settings.MaxBodyLength, _limits.MaxHeadLength);
        try
        {
            for (var served = 1; served <= _limits.MaxRequestsPerConnection; served++)
            {
                var head = await ReadHeadAsync(reader, parser, output, ct);
                if (head is null)
                    return;
                var mayKeepAlive = served < _limits.MaxRequestsPerConnection;
                if (!await HandleRequestAsync(head, reader, output, app, mayKeepAlive, ct))
                    return;
            }
        }
        catch (Exception e) when (e is IOException or TimeoutException or OperationCanceledException
                                      or ObjectDisposedException)
        {
        }
        finally
        {
            await reader.CompleteAsync();
        }
    }

    public Task RejectAsync(Connection connection, int statusCode, string reason, CancellationToken ct)
        => WriteRejectionAsync(connection.Transport, statusCode, reason, ct);

    private async Task<RequestHead?> ReadHeadAsync(
        TimedPipeReader reader, HeadParser parser, Stream output, CancellationToken ct)
    {
        reader.StartPhase(_limits.KeepAliveTimeout);
        var started = false;
        while (true)
        {
            ReadResult result;
            try
            {
                result = await reader.ReadAsync(ct);
            }
            catch (TimeoutException)
            {
                if (started)
                    await WriteRejectionAsync(output, StatusCodes.RequestTimeout, "Request header timeout.", ct);
                return null;
            }

            if (result.IsCanceled)
                throw new OperationCanceledException(ct);

            var buffer = result.Buffer;
            if (!started && !buffer.IsEmpty)
            {
                started = true;
                reader.StartPhase(_limits.HeadersTimeout);
            }

            var parsed = parser.TryParse(ref buffer);
            switch (parsed.Status)
            {
                case HeadParseStatus.Complete:
                    reader.AdvanceTo(buffer.Start);
                    return parsed.Head;
                case HeadParseStatus.Rejected:
                    reader.AdvanceTo(buffer.Start, buffer.End);
                    await WriteRejectionAsync(output, parsed.StatusCode, parsed.Reason!, ct);
                    return null;
                default:
                    reader.AdvanceTo(buffer.Start, buffer.End);
                    if (result.IsCompleted)
                        return null;
                    break;
            }
        }
    }

    private async Task<bool> HandleRequestAsync(
        RequestHead head, TimedPipeReader reader, Stream output, RequestDelegate app, bool mayKeepAlive,
        CancellationToken ct)
    {
        var body = CreateBody(head, reader);
        if (head.ExpectContinue && head.HasBody)
            await ResponseWriter.WriteContinueAsync(output, ct);
        reader.StartPhase(_limits.BodyGracePeriod, _limits.MinBodyBytesPerSecond);

        var request = new HttpRequest(head.Method, head.Path, head.RawPath, head.Protocol, _settings,
            head.Headers, head.Query, body, head.HasBody, ct);
        var keepAlive = head.KeepAlive && mayKeepAlive;

        await using var scope = _container.CreateScope();
        HttpResponse response;
        try
        {
            response = await app(request, new HttpContext(scope, _settings));
        }
        catch (Exception e) when (!ct.IsCancellationRequested)
        {
            (response, var close) = ToErrorResponse(e);
            keepAlive &= !close;
        }

        if (string.Equals(response.Headers.GetOrNull(HeaderNames.Connection), "close",
                StringComparison.OrdinalIgnoreCase))
            keepAlive = false;
        if (keepAlive)
            keepAlive = await TryDrainAsync(body, ct);

        return await ResponseWriter.WriteAsync(output, response, head.Protocol, head.Method == "HEAD", keepAlive, ct);
    }

    private Stream CreateBody(RequestHead head, TimedPipeReader reader)
    {
        if (head.IsChunked)
            return new ChunkedBodyStream(reader, _settings.MaxBodyLength);
        if (head.ContentLength > 0)
            return new FixedBodyStream(reader, head.ContentLength.Value);
        return Stream.Null;
    }

    private (HttpResponse Response, bool Close) ToErrorResponse(Exception e)
    {
        switch (e)
        {
            case TimeoutException:
                return (Error(StatusCodes.RequestTimeout, "Request body timeout."), true);
            case IOException:
                return (Error(StatusCodes.BadRequest, "Connection error."), true);
            case ProtocolException protocol:
                Console.WriteLine($"Request rejected: {protocol.Message}");
                return (Error(protocol.StatusCode, protocol.Message), true);
            case RequestException request:
                Console.WriteLine($"Request rejected: {request.Message}");
                return (Error(request.StatusCode, request.Message), false);
            default:
                Console.WriteLine($"Processing error: {e}");
                return (Error(StatusCodes.InternalServerError, "Internal server error."), false);
        }
    }

    private static async Task<bool> TryDrainAsync(Stream body, CancellationToken ct)
    {
        try
        {
            await body.CopyToAsync(Stream.Null, ct);
            return true;
        }
        catch (Exception e) when (e is RequestException or IOException or TimeoutException)
        {
            return false;
        }
    }

    private async Task WriteRejectionAsync(Stream output, int statusCode, string reason, CancellationToken ct)
    {
        Console.WriteLine($"Request rejected: {statusCode} {reason}");
        await ResponseWriter.WriteAsync(output, Error(statusCode, reason), "HTTP/1.1", isHead: false,
            keepAlive: false, ct);
    }

    private HttpResponse Error(int statusCode, string reason)
        => new ResponseBuilder(_settings).Status(statusCode).Text(reason);
}