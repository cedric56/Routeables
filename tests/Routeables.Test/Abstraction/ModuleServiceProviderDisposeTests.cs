using Routeables.Abstraction;

namespace Routeables.Test.Abstraction;

/// <summary>What the services of a module do with the services it built when it is disposed.</summary>
public class ModuleServiceProviderDisposeTests
{
    private static readonly List<string> Log = [];

    private sealed class SyncService(string name) : IDisposable { public void Dispose() => Log.Add(name); }
    private sealed class AsyncOnly(string name) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync() { await Task.Yield(); Log.Add(name); }
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
