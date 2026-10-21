using TinyNet.Exceptions;
using TinyNet.Http;

namespace TinyNet.Protocols.Streams;

internal abstract class BodyStream : Stream
{
    private bool _failed;

    public sealed override async ValueTask<int> ReadAsync(Memory<byte> destination, CancellationToken ct = default)
    {
        if (_failed)
            throw new ProtocolException("Request body could not be read.", StatusCodes.BadRequest);
        try
        {
            return await ReadBodyAsync(destination, ct);
        }
        catch (TimeoutException)
        {
            _failed = true;
            throw new ProtocolException("Request body timeout.", StatusCodes.RequestTimeout);
        }
        catch (IOException e)
        {
            _failed = true;
            throw new ProtocolException(e.Message, StatusCodes.BadRequest);
        }
        catch
        {
            _failed = true;
            throw;
        }
    }

    protected abstract ValueTask<int> ReadBodyAsync(Memory<byte> destination, CancellationToken ct);

    public sealed override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken ct)
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