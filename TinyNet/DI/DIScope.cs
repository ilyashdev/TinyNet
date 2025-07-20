namespace TinyNet.DI;

public class DIScope : IDisposable
{
    public readonly Dictionary<Type, object> _scopedInstances = new();
    
    public void Dispose()
    {
        _scopedInstances.Clear();
    }
}