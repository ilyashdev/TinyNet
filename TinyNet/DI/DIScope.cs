namespace TinyNet.DI;

public class DIScope : IAsyncDisposable
{ 
    private DIContainer _container;
    private readonly Dictionary<Type, object> _scopedInstances = new();
    private readonly Stack<object> _disposables = new();
    private readonly Lock _lock = new();
    private bool _isDisposed = false;

    public DIScope(DIContainer container)
    {
        _container = container;
    }

    public T GetService<T>()
    => (T)GetService(typeof(T));
    public object GetService(Type type)
    {
        lock (_lock)
        {
            if (_isDisposed)
                throw new InvalidOperationException("Scope is disposed");
            return _container.GetService(type, this) ?? throw new InvalidOperationException("Service not found");
        }
    }

    internal void AddScopedInstance(Type objType, object instance)
    {
        lock (_lock)
        {
            if (_scopedInstances.TryAdd(objType, instance))
                if(instance is IDisposable || instance is IAsyncDisposable)
                    _disposables.Push(instance);
        }
    }
    
    internal bool TryGetValue(Type objType, out object instance)
    {
        lock (_lock)
        {
            return _scopedInstances.TryGetValue(objType, out instance);
        }
    }
    

    public async ValueTask DisposeAsync()
    {
        List<Exception>? errors = new();
        _isDisposed = true;
        while (_disposables.TryPop(out var disposable))
        {
            try
            {
                if (disposable is IAsyncDisposable asyncDisposable)
                    await asyncDisposable.DisposeAsync();
                else if (disposable is IDisposable syncDisposable)
                    syncDisposable.Dispose();
            }
            catch(Exception ex)
            {
                errors.Add(ex);
            }
        }
        if(errors.Count > 0)
            throw new AggregateException(errors);
    }
}