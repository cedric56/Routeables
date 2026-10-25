using System.Collections.Concurrent;
using Routeables.Services;

namespace Routeables.Tests.Services;

public class DispatcherTests
{
    // A thread of the interface: one thread that runs what is posted to it, as the dispatcher of a host does.
    private sealed class UiThread : RouteableDispatcherBase, IDisposable
    {
        private readonly BlockingCollection<Action> _queue = [];
        private readonly Thread _thread;

        public UiThread()
        {
            _thread = new Thread(() => { foreach (var work in _queue.GetConsumingEnumerable()) work(); }) { IsBackground = true };
            _thread.Start();
        }

        public int Id => _thread.ManagedThreadId;

        public override bool CheckAccess() => Environment.CurrentManagedThreadId == Id;

        public override void Post(Action action) => _queue.Add(action);

        public void Dispose() => _queue.CompleteAdding();
    }

    [Fact]
    public async Task Work_asked_from_another_thread_runs_on_the_thread_of_the_interface()
    {
        using var ui = new UiThread();

        Assert.False(ui.CheckAccess());
        Assert.Equal(ui.Id, await ui.InvokeAsync(() => Environment.CurrentManagedThreadId));

        var ran = 0;
        await ui.InvokeAsync(() => { ran = Environment.CurrentManagedThreadId; });
        Assert.Equal(ui.Id, ran);
    }

    [Fact]
    public async Task Asynchronous_work_is_awaited()
    {
        using var ui = new UiThread();
        var done = false;

        await ui.InvokeAsync(async () => { await Task.Delay(20, TestContext.Current.CancellationToken); done = true; });

        Assert.True(done);
    }

    [Fact]
    public async Task An_exception_of_the_work_reaches_the_caller_not_the_thread_of_the_interface()
    {
        using var ui = new UiThread();

        var sync = await Assert.ThrowsAsync<InvalidOperationException>(() => ui.InvokeAsync((Action)(() => throw new InvalidOperationException("boom"))));
        var later = await Assert.ThrowsAsync<InvalidOperationException>(() => ui.InvokeAsync(async () => { await Task.Yield(); throw new InvalidOperationException("late"); }));

        Assert.Equal("boom", sync.Message);
        Assert.Equal("late", later.Message);
        // The thread of the interface is still alive.
        Assert.Equal(ui.Id, await ui.InvokeAsync(() => Environment.CurrentManagedThreadId));
    }

    [Fact]
    public async Task On_the_thread_of_the_interface_the_work_runs_at_once()
    {
        using var ui = new UiThread();

        var completedAtOnce = await ui.InvokeAsync(() =>
        {
            var task = ui.InvokeAsync(() => 42);
            return task.IsCompleted && task.Result == 42;
        });

        Assert.True(completedAtOnce);
    }

    [Fact]
    public async Task A_missing_work_is_refused()
    {
        using var ui = new UiThread();

        await Assert.ThrowsAsync<ArgumentNullException>(() => ui.InvokeAsync((Action)null!));
        await Assert.ThrowsAsync<ArgumentNullException>(() => ui.InvokeAsync((Func<Task>)null!));
        await Assert.ThrowsAsync<ArgumentNullException>(() => ui.InvokeAsync((Func<int>)null!));
    }
}
