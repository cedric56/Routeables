using Microsoft.Extensions.DependencyInjection;
using Routeables.Abstraction;
using Routeables.Routing;
using Routeables.Regions;

namespace Routeables.Tests.Abstraction;

/// <summary>A region follows another without routing it from its own gate, in order, on the thread that asked for it.</summary>
public class RegionFollowerTests
{
    private static ServiceProvider Build() => RegionTestServices.Build(services =>
    {
        services.AddRegion("content");
        services.AddRegion("header");
    });

    private static RouteableEntry<T> Entry<T>(string route, string region) => new(route, null, route, route) { Region = region };

    [Fact]
    public async Task A_region_routes_another_one_each_time_the_source_changes()
    {
        using var provider = Build();
        var seen = new List<string>();
        using var follower = new RegionFollower(provider.GetRouter("content"), async () =>
        {
            var shown = provider.GetRouter("content").History.Current!.ViewModel.GetType().Name;
            await provider.GoToAsync(Entry<RegionDogsViewModel>("/dogs", "header"));
            seen.Add(shown);
        });

        await provider.GoToAsync(Entry<RegionCatsViewModel>("/cats", "content"), TestContext.Current.CancellationToken);
        await follower.SettledAsync();

        Assert.Equal(["RegionCatsViewModel"], seen);
        Assert.Equal(1, provider.GetRouter("header").History.Count);
    }

    [Fact]
    public async Task Updates_run_one_after_the_other()
    {
        using var provider = Build();
        var running = 0; var overlapped = false; var runs = 0;
        using var follower = new RegionFollower(provider.GetRouter("content"), async () =>
        {
            if (Interlocked.Increment(ref running) > 1) overlapped = true;
            await Task.Delay(20);
            Interlocked.Decrement(ref running);
            Interlocked.Increment(ref runs);
        });

        for (var i = 0; i < 3; i++)
            await provider.GoToAsync(Entry<RegionCatsViewModel>("/cats", "content"), TestContext.Current.CancellationToken);
        await follower.SettledAsync();

        Assert.False(overlapped);
        Assert.Equal(3, runs);
    }

    [Fact]
    public async Task A_failing_update_is_reported_and_does_not_break_the_source()
    {
        using var provider = Build();
        Exception? failure = null;
        using var follower = new RegionFollower(provider.GetRouter("content"), () => throw new InvalidOperationException("bar"));
        follower.Failed += (_, ex) => failure = ex;

        var result = await provider.GoToAsync(Entry<RegionCatsViewModel>("/cats", "content"), TestContext.Current.CancellationToken);
        await follower.SettledAsync();

        Assert.True(result.Success);
        Assert.Equal("bar", failure?.Message);
    }

    [Fact]
    public async Task A_disposed_follower_stops_following()
    {
        using var provider = Build();
        var runs = 0;
        var follower = new RegionFollower(provider.GetRouter("content"), () => { runs++; return Task.CompletedTask; });
        follower.Dispose();

        await provider.GoToAsync(Entry<RegionCatsViewModel>("/cats", "content"), TestContext.Current.CancellationToken);
        await follower.SettledAsync();

        Assert.Equal(0, runs);
    }

    [Fact]
    public async Task Updates_go_back_to_the_context_that_created_the_follower()
    {
        using var provider = Build();
        var context = new SingleThreadContext();
        try
        {
            RegionFollower? follower = null;
            int? created = null; int? ran = null;
            await context.RunAsync(() => { created = Environment.CurrentManagedThreadId; follower = new RegionFollower(provider.GetRouter("content"), () => { ran = Environment.CurrentManagedThreadId; return Task.CompletedTask; }); });

            await provider.GoToAsync(Entry<RegionCatsViewModel>("/cats", "content"), TestContext.Current.CancellationToken);
            await follower!.SettledAsync();
            follower.Dispose();

            Assert.NotNull(created);
            Assert.Equal(created, ran);
        }
        finally { context.Dispose(); }
    }

    [Fact]
    public async Task Regions_are_erased_together()
    {
        using var provider = Build();
        await provider.GoToAsync(Entry<RegionCatsViewModel>("/cats", "content"), TestContext.Current.CancellationToken);
        await provider.GoToAsync(Entry<RegionDogsViewModel>("/dogs", "header"), TestContext.Current.CancellationToken);

        await provider.EraseRegionsAsync(["content", "header"], TestContext.Current.CancellationToken);

        Assert.Equal(0, provider.GetRouter("content").History.Count);
        Assert.Equal(0, provider.GetRouter("header").History.Count);
    }

    /// <summary>A thread with a synchronization context, like a UI thread.</summary>
    private sealed class SingleThreadContext : SynchronizationContext, IDisposable
    {
        private readonly System.Collections.Concurrent.BlockingCollection<(SendOrPostCallback, object?)> _queue = [];
        private readonly Thread _thread;

        public SingleThreadContext()
        {
            _thread = new Thread(() =>
            {
                SetSynchronizationContext(this);
                foreach (var (callback, state) in _queue.GetConsumingEnumerable())
                    callback(state);
            }) { IsBackground = true };
            _thread.Start();
        }

        public override void Post(SendOrPostCallback d, object? state) => _queue.Add((d, state));

        public Task RunAsync(Action action)
        {
            var done = new TaskCompletionSource();
            Post(_ => { try { action(); done.SetResult(); } catch (Exception ex) { done.SetException(ex); } }, null);
            return done.Task;
        }

        public void Dispose() => _queue.CompleteAdding();
    }
}
