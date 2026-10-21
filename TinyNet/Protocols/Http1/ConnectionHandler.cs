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
        var reader = new TimedPipeReader(PipeReader.Create(connection.Transport, new StreamPipeReaderOptions(leaveOpen: true)));
        var output = connection.Transport;
        var parser = new HeadParser(_limits.MaxHeadLength);
        var builder = new RequestHeadBuilder(_settings.MaxBodyLength);
        try
        {
            for (var served = 1; served <= _limits.MaxRequestsPerConnection; served++)
            {
                RequestHead? head;
                try
                {
                    head = await ReadHeadAsync(reader, parser, builder, ct);
                }
                catch (ProtocolException e)
                {
                    await WriteRejectionAsync(output, e.StatusCode, e.Message, ct);
                    return;
                }

                if (head is null)
                    return;
                var mayKeepAlive = served < _limits.MaxRequestsPerConnection;
                if (!await HandleRequestAsync(head, reader, output, app, mayKeepAlive, ct))
                    return;
            }
        }
        finally
        {
            await reader.CompleteAsync();
        }
    }

    public Task RejectAsync(Connection connection, int statusCode, string reason, CancellationToken ct)
        => WriteRejectionAsync(connection.Transport, statusCode, reason, ct);

    private async Task WriteRejectionAsync(Stream output, int statusCode, string reason, CancellationToken ct)
    {
        Console.WriteLine($"Request rejected: {statusCode} {reason}");
        var response = new ResponseBuilder(_settings).Status(statusCode).Text(reason);
        await ResponseWriter.WriteAsync(output, response, HttpProtocols.Http11, isHead: false, keepAlive: false, ct);
    }

    private async Task<RequestHead?> ReadHeadAsync(
        TimedPipeReader reader, HeadParser parser, RequestHeadBuilder builder, CancellationToken ct)
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
                    throw new ProtocolException("Request header timeout.", StatusCodes.RequestTimeout);
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

            HeadPart part;
            while ((part = parser.Next(ref buffer)) is HeadPart.RequestLine or HeadPart.Header)
            {
                if (part == HeadPart.RequestLine)
                    builder.SetRequestLine(parser);
                else
                    builder.AddHeader(parser.HeaderName, parser.HeaderValue);
            }

            if (part == HeadPart.End)
            {
                reader.AdvanceTo(buffer.Start);
                return builder.Build();
            }

            reader.AdvanceTo(buffer.Start, buffer.End);
            if (result.IsCompleted)
                return null;
        }
    }

    private async Task<bool> HandleRequestAsync(
        RequestHead head, TimedPipeReader reader, Stream output, RequestDelegate app, bool mayKeepAlive,
        CancellationToken ct)
    {
        var body = CreateBody(head, reader);
        if (head.ExpectContinue && head.HasBody())
            await ResponseWriter.WriteContinueAsync(output, ct);
        reader.StartPhase(_limits.BodyGracePeriod, _limits.MinBodyBytesPerSecond);

        var request = new HttpRequest(head.Method, head.Path, head.RawPath, head.Protocol, _settings,
            head.Headers, head.Query, body, head.HasBody(), ct);

        await using var scope = _container.CreateScope();
        var response = await app(request, new HttpContext(scope, _settings));

        var keepAlive = head.KeepAlive && mayKeepAlive
                        && !string.Equals(response.Headers.GetOrNull(HeaderNames.Connection), "close",
                            StringComparison.OrdinalIgnoreCase);
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

    private static async Task<bool> TryDrainAsync(Stream body, CancellationToken ct)
    {
        try
        {
            await body.CopyToAsync(Stream.Null, ct);
            return true;
        }
        catch (RequestException)
        {
            return false;
        }
    }
}