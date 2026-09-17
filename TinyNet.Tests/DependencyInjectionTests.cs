using TinyNet.DI;

namespace TinyNet.Tests;

public class DependencyInjectionTests
{
    public class SingletonService;

    public class ScopedService;

    public class TransientService;

    public class SingletonHoldingScoped
    {
        public SingletonHoldingScoped(ScopedService dependency) => Dependency = dependency;

        public ScopedService Dependency { get; }
    }

    [Fact]
    public void Lifetimes_MatchRegistration()
    {
        var container = new DIContainer();
        container.AddSingleton<SingletonService>();
        container.AddScoped<ScopedService>();
        container.AddTransient<TransientService>();

        using var first = new DIScope();
        using var second = new DIScope();

        Assert.Same(
            container.GetService(typeof(SingletonService), first),
            container.GetService(typeof(SingletonService), second));

        Assert.Same(
            container.GetService(typeof(ScopedService), first),
            container.GetService(typeof(ScopedService), first));

        Assert.NotSame(
            container.GetService(typeof(ScopedService), first),
            container.GetService(typeof(ScopedService), second));

        Assert.NotSame(
            container.GetService(typeof(TransientService), first),
            container.GetService(typeof(TransientService), first));
    }

    [Fact]
    public void Singleton_DependingOnScoped_CapturesInstanceOfFirstScope()
    {
        var container = new DIContainer();
        container.AddSingleton<SingletonHoldingScoped>();
        container.AddScoped<ScopedService>();

        using var first = new DIScope();
        using var second = new DIScope();

        var holder = (SingletonHoldingScoped)container.GetService(typeof(SingletonHoldingScoped), first);
        var scopedInFirst = container.GetService(typeof(ScopedService), first);
        var scopedInSecond = container.GetService(typeof(ScopedService), second);

        Assert.Same(scopedInFirst, holder.Dependency);
        Assert.NotSame(scopedInSecond, holder.Dependency);
    }
}