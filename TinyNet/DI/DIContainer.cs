

using System.Collections.Concurrent;
using TinyNet.Middlewares;

namespace TinyNet.DI;

public class DIContainer
{
    private readonly ConcurrentDictionary<Type, object> _singletonInstances = new();
    private readonly List<ServiceDescriptor> _descriptors = new(); 
    private readonly HashSet<Type> _instanceTypes = new();

    public void AddTransient<TService, TImplementation>() where TImplementation : TService
        => Register<TService, TImplementation>(ServiceLifetime.Transient);

    public void AddTransient<TImplementation>()
        => Register<TImplementation, TImplementation>(ServiceLifetime.Transient);
    
    public void AddScoped<TService, TImplementation>() where TImplementation : TService
        => Register<TService, TImplementation>(ServiceLifetime.Scoped);
    public void AddScoped<TImplementation>()
        => Register<TImplementation, TImplementation>(ServiceLifetime.Scoped);
    
    public void AddSingleton<TService, TImplementation>() where TImplementation : TService
        => Register<TService, TImplementation>(ServiceLifetime.Singleton);
    public void AddSingleton<TImplementation>()
        => Register<TImplementation, TImplementation>(ServiceLifetime.Singleton);

    public void AddInstance<TService>(TService instance)
    {
        AddSingleton<TService>();
        _singletonInstances.TryAdd(typeof(TService), instance);
    }

    private void Register<TService, TImplementation>(ServiceLifetime lifetime)
    {
        if(_descriptors.Where(c => c.ServiceType == typeof(TService)).Any())
            throw new Exception($"Service type {typeof(TService)} is already registered");
        _descriptors.Add(new ServiceDescriptor(typeof(TService), typeof(TImplementation), lifetime));
    }


    public TService GetService<TService>(DIScope scope) 
        => (TService)GetService(typeof(TService), scope);

    private object GetService(Type serviceType, DIScope scope)
    {
        var descriptor = _descriptors.FirstOrDefault(d => d.ServiceType == serviceType)
                         ?? throw new InvalidOperationException($"Service {serviceType.Name} not registered");
        try
        {

            return descriptor.Lifetime switch
            {
                ServiceLifetime.Transient => CreateInstance(descriptor.ImplementationType, scope),
                ServiceLifetime.Scoped => GetInstance(descriptor, scope._scopedInstances, scope),
                ServiceLifetime.Singleton => GetInstance(descriptor, _singletonInstances, scope),
                _ => throw new ArgumentOutOfRangeException()
            };
        }
        finally
        {
            _instanceTypes.Clear();
        }
    }

    internal Middleware GetMiddleware(Type middlewareType,RequestDelegate next, DIScope scope)
    {
        var ctor = middlewareType.GetConstructors()
            .OrderByDescending(c => c.GetParameters().Length)
            .First();
        var paramsInfo = ctor.GetParameters();
        var parameters = new object[paramsInfo.Length];
        foreach (var parameter in paramsInfo)
        {
            if (parameter.ParameterType == typeof(RequestDelegate))
                parameters[paramsInfo.Length - 1] = next;
            else
            parameters[paramsInfo.Length - 1] = GetService(parameter.ParameterType, scope);
        }
        return (Middleware)ctor.Invoke(parameters);
    }

    internal object GetInstance(ServiceDescriptor descriptor,  IDictionary<Type, object> instances, DIScope scope)
    {
        if (!instances.ContainsKey(descriptor.ServiceType))
        {
            var instance = CreateInstance(descriptor.ImplementationType, scope);
            instances.Add(descriptor.ServiceType, instance);
            return instance;
        }
         return instances[descriptor.ServiceType];
    }
    
    private object CreateInstance(Type type, DIScope scope)
    {
        // if (_instanceTypes.Contains(type))
        //     throw new InvalidOperationException($"Service of type {type.Name} has a cyclic dependency.");
        //_instanceTypes.Add(type);
        var ctor = type.GetConstructors()
            .OrderByDescending(c => c.GetParameters().Length)
            .First();

        var parameters = ctor.GetParameters()
            .Select(p => GetService(p.ParameterType, scope))
            .ToArray();

        return Activator.CreateInstance(type, parameters)!;
    }
    
}