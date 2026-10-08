using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq.Expressions;

namespace TinyNet.DI;

public class DIContainer : IAsyncDisposable
{
    private readonly DIScope _rootScope;
    private bool _frozen;
    private readonly ConcurrentDictionary<Type, Lazy<object>> _singletonInstances = new();
    private readonly ConcurrentStack<object> _singletonDisposables = new();
    private readonly Dictionary<Type, ServiceDescriptor> _descriptors = new();
    private Dictionary<Type, Func<object[], object>> _factories;
    private Dictionary<Type, Type[]> _ctorParams;

    public DIContainer()
    {
        _rootScope = new DIScope(this);
    }

    public void AddTransient<TService, TImplementation>() where TImplementation : TService
        => AddTransient(typeof(TService), typeof(TImplementation));

    public void AddTransient<TImplementation>()
        => AddTransient(typeof(TImplementation), typeof(TImplementation));

    public void AddTransient(Type type)
        => AddTransient(type, type);

    public void AddTransient(Type serviceType, Type implementationType)
        => Register(serviceType, implementationType, ServiceLifetime.Transient);

    public void AddScoped<TService, TImplementation>() where TImplementation : TService
        => AddScoped(typeof(TService), typeof(TImplementation));

    public void AddScoped<TImplementation>()
        => AddScoped(typeof(TImplementation), typeof(TImplementation));

    public void AddScoped(Type type)
        => AddScoped(type, type);

    public void AddScoped(Type serviceType, Type implementationType)
        => Register(serviceType, implementationType, ServiceLifetime.Scoped);

    public DIScope CreateScope() => new DIScope(this);

    public void AddSingleton<TService, TImplementation>() where TImplementation : TService
        => AddSingleton(typeof(TService), typeof(TImplementation));

    public void AddSingleton<TImplementation>()
        => AddSingleton(typeof(TImplementation), typeof(TImplementation));

    public void AddSingleton(Type type)
        => AddSingleton(type, type);

    public void AddSingleton(Type serviceType, Type implementationType)
        => Register(serviceType, implementationType, ServiceLifetime.Singleton);

    internal void Freeze()
    {
        var diServices = _descriptors.Where(d => !_singletonInstances.ContainsKey(d.Key));
        _ctorParams = diServices.ToDictionary(kv => kv.Value.ServiceType, kv => kv.Value.ImplementationType
            .GetConstructors()
            .OrderByDescending(c => c.GetParameters().Length)
            .First().GetParameters().Select(p => p.ParameterType).ToArray());
        _factories =
            diServices.ToDictionary(kv => kv.Value.ServiceType, kv => BuildFactory(kv.Value.ImplementationType));
        _frozen = true;
    }

    public void AddInstance<TService>(TService instance)
    {
        AddSingleton<TService>();
        if (instance is IDisposable || instance is IAsyncDisposable)
            _singletonDisposables.Push(instance);
        _singletonInstances.TryAdd(typeof(TService), new Lazy<object>(instance!));
    }

    private void Register<TService, TImplementation>(ServiceLifetime lifetime) =>
        Register(typeof(TService), typeof(TImplementation), lifetime);

    private void Register(Type serviceType, Type implementationType, ServiceLifetime lifetime)
    {
        if (_frozen)
            throw new InvalidOperationException($"Cannot register {serviceType} after Build()");
        if (_descriptors.ContainsKey(serviceType))
            throw new Exception($"Service type {serviceType} is already registered");
        _descriptors[serviceType] = new ServiceDescriptor(serviceType, implementationType, lifetime);
    }

    public void Validate()
    {
        foreach (var descriptor in _descriptors.Values)
        {
            if (descriptor.Lifetime != ServiceLifetime.Singleton)
                continue;
            if (_singletonInstances.ContainsKey(descriptor.ServiceType))
                continue;
            ValidateDependencies(descriptor, new List<ServiceDescriptor> { descriptor }, new HashSet<Type>());
        }
    }

    private void ValidateDependencies(ServiceDescriptor descriptor, List<ServiceDescriptor> chain,
        HashSet<Type> visited)
    {
        if (!visited.Add(descriptor.ImplementationType))
            return;
        var ctor = descriptor.ImplementationType.GetConstructors()
            .OrderByDescending(c => c.GetParameters().Length)
            .FirstOrDefault();
        if (ctor is null)
            return;
        foreach (var parameter in ctor.GetParameters())
        {
            if (!_descriptors.TryGetValue(parameter.ParameterType, out var dependency))
                continue;
            chain.Add(dependency);
            if (dependency.Lifetime == ServiceLifetime.Scoped)
                throw new InvalidOperationException($"Captive dependency: {FormatChain(chain)}");
            if (dependency.Lifetime == ServiceLifetime.Transient)
                ValidateDependencies(dependency, chain, visited);
            chain.RemoveAt(chain.Count - 1);
        }
    }

