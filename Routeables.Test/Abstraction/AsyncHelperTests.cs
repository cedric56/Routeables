using System.Globalization;
using Routeables.Abstraction;

namespace Routeables.Tests.Abstraction;

public class AsyncHelperTests
{
    private sealed class Sink : IRouteableErrorSink
    {
        public List<(string Source, Exception Exception)> Reported { get; } = [];
        public void Report(string source, Exception exception) => Reported.Add((source, exception));
    }

    private static (ServiceProvider Provider, Sink Sink) Build()
    {
        var sink = new Sink();
        var services = new ServiceCollection();
        services.AddSingleton<IRouteableErrorSink>(sink);
        return (services.BuildServiceProvider(), sink);
    }

    [Fact]
    public async Task A_fault_of_the_work_is_reported_and_given_to_onError()
    {
        var (provider, sink) = Build();
        using var scope = provider;
        var seen = new TaskCompletionSource<Exception>(TaskCreationOptions.RunContinuationsAsynchronously);

        AsyncHelper.FireAndForget(async () =>
        {
            await Task.Yield();
            throw new InvalidOperationException("load failed");
        }, provider, "persons", onError: ex => seen.TrySetResult(ex));

        var exception = await seen.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        Assert.Equal("load failed", exception.Message);
        var reported = Assert.Single(sink.Reported);
        Assert.Same(exception, reported.Exception);
        Assert.Contains("persons", reported.Source);
    }

    [Fact]
    public void An_exception_thrown_before_the_first_await_is_reported_too()
    {
        var (provider, sink) = Build();
        using var scope = provider;

        AsyncHelper.FireAndForget(() => throw new InvalidOperationException("at once"), provider);

        Assert.Equal("at once", Assert.Single(sink.Reported).Exception.Message);
    }

    [Fact]
    public async Task A_cancellation_is_not_an_error()
    {
        var (provider, sink) = Build();
        using var scope = provider;
        using var cts = new CancellationTokenSource();
        var work = new TaskCompletionSource();

        AsyncHelper.FireAndForget(async () => { await work.Task.WaitAsync(cts.Token); }, provider);
        await cts.CancelAsync();
        await Task.Delay(50, TestContext.Current.CancellationToken);

        Assert.Empty(sink.Reported);
    }

    [Fact]
    public async Task Forget_observes_a_task_that_is_already_faulted()
    {
        var (provider, sink) = Build();
        using var scope = provider;

        Task.FromException(new InvalidOperationException("late")).Forget(provider);
        await Task.Delay(50, TestContext.Current.CancellationToken);

        Assert.Equal("late", Assert.Single(sink.Reported).Exception.Message);
    }

    [Fact]
    public void An_onError_that_throws_is_reported_as_well()
    {
        var (provider, sink) = Build();
        using var scope = provider;

        AsyncHelper.FireAndForget(() => throw new InvalidOperationException("first"), provider, onError: _ => throw new InvalidOperationException("second"));

        Assert.Equal(["first", "second"], sink.Reported.Select(r => r.Exception.Message));
    }

    [Fact]
    public void RunSync_returns_the_result_and_throws_the_original_exception()
    {
        Assert.Equal(42, AsyncHelper.RunSync(async () => { await Task.Delay(10); return 42; }));

        var exception = Assert.Throws<InvalidOperationException>(() => AsyncHelper.RunSync(async () =>
        {
            await Task.Delay(10);
            throw new InvalidOperationException("sync failure");
        }));
        Assert.Equal("sync failure", exception.Message);
    }

    [Fact]
    public void RunSync_keeps_the_culture_of_the_caller()
    {
        var culture = CultureInfo.CurrentCulture;
        var uiCulture = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("fr-FR");
            CultureInfo.CurrentUICulture = new CultureInfo("de-DE");

            var seen = AsyncHelper.RunSync(async () =>
            {
                await Task.Yield();
                return (CultureInfo.CurrentCulture.Name, CultureInfo.CurrentUICulture.Name);
            });

            Assert.Equal(("fr-FR", "de-DE"), seen);
        }
        finally
        {
            CultureInfo.CurrentCulture = culture;
            CultureInfo.CurrentUICulture = uiCulture;
        }
    }

    [Fact]
    public void RunSync_without_a_result_waits_for_the_work()
    {
        var done = false;

        AsyncHelper.RunSync(async () => { await Task.Delay(20); done = true; });

        Assert.True(done);
    }
}
