namespace TinyNet.DI;

public class ServiceDescriptor
{
    public Type ServiceType { get; }
    public Type ImplementationType { get; }
    public ServiceLifetime Lifetime { get; }
    
    public ServiceDescriptor(Type serviceType, Type implementationType, ServiceLifetime lifetime)
    {
        ServiceType = serviceType;
        ImplementationType = implementationType;
        Lifetime = lifetime;
    }
    
}

public enum ServiceLifetime
{
    Transient,
    Scoped,
    Singleton
}