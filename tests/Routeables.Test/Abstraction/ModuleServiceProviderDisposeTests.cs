using Routeables.Abstraction;

namespace Routeables.Test.Abstraction;

/// <summary>What the services of a module do with the services it built when it is disposed.</summary>
public class ModuleServiceProviderDisposeTests
{
    private static readonly List<string> Log = [];

    private sealed class SyncService(string name) : IDisposable { public void Dispose() => Log.Add(name); }
    private sealed class AsyncOnly(string name) : IAsyncDisposable
    {
        public ValueTask DisposeAsync() { Log.Add(name); return ValueTask.CompletedTask; }
    }
    private sealed class Both : IDisposable, IAsyncDisposable
    {
        public void Dispose() => Log.Add("both-sync");
        public ValueTask DisposeAsync() { Log.Add("both-async"); return ValueTask.CompletedTask; }
    }

    private static RouteableModuleServices Provider()
    {
        Log.Clear();
        var own = new ServiceCollection();
        own.AddSingleton(typeof(SyncService), _ => new SyncService("sync"));
        own.AddSingleton(typeof(AsyncOnly), _ => new AsyncOnly("async"));
        own.AddSingleton(typeof(Both), _ => new Both());
        var services = new RouteableModuleServices(own);
        var application = new ServiceCollection().BuildServiceProvider();
        foreach (var type in new[] { typeof(SyncService), typeof(AsyncOnly), typeof(Both) })
            services.TryGet(type, application, application, out _);
        return services;
    }

    [Fact]
    public void Dispose_disposes_in_reverse_order_and_prefers_Dispose_when_a_service_has_both()
    {
        Provider().Dispose();

        Assert.Equal("both-sync,async,sync", string.Join(",", Log));
    }

    [Fact]
    public async Task DisposeAsync_prefers_DisposeAsync_when_a_service_has_both()
    {
        await Provider().DisposeAsync();

        Assert.Equal("both-async,async,sync", string.Join(",", Log));
    }

    private sealed class Slow : IAsyncDisposable
    {
        public int Disposed;
        public async ValueTask DisposeAsync() { await Task.Delay(50); Interlocked.Exchange(ref Disposed, 1); }
    }

    [Fact]
    public async Task Dispose_does_not_wait_for_a_service_that_only_has_a_slow_DisposeAsync()
    {
        var slow = new Slow();
        var own = new ServiceCollection();
        own.AddSingleton(typeof(Slow), _ => slow);
        var services = new RouteableModuleServices(own);
        var application = new ServiceCollection().BuildServiceProvider();
        services.TryGet(typeof(Slow), application, application, out _);

        await Task.Run(services.Dispose, cancellationToken:TestContext.Current.CancellationToken);

        // Started, not awaited: it finishes afterwards.
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (Volatile.Read(ref slow.Disposed) == 0 && DateTime.UtcNow < deadline)
            await Task.Delay(10, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(1, Volatile.Read(ref slow.Disposed));
    }

    [Fact]
    public void Dispose_twice_does_nothing_the_second_time()
    {
        var provider = Provider();
        provider.Dispose();
        Log.Clear();
        provider.Dispose();

        Assert.Empty(Log);
    }
}
