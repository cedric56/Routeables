using NSubstitute;
using Routeables.Abstraction.Errors;
using Routeables.Abstraction.History;
using Routeables.Abstraction.Registrations;
using Routeables.Abstraction.Routing;
using Routeables.Routing;
using Routeables.Services;
using Routeables.Tests.Pages;

namespace Routeables.Tests.Services;

/// <summary>The keeper that is disposed while it starts or restores, a flush that the interface thread does not answer, and a flush with nothing to write.</summary>
public class RouteableHistoryKeeperShutdownTests
{
    private sealed class MemoryStore(string? kept = null) : IRouteableHistoryStore
    {
        public string? Kept { get; } = kept;
        public int Saves;
        public Func<string?>? OnLoad { get; set; }

        public string? Load() => OnLoad is { } load ? load() : Kept;

        public void Save(string text) => Interlocked.Increment(ref Saves);
    }

    // Not on the UI thread, and it never answers.
    private sealed class SilentDispatcher : IRouteableDispatcher
    {
        public bool CheckAccess() => false;
        public void Post(Action action) { }
        public Task InvokeAsync(Action action) => new TaskCompletionSource().Task;
        public Task<T> InvokeAsync<T>(Func<T> function) => new TaskCompletionSource<T>().Task;
        public Task InvokeAsync(Func<Task> function) => new TaskCompletionSource().Task;
    }

    private sealed class Sink : IRouteableErrorSink
    {
        public List<Exception> Errors { get; } = [];
        public void Report(string source, Exception exception) { lock (Errors) Errors.Add(exception); }
    }

    private static ServiceCollection Services(IRouteableHistoryStore store)
    {
        var services = new ServiceCollection();
        services.RegisterRouting();
        services.RegisterRoute<FakeViewFor, FakeViewModel>(ServiceLifetime.Transient, ServiceLifetime.Transient, "/kept-first");
        services.RegisterRoute<SecondViewFor, SecondViewModel>(ServiceLifetime.Transient, ServiceLifetime.Transient, "/kept-second");
        services.AddTransient<FakeViewModel>();
        services.AddTransient<SecondViewModel>();
        services.AddSingleton(store);
        return services;
    }

    private static string Kept() => RouteableState.Serialize(["/kept-first", "/kept-second"]);

    [Fact]
    public async Task A_keeper_disposed_while_it_reads_the_store_subscribes_to_nothing_and_restores_nothing()
    {
        var store = new MemoryStore(Kept());
        using var provider = Services(store).BuildServiceProvider();
        var keeper = new RouteableHistoryKeeper(provider, store);
        store.OnLoad = () => { keeper.Dispose(); return Kept(); };

        keeper.Start();

        var router = provider.GetRequiredService<IRouter>();
        await router.GoToAsync("/kept-first", cancellationToken: TestContext.Current.CancellationToken);
        await Task.Delay(200, TestContext.Current.CancellationToken);
        Assert.Equal(1, router.History.Count);
        Assert.Equal(0, Volatile.Read(ref store.Saves));
    }

    [Fact]
    public async Task A_keeper_disposed_while_it_waits_for_the_first_page_gives_up_and_writes_nothing()
    {
        var previous = RouteableHistoryKeeper.FirstPageSlice;
        RouteableHistoryKeeper.FirstPageSlice = TimeSpan.FromMilliseconds(100);
        try
        {
            var store = new MemoryStore(Kept());
            using var provider = Services(store).BuildServiceProvider();
            var keeper = new RouteableHistoryKeeper(provider, store);
            keeper.Start();
            var router = provider.GetRequiredService<IRouter>();
            // Longer than one wait: the restore went round the loop once and still has no page.
            await Task.Delay(350, TestContext.Current.CancellationToken);

            keeper.Dispose();

            await RouteableStartup.WhenRestoredAsync(router).WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            Assert.Equal(0, router.History.Count);
            Assert.Equal(0, Volatile.Read(ref store.Saves));
        }
        finally
        {
            RouteableHistoryKeeper.FirstPageSlice = previous;
        }
    }

    [Fact]
    public async Task A_keeper_disposed_just_as_the_first_page_is_shown_puts_nothing_back()
    {
        var store = new MemoryStore(Kept());
        var history = Substitute.For<IRouterHistory>();
        var router = Substitute.For<IRouter>();
        router.History.Returns(history);
        var services = new ServiceCollection();
        services.AddSingleton(router);
        using var provider = services.BuildServiceProvider();
        var keeper = new RouteableHistoryKeeper(provider, store);
        // The page appears (Count is read to see it) and the keeper is disposed before it looks again.
        history.Count.Returns(_ => { keeper.Dispose(); return 1; });

        keeper.Start();

        await RouteableStartup.WhenRestoredAsync(router).WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        Assert.Equal(0, Volatile.Read(ref store.Saves));
        _ = router.DidNotReceiveWithAnyArgs().GoToAsync(default!, cancellationToken: TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task A_flush_that_the_interface_thread_does_not_answer_in_time_is_reported()
    {
        var previous = RouteableHistoryKeeper.FlushWait;
        RouteableHistoryKeeper.FlushWait = TimeSpan.FromMilliseconds(50);
        try
        {
            var store = new MemoryStore();
            var sink = new Sink();
            var services = Services(store);
            services.AddSingleton<IRouteableDispatcher>(new SilentDispatcher());
            services.AddSingleton<IRouteableErrorSink>(sink);
            using var provider = services.BuildServiceProvider();
            var keeper = new RouteableHistoryKeeper(provider, store);
            keeper.Start();
            await provider.GetRequiredService<IRouter>().GoToAsync("/kept-first", cancellationToken: TestContext.Current.CancellationToken);

            keeper.Flush();

            lock (sink.Errors)
                Assert.Contains(sink.Errors, error => error is TimeoutException);
        }
        finally
        {
            RouteableHistoryKeeper.FlushWait = previous;
        }
    }

    [Fact]
    public void A_flush_with_no_page_in_the_history_writes_nothing()
    {
        var store = new MemoryStore();
        using var provider = Services(store).BuildServiceProvider();
        var keeper = new RouteableHistoryKeeper(provider, store);
        keeper.Start();

        keeper.Flush();

        Assert.Equal(0, Volatile.Read(ref store.Saves));
    }
}
