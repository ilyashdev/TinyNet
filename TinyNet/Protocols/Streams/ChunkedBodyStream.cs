using System.Buffers;
using System.Buffers.Text;
using System.IO.Pipelines;
using TinyNet.Exceptions;

namespace TinyNet.Protocols.Streams;

internal sealed class ChunkedBodyStream : Stream
{
    private const int MaxLineLength = 4096;

    private static readonly SearchValues<byte> HexDigits = SearchValues.Create("0123456789ABCDEFabcdef"u8);

    private static readonly SearchValues<byte> TokenChars =
        SearchValues.Create("!#$%&'*+-.^_`|~0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz"u8);

    private static readonly SearchValues<byte> FieldValueChars =
        SearchValues.Create(Enumerable.Range(0, 256).Where(b => b == '\t' || b is >= 0x20 and not 0x7F)
            .Select(b => (byte)b).ToArray());

    private static ReadOnlySpan<byte> Whitespace => " \t"u8;

    private readonly PipeReader _reader;
    private readonly long _maxLength;
    private long _total;
    private long _chunkRemaining;
    private bool _chunkEndPending;
    private bool _completed;

    public ChunkedBodyStream(PipeReader reader, long maxLength)
    {
        _reader = reader;
        _maxLength = maxLength;
    }

    public override async ValueTask<int> ReadAsync(Memory<byte> destination, CancellationToken ct = default)
    {
        if (_completed || destination.IsEmpty)
            return 0;

        if (_chunkRemaining == 0)
        {
            if (_chunkEndPending)
            {
                if ((await ReadLineAsync(ct)).Length != 0)
                    throw Malformed();
                _chunkEndPending = false;
            }

            _chunkRemaining = ParseChunkSize(await ReadLineAsync(ct));
            if (_chunkRemaining == 0)
            {
                await SkipTrailersAsync(ct);
                _completed = true;
                return 0;
            }

            _total += _chunkRemaining;
            _chunkEndPending = true;
        }

        var result = await _reader.ReadAsync(ct);
        if (result.IsCanceled)
            throw new OperationCanceledException(ct);
        var buffer = result.Buffer;
        if (buffer.IsEmpty && result.IsCompleted)
            throw new IOException("Connection closed before the request body was complete.");

        var count = (int)Math.Min(Math.Min(buffer.Length, destination.Length), _chunkRemaining);
        buffer.Slice(0, count).CopyTo(destination.Span);
        _reader.AdvanceTo(buffer.GetPosition(count));
        _chunkRemaining -= count;
        return count;
    }

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken ct)
        => ReadAsync(buffer.AsMemory(offset, count), ct).AsTask();

    public override int Read(byte[] buffer, int offset, int count)
        => throw new NotSupportedException("Synchronous reads are not supported. Use ReadAsync.");

    private long ParseChunkSize(byte[] line)
    {
        var span = line.AsSpan();
        var end = span.IndexOfAnyExcept(HexDigits);
        if (end < 0)
            end = span.Length;
        var digits = span[..end].TrimStart((byte)'0');
        ulong size = 0;
        if (end == 0
            || digits.Length > 16
            || !IsChunkExtensions(span[end..])
            || (!digits.IsEmpty && !Utf8Parser.TryParse(digits, out size, out _, 'X')))
            throw Malformed();
        if (size > (ulong)(_maxLength - _total))
            throw new ProtocolException("Request body is too large.", 413);
        return (long)size;
    }

    private static bool IsChunkExtensions(ReadOnlySpan<byte> s)
    {
        while (!s.IsEmpty)
        {
            s = s.TrimStart(Whitespace);
            if (s.IsEmpty || s[0] != (byte)';')
                return false;
            s = s[1..].TrimStart(Whitespace);
            var name = TokenLength(s);
            if (name == 0)
                return false;
            s = s[name..];

            var rest = s.TrimStart(Whitespace);
            if (rest.IsEmpty || rest[0] != (byte)'=')
                continue;
            s = rest[1..].TrimStart(Whitespace);
            var value = !s.IsEmpty && s[0] == (byte)'"' ? QuotedStringLength(s) : TokenLength(s);
            if (value == 0)
                return false;
            s = s[value..];
        }

        return true;
    }

    private static int TokenLength(ReadOnlySpan<byte> s)
    {
        var end = s.IndexOfAnyExcept(TokenChars);
        return end < 0 ? s.Length : end;
    }

    private static int QuotedStringLength(ReadOnlySpan<byte> s)
    {
        for (var i = 1; i < s.Length; i++)
        {
            var b = s[i];
            if (b == (byte)'"')
                return i + 1;
            if (b == (byte)'\\')
            {
                if (i + 1 >= s.Length || !FieldValueChars.Contains(s[i + 1]))
                    return 0;
                i++;
            }
            else if (!FieldValueChars.Contains(b))
                return 0;
        }

        return 0;
    }

    private static bool IsFieldLine(ReadOnlySpan<byte> line)
    {
        var name = TokenLength(line);
        return name > 0
               && name < line.Length
               && line[name] == (byte)':'
               && !line[(name + 1)..].ContainsAnyExcept(FieldValueChars);
    }

    private async ValueTask SkipTrailersAsync(CancellationToken ct)
    {
        var length = 0;
        while (true)
        {
            var line = await ReadLineAsync(ct);
            if (line.Length == 0)
                return;
            length += line.Length;
            if (length > MaxLineLength || !IsFieldLine(line))
                throw Malformed();
        }
    }

    private async ValueTask<byte[]> ReadLineAsync(CancellationToken ct)
    {
        var scanned = 0L;
        while (true)
        {
            var result = await _reader.ReadAsync(ct);
            if (result.IsCanceled)
                throw new OperationCanceledException(ct);
            var buffer = result.Buffer;

            if (buffer.Slice(scanned).PositionOf((byte)'\n') is { } lf)
            {
                var line = buffer.Slice(0, lf);
                var valid = line.Length > 0
                            && line.Length - 1 <= MaxLineLength
                            && line.Slice(line.Length - 1).FirstSpan[0] == (byte)'\r';
                var content = valid ? line.Slice(0, line.Length - 1).ToArray() : null;
                _reader.AdvanceTo(buffer.GetPosition(1, lf));
                return content ?? throw Malformed();
            }

            scanned = buffer.Length;
            var tooLong = buffer.Length > MaxLineLength;
            _reader.AdvanceTo(buffer.Start, buffer.End);
            if (tooLong)
                throw Malformed();
            if (result.IsCompleted)
                throw new IOException("Connection closed before the request body was complete.");
        }
    }

    private static ProtocolException Malformed()
        => new("Malformed chunked request body.", 400);

    public override void Flush()
    {
    }

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }
}