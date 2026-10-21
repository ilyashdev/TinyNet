using System.Buffers;
using System.IO.Pipelines;

namespace TinyNet.Protocols.Streams;

internal sealed class FixedBodyStream : BodyStream
{
    private readonly PipeReader _reader;
    private long _remaining;

    public FixedBodyStream(PipeReader reader, long length)
    {
        _reader = reader;
        _remaining = length;
    }

    protected override async ValueTask<int> ReadBodyAsync(Memory<byte> destination, CancellationToken ct)
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
}