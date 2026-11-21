using Routeables.Abstraction.Errors;
using Routeables.Abstraction.Registrations;
using Routeables.Abstraction.Routing;
using Routeables.Routing;
using Routeables.Services;
using Routeables.Tests.Pages;

namespace Routeables.Tests.Services;

/// <summary>
/// The keeper in an application that has a UI thread: the history is read and put back on that thread, and a failure of the dispatcher is
/// reported instead of lost.
/// </summary>
public class RouteableHistoryKeeperDispatcherTests
{
    private sealed class MemoryStore(string? kept = null) : IRouteableHistoryStore
    {
        public string? Kept { get; set; } = kept;
        public string? Load() => Kept;
        public void Save(string text) => Kept = text;
    }

    // Not on the UI thread whoever asks: everything the keeper does with the history has to go through it.
    private sealed class OtherThreadDispatcher(bool fails = false) : IRouteableDispatcher
    {
        public int Calls;
        public bool CheckAccess() => false;
        public void Post(Action action) { Interlocked.Increment(ref Calls); action(); }
        public Task InvokeAsync(Action action)
        {
            Interlocked.Increment(ref Calls);
            if (fails) throw new InvalidOperationException("the UI thread is gone");
            action();
            return Task.CompletedTask;
        }
        public Task<T> InvokeAsync<T>(Func<T> function)
        {
            Interlocked.Increment(ref Calls);
            if (fails) throw new InvalidOperationException("the UI thread is gone");
            return Task.FromResult(function());
        }
        public Task InvokeAsync(Func<Task> function)
        {
            Interlocked.Increment(ref Calls);
            if (fails) throw new InvalidOperationException("the UI thread is gone");
            return function();
        }
    }

    private sealed class Sink : IRouteableErrorSink
    {
        public List<Exception> Errors { get; } = [];
        public void Report(string source, Exception exception) { lock (Errors) Errors.Add(exception); }
    }

    private static ServiceProvider Build(IRouteableHistoryStore store, IRouteableDispatcher dispatcher, Sink? sink = null)
    {
        var services = new ServiceCollection();
        services.RegisterRouting();
        services.RegisterRoute<FakeViewFor, FakeViewModel>(ServiceLifetime.Transient, ServiceLifetime.Transient, "/kept-first");
        services.RegisterRoute<SecondViewFor, SecondViewModel>(ServiceLifetime.Transient, ServiceLifetime.Transient, "/kept-second");
        services.AddTransient<FakeViewModel>();
        services.AddTransient<SecondViewModel>();
        services.AddSingleton(store);
        services.AddSingleton(dispatcher);
        if (sink is not null)
            services.AddSingleton<IRouteableErrorSink>(sink);
        services.AddRouteableHistory(path: null);
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
    public async Task The_history_is_put_back_and_written_again_through_the_dispatcher_when_the_caller_is_not_on_the_UI_thread()
    {
        var store = new MemoryStore(RouteableState.Serialize(["/kept-first", "/kept-second"]));
        var dispatcher = new OtherThreadDispatcher();
        using var provider = Build(store, dispatcher);
        provider.StartRouteableHistory();
        var router = provider.GetRequiredService<IRouter>();

        await router.GoToAsync("/kept-first", cancellationToken: TestContext.Current.CancellationToken);

        await UntilAsync(() => router.CurrentViewModel is SecondViewModel);
        await UntilAsync(() => Volatile.Read(ref dispatcher.Calls) >= 2); // the restore, then the write that ends it
        Assert.Equal(2, router.History.Count);
    }

    [Fact]
    public async Task A_dispatcher_that_fails_is_reported_for_the_restore_and_for_the_write()
    {
        var store = new MemoryStore(RouteableState.Serialize(["/kept-first", "/kept-second"]));
        var sink = new Sink();
        using var provider = Build(store, new OtherThreadDispatcher(fails: true), sink);
        provider.StartRouteableHistory();
        var router = provider.GetRequiredService<IRouter>();

        await router.GoToAsync("/kept-first", cancellationToken: TestContext.Current.CancellationToken);

        await UntilAsync(() => { lock (sink.Errors) return sink.Errors.Count >= 2; });
        lock (sink.Errors)
            Assert.All(sink.Errors, error => Assert.Contains("the UI thread is gone", error.Message));
    }
}
