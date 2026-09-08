using System.Collections.Concurrent;
using System.Linq.Expressions;
using TinyNet.Middlewares;

namespace TinyNet.DI;

public class DIContainer
{
    private readonly ConcurrentDictionary<Type, Lazy<object>> _singletonInstances = new();
    private readonly ConcurrentDictionary<Type, ServiceDescriptor> _descriptors = new(); 
    private readonly ConcurrentDictionary<Type, Func<object[], object>> _factories = new();    
    private readonly ConcurrentDictionary<Type, Type[]> _ctorParams = new();
    private record MiddlewareParamMeta(int Index, bool IsDelegate, Type? ServiceType);                                                                                                                                                                
    private record MiddlewareMeta(Func<object[], object> Factory, MiddlewareParamMeta[] Params);                                                                                                                                                      
    private readonly ConcurrentDictionary<Type, MiddlewareMeta> _middlewareMeta = new(); 
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
                                                                                                                                                                                                                                                    
  public void AddSingleton<TService, TImplementation>() where TImplementation : TService                                                                                                                                                            
      => AddSingleton(typeof(TService), typeof(TImplementation));                                                                                                                                                                                   
  public void AddSingleton<TImplementation>()                                                                                                                                                                                                       
      => AddSingleton(typeof(TImplementation), typeof(TImplementation));                                                                                                                                                                            
  public void AddSingleton(Type type)                                                                                                                                                                                                               
      => AddSingleton(type, type);                                                                                                                                                                                                                  
  public void AddSingleton(Type serviceType, Type implementationType)                                                                                                                                                                               
      => Register(serviceType, implementationType, ServiceLifetime.Singleton);   

    public void AddInstance<TService>(TService instance)
    {
        AddSingleton<TService>();
        _singletonInstances.TryAdd(typeof(TService), new Lazy<object>(instance!));
    }

    private void Register<TService, TImplementation>(ServiceLifetime lifetime)
    => Register(typeof(TService), typeof(TImplementation), lifetime);
    
    private void Register(Type serviceType, Type implementationType,ServiceLifetime lifetime)
    {
        if(_descriptors.ContainsKey(serviceType))
            throw new Exception($"Service type {serviceType} is already registered");
        _descriptors[serviceType] = new ServiceDescriptor(serviceType, implementationType, lifetime);
    }
    public Object GetService(Type serviceType, DIScope scope) 
        => GetService(serviceType, scope, new());

    private object GetService(Type serviceType, DIScope scope, HashSet<Type> resolving)
    {
        if (!_descriptors.TryGetValue(serviceType, out var descriptor))                                                                                                                                                                                   
            throw new InvalidOperationException($"Service {serviceType.Name} not registered");                                                                                                                                                            

            return descriptor.Lifetime switch
            {
                ServiceLifetime.Transient => CreateInstance(descriptor.ImplementationType, scope,resolving),
                ServiceLifetime.Scoped => GetScoped(descriptor, scope, resolving),
                ServiceLifetime.Singleton => GetSingleton(descriptor, scope, resolving),
                _ => throw new ArgumentOutOfRangeException()
            };
    }
    internal Middleware GetMiddleware(Type middlewareType, RequestDelegate next, DIScope scope)                                                                                                                                                       
    {                                                                                                                                                                                                                                                 
        var meta = _middlewareMeta.GetOrAdd(middlewareType, BuildMiddlewareMeta);                                                                                                                                                                     
        var parameters = new object[meta.Params.Length];                                                                                                                                                                                              
        foreach (var p in meta.Params)                                                                                                                                                                                                                
            parameters[p.Index] = p.IsDelegate ? next : GetService(p.ServiceType!, scope, new());                                                                                                                                                     
        return (Middleware)meta.Factory(parameters);                                                                                                                                                                                                  
    }                                                                                                                                                                                                                                                 
                                                                                                                                                                                                                                                      
    private static MiddlewareMeta BuildMiddlewareMeta(Type middlewareType)                                                                                                                                                                            
    {                                                                                                                                                                                                                                                 
        var ctor = middlewareType.GetConstructors()                                                                                                                                                                                                   
            .OrderByDescending(c => c.GetParameters().Length)                                                                                                                                                                                         
            .First();                                                                                                                                                                                                                                 
        var paramsInfo = ctor.GetParameters();                                                                                                                                                                                                        
        var paramMetas = paramsInfo.Select((p, i) => new MiddlewareParamMeta(                                                                                                                                                                         
            i,                                                                                                                                                                                                                                        
            p.ParameterType == typeof(RequestDelegate),                                                                                                                                                                                               
            p.ParameterType != typeof(RequestDelegate) ? p.ParameterType : null)).ToArray();                                                                                                                                                          
                                                                                                                                                                                                                                                      
        var param = Expression.Parameter(typeof(object[]), "args");                                                                                                                                                                                   
        var ctorParams = paramsInfo.Select((p, i) =>                                                                                                                                                                                                  
            Expression.Convert(Expression.ArrayIndex(param, Expression.Constant(i)), p.ParameterType));                                                                                                                                               
        var factory = Expression.Lambda<Func<object[], object>>(                                                                                                                                                                                      
            Expression.Convert(Expression.New(ctor, ctorParams), typeof(object)), param).Compile();                                                                                                                                                   
                                                                                                                                                                                                                                                      
        return new MiddlewareMeta(factory, paramMetas);                                                                                                                                                                                               
    }  

    private object GetSingleton(ServiceDescriptor descriptor, DIScope scope, HashSet<Type> resolving)
    {
        var lazy = _singletonInstances.GetOrAdd(
            descriptor.ServiceType,
            _ => new Lazy<object>(
                () => CreateInstance(descriptor.ImplementationType, scope, resolving),
                LazyThreadSafetyMode.ExecutionAndPublication));
        return lazy.Value;
    }

    private object GetScoped(ServiceDescriptor descriptor, DIScope scope, HashSet<Type> resolving)
    {
        if (!scope._scopedInstances.TryGetValue(descriptor.ServiceType, out var existing))
        {
            existing = CreateInstance(descriptor.ImplementationType, scope, resolving);
            scope._scopedInstances[descriptor.ServiceType] = existing;
        }
        return existing;
    }
    private object CreateInstance(Type type, DIScope scope, HashSet<Type> resolving)                                                                                                                                                                  
    {
        if(!resolving.Add(type)) 
            throw  new InvalidOperationException($"Type {type} has cyclic dependency");
        try
        {
            var factory = _factories.GetOrAdd(type, BuildFactory);                                                                                                                                                                                        
            var paramTypes = _ctorParams.GetOrAdd(type, t => t.GetConstructors()                                                                                                                                                                          
                .OrderByDescending(c => c.GetParameters().Length)                                                                                                                                                                                         
                .First().GetParameters().Select(p => p.ParameterType).ToArray());                                                                                                                                                                         
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
    
}