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

    public interface IGreeter;

    public class Greeter : IGreeter;

    public class DisposeLog
    {
        public List<string> Entries { get; } = new();
    }

    public class Connection : IDisposable
    {
        private readonly DisposeLog _log;

        public Connection(DisposeLog log) => _log = log;

        public void Dispose() => _log.Entries.Add(nameof(Connection));
    }

    public class Pool : IDisposable
    {
        private readonly DisposeLog _log;

        public Pool(DisposeLog log) => _log = log;

        public void Dispose() => _log.Entries.Add(nameof(Pool));
    }

    public class Session : IAsyncDisposable
    {
        private readonly DisposeLog _log;

        public Session(DisposeLog log, Connection connection) => _log = log;

        public ValueTask DisposeAsync()
        {
            _log.Entries.Add(nameof(Session));
            return ValueTask.CompletedTask;
        }
    }

    [Fact]
    public async Task Lifetimes_MatchRegistration()
    {
        var container = new DIContainer();
        container.AddSingleton<SingletonService>();
        container.AddScoped<ScopedService>();
        container.AddTransient<TransientService>();
        container.Freeze();

        await using var first = container.CreateScope();
        await using var second = container.CreateScope();

        Assert.Same(first.GetService<SingletonService>(), second.GetService<SingletonService>());
        Assert.Same(first.GetService<ScopedService>(), first.GetService<ScopedService>());
        Assert.NotSame(first.GetService<ScopedService>(), second.GetService<ScopedService>());
        Assert.NotSame(first.GetService<TransientService>(), first.GetService<TransientService>());
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

    [Fact]
    public async Task InterfaceRegistration_ResolvesImplementation()
    {
        var container = new DIContainer();
        container.AddSingleton<IGreeter, Greeter>();
        container.Freeze();

        await using var scope = container.CreateScope();

        Assert.IsType<Greeter>(scope.GetService<IGreeter>());
    }

    [Fact]
    public async Task ScopeDispose_DisposesScopedServicesInReverseCreationOrder()
    {
        var log = new DisposeLog();
        var container = new DIContainer();
        container.AddInstance(log);
        container.AddScoped<Connection>();
        container.AddScoped<Session>();
        container.Freeze();

        var scope = container.CreateScope();
        scope.GetService<Session>();
        await scope.DisposeAsync();

        Assert.Equal([nameof(Session), nameof(Connection)], log.Entries);
    }

    [Fact]
    public async Task ContainerDispose_DisposesEachSingletonOnceAndInstancesLast()
    {
        var log = new DisposeLog();
        var container = new DIContainer();
        container.AddInstance(log);
        container.AddInstance(new Pool(log));
        container.AddSingleton<Connection>();
        container.Freeze();

        await using (var first = container.CreateScope())
            first.GetService<Connection>();
        await using (var second = container.CreateScope())
            second.GetService<Connection>();

        await container.DisposeAsync();
        await container.DisposeAsync();

        Assert.Equal([nameof(Connection), nameof(Pool)], log.Entries);
    }
}