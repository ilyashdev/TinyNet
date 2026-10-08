using System.Buffers;
using System.Globalization;
using System.Text;
using TinyNet.Http;

namespace TinyNet.Protocols.Http1;

internal static class ResponseWriter
{
    private const int CombinedWriteLimit = 4096;

    private static readonly byte[] ContinueResponse = "HTTP/1.1 100 Continue\r\n\r\n"u8.ToArray();

    public static async Task WriteContinueAsync(Stream output, CancellationToken ct)
    {
        await output.WriteAsync(ContinueResponse, ct);
        await output.FlushAsync(ct);
    }

    public static async Task<bool> WriteAsync(
        Stream output, HttpResponse response, string protocol, bool isHead, bool keepAlive, CancellationToken ct)
    {
        var status = response.StatusCode;
        var bodyAllowed = status is not (StatusCodes.NoContent or StatusCodes.ResetContent or StatusCodes.NotModified);
        var isStream = response.Stream is not null;
        long? length = isStream ? response.ContentLength : response.Buffer.Length;
        var chunked = bodyAllowed && length is null && protocol == "HTTP/1.1";
        if (bodyAllowed && length is null && !chunked)
            keepAlive = false;

        var head = Encoding.Latin1.GetBytes(BuildHead(response, protocol, bodyAllowed, length, chunked, keepAlive));
        var sendBody = bodyAllowed && !isHead;

        if (!sendBody || isStream)
            await output.WriteAsync(head, ct);
        else if (response.Buffer.Length <= CombinedWriteLimit)
            await output.WriteAsync(Concat(head, response.Buffer.Span), ct);
        else
        {
            await output.WriteAsync(head, ct);
            await output.WriteAsync(response.Buffer, ct);
        }

        if (sendBody && isStream && !await TryWriteStreamAsync(output, response, length, chunked, ct))
            return false;

        await output.FlushAsync(ct);
        return keepAlive;
    }

    private static string BuildHead(
        HttpResponse response, string protocol, bool bodyAllowed, long? length, bool chunked, bool keepAlive)
    {
        var status = response.StatusCode;
        var head = new StringBuilder(256)
            .Append("HTTP/1.1 ")
            .Append(status.ToString(CultureInfo.InvariantCulture))
            .Append(' ')
            .Append(ReasonPhrase(status))
            .Append("\r\n");

        if (response.Headers.GetOrNull(HeaderNames.Date) is null)
            AppendHeader(head, HeaderNames.Date, DateTime.UtcNow.ToString("R", CultureInfo.InvariantCulture));

        foreach (var (name, values) in response.Headers.All)
        {
            if (name.Equals(HeaderNames.Connection, StringComparison.OrdinalIgnoreCase))
                continue;
            foreach (var value in values)
                AppendHeader(head, name, value);
        }

        if (bodyAllowed && response.ContentType is not null && response.Headers.GetOrNull(HeaderNames.ContentType) is null)
            AppendHeader(head, HeaderNames.ContentType, response.ContentType);

        if (status == StatusCodes.ResetContent)
            AppendHeader(head, HeaderNames.ContentLength, "0");
        else if (bodyAllowed && length is not null)
            AppendHeader(head, HeaderNames.ContentLength, length.Value.ToString(CultureInfo.InvariantCulture));
        else if (chunked)
            AppendHeader(head, HeaderNames.TransferEncoding, HeaderNames.EncodingTypes.Chunked);

        if (!keepAlive)
            AppendHeader(head, HeaderNames.Connection, "close");
        else if (protocol == "HTTP/1.0")
            AppendHeader(head, HeaderNames.Connection, "keep-alive");

        return head.Append("\r\n").ToString();
    }

    private static async Task<bool> TryWriteStreamAsync(
        Stream output, HttpResponse response, long? length, bool chunked, CancellationToken ct)
    {
        try
        {
            if (chunked)
            {
                var body = new ChunkedWriteStream(output);
                await response.Stream!(body, ct);
                await body.CompleteAsync(ct);
            }
            else
            {
                var body = new FixedWriteStream(output, length ?? long.MaxValue);
                await response.Stream!(body, ct);
                if (length is not null)
                    body.EnsureCompleted();
            }

            return true;
        }
        catch (Exception e) when (e is not (OperationCanceledException or IOException))
        {
            Console.WriteLine($"Response body error: {e}");
            return false;
        }
    }

    private static void AppendHeader(StringBuilder head, string name, string value)
        => head.Append(name).Append(": ").Append(value).Append("\r\n");

    private static byte[] Concat(byte[] head, ReadOnlySpan<byte> body)
    {
        var payload = new byte[head.Length + body.Length];
        head.CopyTo(payload, 0);
        body.CopyTo(payload.AsSpan(head.Length));
        return payload;
    }