    private static string FormatChain(IEnumerable<ServiceDescriptor> chain)
        => string.Join(" -> ", chain.Select(d => $"{d.ServiceType.Name} ({d.Lifetime})"));

    internal object GetSingleton(Type serviceType)
    {
        if (!_descriptors.TryGetValue(serviceType, out var descriptor))
            throw new UnreachableException($"Service type {serviceType.Name} not registered");
        if (descriptor.Lifetime != ServiceLifetime.Singleton)
            throw new UnreachableException($"Service type {serviceType.Name} not singleton");
        return GetSingleton(descriptor, new());
    }

    internal object GetService(Type serviceType, DIScope scope)
        => GetService(serviceType, scope, new());

    public bool IsService(Type serviceType)
        => _descriptors.ContainsKey(serviceType);

    private object GetService(Type serviceType, DIScope scope, HashSet<Type> resolving)
    {
        if (!_frozen)
            throw new InvalidOperationException("Container is not built");
        if (!_descriptors.TryGetValue(serviceType, out var descriptor))
            throw new InvalidOperationException($"Service {serviceType.Name} not registered");
        return descriptor.Lifetime switch
        {
            ServiceLifetime.Transient => CreateInstance(descriptor.ServiceType, scope, resolving),
            ServiceLifetime.Scoped => GetScoped(descriptor, scope, resolving),
            ServiceLifetime.Singleton => GetSingleton(descriptor, resolving),
            _ => throw new ArgumentOutOfRangeException()
        };
    }

    private object GetSingleton(ServiceDescriptor descriptor, HashSet<Type> resolving)
    {
        var lazy = _singletonInstances.GetOrAdd(
            descriptor.ServiceType,
            _ => new Lazy<object>(
                () =>
                {
                    var instance = CreateInstance(descriptor.ServiceType, _rootScope, resolving);
                    if (instance is IDisposable || instance is IAsyncDisposable)
                        _singletonDisposables.Push(instance);
                    return instance;
                },
                LazyThreadSafetyMode.ExecutionAndPublication));

        return lazy.Value;
    }

    private object GetScoped(ServiceDescriptor descriptor, DIScope scope, HashSet<Type> resolving)
    {
        if (!scope.TryGetValue(descriptor.ServiceType, out var existing))
        {
            existing = CreateInstance(descriptor.ServiceType, scope, resolving);
            scope.AddScopedInstance(descriptor.ServiceType, existing);
        }

        return existing;
    }

    private object CreateInstance(Type type, DIScope scope, HashSet<Type> resolving)
    {
        if (!resolving.Add(type))
            throw new InvalidOperationException($"Type {type} has cyclic dependency");
        try
        {
            var factory = _factories[type];
            var paramTypes = _ctorParams[type];
            var parameters = paramTypes.Select(p => GetService(p, scope, resolving)).ToArray();
            return factory(parameters);
        }
        finally
        {
            resolving.Remove(type);
        }
    }

    private static Func<object[], object> BuildFactory(Type type)
    {
        var ctor = type.GetConstructors().OrderByDescending(c => c.GetParameters().Length).First();
        var param = Expression.Parameter(typeof(object[]), "args");
        var ctorParams = ctor.GetParameters().Select((p, i) =>
            Expression.Convert(Expression.ArrayIndex(param, Expression.Constant(i)), p.ParameterType));
        var newExpr = Expression.New(ctor, ctorParams);
        return Expression.Lambda<Func<object[], object>>(Expression.Convert(newExpr, typeof(object)), param).Compile();
    }

    public async ValueTask DisposeAsync()
    {
        List<Exception>? errors = new();
        while (_singletonDisposables.TryPop(out var disposable))
        {
            try
            {
                if (disposable is IAsyncDisposable asyncDisposable)
                    await asyncDisposable.DisposeAsync();
                else if (disposable is IDisposable syncDisposable)
                    syncDisposable.Dispose();
            }
            catch (Exception ex)
            {
                errors.Add(ex);
            }
        }

        if (errors.Count > 0)
            throw new AggregateException(errors);
    }
}