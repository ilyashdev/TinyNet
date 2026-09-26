namespace TinyNet.DI;

public class DIScope : IDisposable
{
    private DIContainer _container;
    internal readonly Dictionary<Type, object> _scopedInstances = new();

    public DIScope(DIContainer container)
    {
        _container = container;
    }

    public T GetService<T>()
    => (T)GetService(typeof(T));
    public object GetService(Type type)
        => _container.GetService(type, this) ??  throw new InvalidOperationException("Service not found");

    public void Dispose()
    {
        _scopedInstances.Clear();
    }
}