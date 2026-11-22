using Routeables.Abstraction.Bases;
using Routeables.Abstraction.Registrations;
using Routeables.Abstraction.Routing;
using Routeables.Abstraction.States;
using Routeables.Routing;
using Routeables.Services;
using Routeables.Tests.Pages;

namespace Routeables.Tests.Services;

/// <summary>Two saves may overlap: the one that read the history last is the one that is written last.</summary>
public class RouteableHistoryKeeperOrderTests
{
    public sealed class OrderView : IRouteableFor { public object? Context { get; set; } }

    /// <summary>A view model whose state can be changed, and whose next save can be held in the middle of its read.</summary>
    public sealed class OrderVm : IRouteableState
    {
        public string Value { get; set; } = "alpha";
        public bool HoldNext { get; set; }
        public ManualResetEventSlim Reading { get; } = new(false);
        public ManualResetEventSlim Release { get; } = new(false);

        public void SaveState(IDictionary<string, string> state)
        {
            state["v"] = Value;
            if (!HoldNext)
                return;

            HoldNext = false;
            Reading.Set();
            Release.Wait(TimeSpan.FromSeconds(5));
        }

        public void LoadState(IReadOnlyDictionary<string, string> state) { }
    }

    private sealed class MemoryStore : IRouteableHistoryStore
    {
        public string? Kept { get; private set; }
        public int Saves { get; private set; }
        public string? Load() => Kept;

        public void Save(string text)
        {
            Kept = text;
            Saves++;
        }
    }

    private static ServiceProvider Build(MemoryStore store)
    {
        var services = new ServiceCollection();
        services.RegisterRouting();
        services.RegisterRoute<OrderView, OrderVm>(ServiceLifetime.Transient, ServiceLifetime.Singleton, "/order");
        services.AddSingleton<OrderVm>();
        services.AddSingleton<IRouteableHistoryStore>(store);
        services.AddRouteableHistory();
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task A_read_that_is_older_than_the_one_already_looked_at_is_not_written_when_the_text_did_not_change()
    {
        var store = new MemoryStore();
        using var provider = Build(store);
        provider.StartRouteableHistory();
        var router = provider.GetRequiredService<IRouter>();
        var keeper = provider.GetRequiredService<RouteableHistoryKeeper>();
        var ct = TestContext.Current.CancellationToken;

        await router.GoToAsync("/order", cancellationToken: ct);
        var vm = Assert.IsType<OrderVm>(router.CurrentViewModel);
        keeper.Flush();
        var kept = store.Kept;
        Assert.NotNull(kept);
        Assert.Contains("alpha", kept);

        // A first save starts reading the history while the page says "bravo", and is held in the middle of its read.
        vm.Value = "bravo";
        vm.HoldNext = true;
        var slow = Task.Run(keeper.Flush, ct);
        Assert.True(vm.Reading.Wait(TimeSpan.FromSeconds(5), ct), "the first save did not start reading");

        // Then the page says "alpha" again, and a second save reads the history: the text is the one that is already written.
        vm.Value = "alpha";
        keeper.Flush();
        Assert.Equal(kept, store.Kept);

        // The first save ends last, with a text that was true earlier and is not any more: it must not replace the newer one.
        vm.Release.Set();
        await slow;
        Assert.Equal(kept, store.Kept);
        Assert.DoesNotContain("bravo", store.Kept);
    }

    public sealed class SlowView : IRouteableFor { public object? Context { get; set; } }
    public sealed class SlowVm : IRouteableState
    {
        public static readonly ManualResetEventSlim Reading = new();
        public static readonly ManualResetEventSlim Release = new();
        public static int Calls;

        public void SaveState(IDictionary<string, string> state)
        {
            if (Interlocked.Increment(ref Calls) == 1)
            {
                Reading.Set();      // the first read of the history is slow
                Release.Wait(TimeSpan.FromSeconds(10));
            }
        }

        public void LoadState(IReadOnlyDictionary<string, string> state) { }
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_save_that_read_the_history_before_another_one_does_not_replace_it()
    {
        SlowVm.Reading.Reset();
        SlowVm.Release.Reset();
        SlowVm.Calls = 1;                                   // not the first read: the save of the first route change must not wait
        var store = new MemoryStore();
        var services = new ServiceCollection();
        services.RegisterRouting();
        services.RegisterRoute<SlowView, SlowVm>(ServiceLifetime.Transient, ServiceLifetime.Transient, "/slow");
        services.RegisterRoute<SecondViewFor, SecondViewModel>(route: "/other");
        services.AddTransient<SlowVm>();
        services.AddSingleton<IRouteableHistoryStore>(store);
        services.AddRouteableHistory();
        await using var provider = services.BuildServiceProvider();
        provider.StartRouteableHistory();
        var router = provider.GetRequiredService<IRouter>();
        await router.GoToAsync("/slow", cancellationToken: Ct);
        SlowVm.Calls = 0;                                   // the save of that route change is done

        var keeper = provider.GetRequiredService<RouteableHistoryKeeper>();
        var slow = Task.Run(keeper.Flush, Ct);              // reads the history [/slow] and waits in SaveState
        Assert.True(SlowVm.Reading.Wait(TimeSpan.FromSeconds(10), Ct));

        await router.GoToAsync("/other", cancellationToken: Ct);   // its save reads [/slow, /other] and writes it
        SlowVm.Release.Set();
        await slow;

        Assert.True(new[] { "/slow", "/other" }.SequenceEqual(RouteableState.Deserialize(store.Kept)));
    }
}
