using System.Buffers;
using System.Diagnostics;
using System.IO.Pipelines;

namespace TinyNet.Protocols.Http1;

internal sealed class TimedPipeReader : PipeReader
{
    private readonly PipeReader _inner;
    private CancellationTokenSource _timeout = new();
    private ReadOnlySequence<byte> _last;
    private TimeSpan _grace;
    private double _minBytesPerSecond;
    private TimeSpan _waited;
    private long _consumed;

    public TimedPipeReader(PipeReader inner)
    {
        _inner = inner;
    }

    public void StartPhase(TimeSpan grace, double minBytesPerSecond = 0)
    {
        _grace = grace;
        _minBytesPerSecond = minBytesPerSecond;
        _waited = TimeSpan.Zero;
        _consumed = 0;
    }

    public override async ValueTask<ReadResult> ReadAsync(CancellationToken ct = default)
    {
        var earned = _minBytesPerSecond > 0 ? TimeSpan.FromSeconds(_consumed / _minBytesPerSecond) : TimeSpan.Zero;
        var remaining = _grace + earned - _waited;
        if (remaining <= TimeSpan.Zero)
            throw new TimeoutException("Client is sending data too slowly.");

        if (!_timeout.TryReset())
        {
            _timeout.Dispose();
            _timeout = new CancellationTokenSource();
        }

        _timeout.CancelAfter(remaining);
        using var registration = ct.UnsafeRegister(static s => ((PipeReader)s!).CancelPendingRead(), _inner);
        var started = Stopwatch.GetTimestamp();
        try
        {
            var result = await _inner.ReadAsync(_timeout.Token);
            _last = result.Buffer;
            return result;
        }
        catch (OperationCanceledException) when (_timeout.IsCancellationRequested)
        {
            throw new TimeoutException("Client is sending data too slowly.");
        }
        finally
        {
            _timeout.CancelAfter(Timeout.InfiniteTimeSpan);
            _waited += Stopwatch.GetElapsedTime(started);
        }
    }

    public override bool TryRead(out ReadResult result)
    {
        if (!_inner.TryRead(out result))
            return false;
        _last = result.Buffer;
        return true;
    }

    public override void AdvanceTo(SequencePosition consumed) => AdvanceTo(consumed, consumed);

    public override void AdvanceTo(SequencePosition consumed, SequencePosition examined)
    {
        _consumed += _last.Slice(_last.Start, consumed).Length;
        _inner.AdvanceTo(consumed, examined);
    }

    public override void CancelPendingRead() => _inner.CancelPendingRead();

    public override void Complete(Exception? exception = null)
    {
        _timeout.Dispose();
        _inner.Complete(exception);
    }
}