namespace TinyNet.DI;

public class DIScope : IDisposable
{
    internal readonly Dictionary<Type, object> _scopedInstances = new();
    
    public void Dispose()
    {
        _scopedInstances.Clear();
    }
}