    private static string ReasonPhrase(int status) => status switch
    {
        StatusCodes.OK => "OK",
        StatusCodes.Created => "Created",
        StatusCodes.Accepted => "Accepted",
        StatusCodes.NonAuthoritativeInformation => "Non-Authoritative Information",
        StatusCodes.NoContent => "No Content",
        StatusCodes.ResetContent => "Reset Content",
        StatusCodes.PartialContent => "Partial Content",
        StatusCodes.MultipleChoices => "Multiple Choices",
        StatusCodes.MovedPermanently => "Moved Permanently",
        StatusCodes.Found => "Found",
        StatusCodes.SeeOther => "See Other",
        StatusCodes.NotModified => "Not Modified",
        StatusCodes.TemporaryRedirect => "Temporary Redirect",
        StatusCodes.PermanentRedirect => "Permanent Redirect",
        StatusCodes.BadRequest => "Bad Request",
        StatusCodes.Unauthorized => "Unauthorized",
        StatusCodes.Forbidden => "Forbidden",
        StatusCodes.NotFound => "Not Found",
        StatusCodes.MethodNotAllowed => "Method Not Allowed",
        StatusCodes.NotAcceptable => "Not Acceptable",
        StatusCodes.ProxyAuthenticationRequired => "Proxy Authentication Required",
        StatusCodes.RequestTimeout => "Request Timeout",
        StatusCodes.Conflict => "Conflict",
        StatusCodes.Gone => "Gone",
        StatusCodes.LengthRequired => "Length Required",
        StatusCodes.PreconditionFailed => "Precondition Failed",
        StatusCodes.RequestEntityTooLarge => "Content Too Large",
        StatusCodes.RequestUriTooLong => "URI Too Long",
        StatusCodes.UnsupportedMediaType => "Unsupported Media Type",
        StatusCodes.RangeNotSatisfiable => "Range Not Satisfiable",
        StatusCodes.ExpectationFailed => "Expectation Failed",
        StatusCodes.ImATeapot => "I'm a teapot",
        StatusCodes.MisdirectedRequest => "Misdirected Request",
        StatusCodes.UnprocessableContent => "Unprocessable Content",
        StatusCodes.UpgradeRequired => "Upgrade Required",
        StatusCodes.PreconditionRequired => "Precondition Required",
        StatusCodes.TooManyRequests => "Too Many Requests",
        StatusCodes.RequestHeaderFieldsTooLarge => "Request Header Fields Too Large",
        StatusCodes.InternalServerError => "Internal Server Error",
        StatusCodes.NotImplemented => "Not Implemented",
        StatusCodes.BadGateway => "Bad Gateway",
        StatusCodes.ServiceUnavailable => "Service Unavailable",
        StatusCodes.GatewayTimeout => "Gateway Timeout",
        StatusCodes.HttpVersionNotSupported => "HTTP Version Not Supported",
        _ => ""
    };

    private abstract class WriteOnlyStream : Stream
    {
        public override void Write(byte[] buffer, int offset, int count)
            => throw new NotSupportedException("Synchronous writes are not supported. Use WriteAsync.");

        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken ct)
            => WriteAsync(buffer.AsMemory(offset, count), ct).AsTask();

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }
    }

    private sealed class FixedWriteStream(Stream inner, long length) : WriteOnlyStream
    {
        private long _remaining = length;

        public override ValueTask WriteAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default)
        {
            if (data.Length > _remaining)
                throw new InvalidOperationException("Response body is longer than the declared length.");
            _remaining -= data.Length;
            return inner.WriteAsync(data, ct);
        }

        public override Task FlushAsync(CancellationToken ct) => inner.FlushAsync(ct);

        public void EnsureCompleted()
        {
            if (_remaining != 0)
                throw new InvalidOperationException("Response body is shorter than the declared length.");
        }
    }

    private sealed class ChunkedWriteStream(Stream inner) : WriteOnlyStream
    {
        private static readonly byte[] LastChunk = "0\r\n\r\n"u8.ToArray();

        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default)
        {
            if (data.IsEmpty)
                return;

            var size = data.Length.ToString("X", CultureInfo.InvariantCulture);
            var length = size.Length + 2 + data.Length + 2;
            var buffer = ArrayPool<byte>.Shared.Rent(length);
            try
            {
                var offset = Encoding.ASCII.GetBytes(size, buffer);
                "\r\n"u8.CopyTo(buffer.AsSpan(offset));
                data.Span.CopyTo(buffer.AsSpan(offset + 2));
                "\r\n"u8.CopyTo(buffer.AsSpan(offset + 2 + data.Length));
                await inner.WriteAsync(buffer.AsMemory(0, length), ct);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }

        public override Task FlushAsync(CancellationToken ct) => inner.FlushAsync(ct);

        public ValueTask CompleteAsync(CancellationToken ct) => inner.WriteAsync(LastChunk, ct);
    }
}