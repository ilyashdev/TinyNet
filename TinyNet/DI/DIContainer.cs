

using System.Collections.Concurrent;
using TinyNet.Middlewares;

namespace TinyNet.DI;

public class DIContainer
{
    private readonly ConcurrentDictionary<Type, object> _singletonInstances = new();
    private readonly ConcurrentBag<ServiceDescriptor> _descriptors = new(); 

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
        => (TService)GetService(typeof(TService), scope, new());

    private object GetService(Type serviceType, DIScope scope, HashSet<Type> resolving)
    {
        var descriptor = _descriptors.FirstOrDefault(d => d.ServiceType == serviceType)
                         ?? throw new InvalidOperationException($"Service {serviceType.Name} not registered");
            return descriptor.Lifetime switch
            {
                ServiceLifetime.Transient => CreateInstance(descriptor.ImplementationType, scope,resolving),
                ServiceLifetime.Scoped => GetInstance(descriptor, scope._scopedInstances, scope, resolving),
                ServiceLifetime.Singleton => GetInstance(descriptor, _singletonInstances, scope, resolving),
                _ => throw new ArgumentOutOfRangeException()
            };
    }

    internal Middleware GetMiddleware(Type middlewareType,RequestDelegate next, DIScope scope)
    {
        var ctor = middlewareType.GetConstructors()
            .OrderByDescending(c => c.GetParameters().Length)
            .First();
        var paramsInfo = ctor.GetParameters();
        var parameters = new object[paramsInfo.Length];
        for (int i = 0; i < paramsInfo.Length; i++)
        {
            if (paramsInfo[i].ParameterType == typeof(RequestDelegate))
                parameters[i] = next;
            else
                parameters[i] = GetService(paramsInfo[i].ParameterType, scope, new());
        }
        return (Middleware)ctor.Invoke(parameters);
    }

    internal object GetInstance(ServiceDescriptor descriptor,  IDictionary<Type, object> instances, DIScope scope, HashSet<Type> resolving)
    {
        if (!instances.ContainsKey(descriptor.ServiceType))
        {
            var instance = CreateInstance(descriptor.ImplementationType, scope, resolving);
            instances.Add(descriptor.ServiceType, instance);
            return instance;
        }
         return instances[descriptor.ServiceType];
    }
    
    private object CreateInstance(Type type, DIScope scope, HashSet<Type> resolving)
    {
        if (!resolving.Add(type))
             throw new InvalidOperationException($"Service of type {type.Name} has a cyclic dependency.");
        var ctor = type.GetConstructors()
            .OrderByDescending(c => c.GetParameters().Length)
            .First();

        var parameters = ctor.GetParameters()
            .Select(p => GetService(p.ParameterType, scope, resolving))
            .ToArray();

        return Activator.CreateInstance(type, parameters)!;
    }
    
}