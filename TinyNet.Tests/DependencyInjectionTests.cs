using TinyNet.DI;

namespace TinyNet.Tests;

public class DependencyInjectionTests
{
    public class SingletonService;

    public class ScopedService;

    public class TransientService;

    public class TransientHoldingScoped
    {
        public TransientHoldingScoped(ScopedService dependency) => Dependency = dependency;

        public ScopedService Dependency { get; }
    }

    public class SingletonHoldingTransient
    {
        public SingletonHoldingTransient(TransientHoldingScoped dependency) => Dependency = dependency;

        public TransientHoldingScoped Dependency { get; }
    }

    [Fact]
    public void Lifetimes_MatchRegistration()
    {
        var container = new DIContainer();
        container.AddSingleton<SingletonService>();
        container.AddScoped<ScopedService>();
        container.AddTransient<TransientService>();

        using var first = container.CreateScope();
        using var second = container.CreateScope();

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
    public void Validate_SingletonReachingScopedThroughTransient_Throws()
    {
        var container = new DIContainer();
        container.AddSingleton<SingletonHoldingTransient>();
        container.AddTransient<TransientHoldingScoped>();
        container.AddScoped<ScopedService>();

        var error = Assert.Throws<InvalidOperationException>(container.Validate);

        Assert.Contains("Captive", error.Message);
    }
}