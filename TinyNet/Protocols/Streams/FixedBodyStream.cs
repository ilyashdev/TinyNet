using System.Buffers;
using System.IO.Pipelines;

namespace TinyNet.Protocols.Streams;

internal sealed class FixedBodyStream : Stream
{
    private readonly PipeReader _reader;
    private long _remaining;

    public FixedBodyStream(PipeReader reader, long length)
    {
        _reader = reader;
        _remaining = length;
    }

    public override async ValueTask<int> ReadAsync(Memory<byte> destination, CancellationToken ct = default)
    {
        if (_remaining == 0 || destination.IsEmpty)
            return 0;

        var result = await _reader.ReadAsync(ct);
        if (result.IsCanceled)
            throw new OperationCanceledException(ct);
        var buffer = result.Buffer;
        if (buffer.IsEmpty && result.IsCompleted)
            throw new IOException("Connection closed before the request body was complete.");

        var count = (int)Math.Min(Math.Min(buffer.Length, destination.Length), _remaining);
        buffer.Slice(0, count).CopyTo(destination.Span);
        _reader.AdvanceTo(buffer.GetPosition(count));
        _remaining -= count;
        return count;
    }

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken ct)
        => ReadAsync(buffer.AsMemory(offset, count), ct).AsTask();

    public override int Read(byte[] buffer, int offset, int count)
        => throw new NotSupportedException("Synchronous reads are not supported. Use ReadAsync.");

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