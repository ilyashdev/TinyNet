namespace TinyNet.Example.Services;

public class VisitCounter
{
    private int _visits;

    public int Next() => Interlocked.Increment(ref _visits);
}