using Routeables.Abstraction;
using Routeables.Routing;
using Routeables.Services;

namespace Routeables.Tests.Services;

public class RouteableHistoryKeeperTests
{
    public sealed class KeptFirstView : IRouteableFor { public object? Context { get; set; } }
    public sealed class KeptFirstVm;
    public sealed class KeptSecondView : IRouteableFor { public object? Context { get; set; } }
    public sealed class KeptSecondVm;

    private sealed class MemoryStore(string? kept = null) : IRouteableHistoryStore
    {
        public string? Kept { get; set; } = kept;
        public int Saves { get; private set; }
        public bool Fails { get; set; }

        public string? Load() => Kept;

        public void Save(string text)
        {
            if (Fails) throw new IOException("read-only");
            Kept = text;
            Saves++;
        }
    }

    private static ServiceProvider Build(IRouteableHistoryStore store)
    {
        var services = new ServiceCollection();
        services.RegisterRouting();
        services.RegisterRoute<KeptFirstView, KeptFirstVm>(ServiceLifetime.Transient, ServiceLifetime.Transient, "/kept-first");
        services.RegisterRoute<KeptSecondView, KeptSecondVm>(ServiceLifetime.Transient, ServiceLifetime.Transient, "/kept-second");
        services.AddTransient<KeptFirstVm>();
        services.AddTransient<KeptSecondVm>();
        services.AddSingleton(store);
        services.AddRouteableHistory();
        return services.BuildServiceProvider();
    }

    private static async Task UntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!condition())
        {
            if (DateTime.UtcNow > deadline) throw new TimeoutException("The condition was not met in 5 seconds.");
            await Task.Delay(10, TestContext.Current.CancellationToken);
        }
    }

    [Fact]
    public async Task Each_route_change_is_kept_in_the_store()
    {
        var store = new MemoryStore();
        using var provider = Build(store);
        provider.StartRouteableHistory();
        var router = provider.GetRequiredService<IRouter>();
        var ct = TestContext.Current.CancellationToken;

        await router.GoToAsync("/kept-first", cancellationToken: ct);
        await router.GoToAsync("/kept-second", cancellationToken: ct);

        Assert.Equal(["/kept-first", "/kept-second"], RouteableState.Deserialize(store.Kept));
    }

    [Fact]
    public async Task The_history_kept_by_the_previous_run_is_put_back_once_the_first_page_is_shown()
    {
        var store = new MemoryStore(RouteableState.Serialize(["/kept-first", "/kept-second"]));
        using var provider = Build(store);
        provider.StartRouteableHistory();
        var router = provider.GetRequiredService<IRouter>();

        await router.GoToAsync("/kept-first", cancellationToken: TestContext.Current.CancellationToken);

        await UntilAsync(() => router.CurrentViewModel is KeptSecondVm);
        Assert.Equal(2, router.History.Count);
    }

    [Fact]
    public async Task The_first_page_of_the_new_run_does_not_replace_the_history_that_is_waiting()
    {
        var waiting = RouteableState.Serialize(["/kept-first", "/kept-second"]);
        var store = new MemoryStore(waiting);
        using var provider = Build(store);
        provider.StartRouteableHistory();
        var router = provider.GetRequiredService<IRouter>();

        await router.GoToAsync("/kept-first", cancellationToken: TestContext.Current.CancellationToken);

        // Until the pages are back, nothing is written; once they are, the whole history is.
        await UntilAsync(() => router.CurrentViewModel is KeptSecondVm);
        await UntilAsync(() => store.Kept == waiting && store.Saves <= 1);
        Assert.Equal(["/kept-first", "/kept-second"], RouteableState.Deserialize(store.Kept));
    }

    [Fact]
    public async Task A_store_that_cannot_write_does_not_fail_a_route_change()
    {
        var store = new MemoryStore { Fails = true };
        using var provider = Build(store);
        provider.StartRouteableHistory();
        var router = provider.GetRequiredService<IRouter>();

        var result = await router.GoToAsync("/kept-first", cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.Null(store.Kept);
    }

    [Fact]
    public void Without_the_registration_starting_does_nothing()
    {
        var services = new ServiceCollection();
        services.RegisterRouting();
        using var provider = services.BuildServiceProvider();

        Assert.Same(provider, provider.StartRouteableHistory());
        Assert.Null(provider.GetService<RouteableHistoryKeeper>());
    }

    [Fact]
    public async Task Starting_twice_keeps_one_subscription()
    {
        var store = new MemoryStore();
        using var provider = Build(store);
        provider.StartRouteableHistory();
        provider.StartRouteableHistory();

        await provider.GetRequiredService<IRouter>().GoToAsync("/kept-first", cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(1, store.Saves);
    }

    [Fact]
    public void The_file_store_writes_reads_and_replaces_its_file()
    {
        var folder = Path.Combine(Path.GetTempPath(), "routeables-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new FileRouteableHistoryStore(Path.Combine(folder, "sub", "history.txt"));

            Assert.Null(store.Load());
            store.Save("one");
            store.Save("two");

            Assert.Equal("two", store.Load());
            Assert.False(File.Exists(store.Path + ".tmp"));
        }
        finally
        {
            if (Directory.Exists(folder)) Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void The_default_file_is_named_after_the_application()
        => Assert.True(new FileRouteableHistoryStore().Path.EndsWith("routeables-history.txt", StringComparison.Ordinal));
}
