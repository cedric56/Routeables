#if !ANDROID && !IOS && !MACCATALYST && !WINDOWS
using Routeables.Native;
using Routeables.Services;

namespace Routeables.Tests.Native;

/// <summary>The bookkeeping of the system notifications, on any operating system: the platform is replaced (<c>PresentOverride</c>, <c>CancelOverride</c>).</summary>
public class SystemNotificationBookkeepingTests
{
    private static SystemNotificationCenter Center(out List<uint> cancelled)
    {
        var center = new SystemNotificationCenter();
        var list = new List<uint>();
        center.PresentOverride = _ => { };
        center.CancelOverride = list.Add;
        cancelled = list;
        return center;
    }

    private sealed class RecordingContext : SynchronizationContext
    {
        public int Posts;
        public override void Post(SendOrPostCallback d, object? state) { Interlocked.Increment(ref Posts); d(state); }
    }

    [Fact]
    public void The_identifiers_of_a_run_start_from_a_base_of_their_own_and_stay_in_the_range_of_Android()
    {
        var first = new SystemNotificationCenter();
        first.SetIdBaseForTests(3u << 16);
        var second = new SystemNotificationCenter();
        second.SetIdBaseForTests(9u << 16);

        var a = first.NextId();
        var b = second.NextId();

        Assert.Equal((3u << 16) + 1, a);
        Assert.NotEqual(a, b);
        Assert.True(new SystemNotificationCenter().NextId() is > 0 and < int.MaxValue);
    }

    [Fact]
    public void Two_notifications_of_a_run_never_share_an_identifier()
    {
        var center = new SystemNotificationCenter();
        var ids = Enumerable.Range(0, 1000).Select(_ => center.NextId()).ToHashSet();

        Assert.Equal(1000, ids.Count);
    }

    [Fact]
    public async Task A_notification_that_is_updated_expires_from_its_last_display()
    {
        var ct = TestContext.Current.CancellationToken;
        var center = Center(out _);
        var completed = new TaskCompletionSource<DateTime>(TaskCreationOptions.RunContinuationsAsynchronously);
        center.NotificationCompleted += (_, _) => completed.TrySetResult(DateTime.UtcNow);
        var notification = new SystemNotification(center, 7, null) { Title = "t", Expiration = TimeSpan.FromMilliseconds(600) };

        var start = DateTime.UtcNow;
        await notification.ShowAsync();
        await Task.Delay(400, ct);
        var updating = DateTime.UtcNow;
        await notification.ShowAsync();   // an update: the 600 ms start again
        var updated = DateTime.UtcNow;

        // A machine that stalls for 160 ms or more between the two displays lets the first 600 ms run out before the update: there is
        // nothing to learn from that run (it failed once, on a runner that was running the tests of two platforms at once).
        if (updating - start >= TimeSpan.FromMilliseconds(560))
            return;

        var end = await completed.Task.WaitAsync(TimeSpan.FromSeconds(10), ct);

        // Closed 600 ms after the update, not at the 600 ms of the first display (which would be at most 200 ms after the update).
        Assert.True(end - updated >= TimeSpan.FromMilliseconds(450), $"closed {(end - updated).TotalMilliseconds} ms after the update");
    }

    [Fact]
    public async Task A_notification_that_is_closed_does_not_expire_again()
    {
        var ct = TestContext.Current.CancellationToken;
        var center = Center(out var cancelled);
        var completions = 0;
        center.NotificationCompleted += (_, _) => Interlocked.Increment(ref completions);
        var notification = new SystemNotification(center, 8, null) { Expiration = TimeSpan.FromMilliseconds(300) };

        await notification.ShowAsync();
        await notification.CloseAsync();
        await Task.Delay(700, ct);

        Assert.Equal(1, completions);
        Assert.Single(cancelled);
    }

    [Fact]
    public async Task The_completion_is_raised_on_the_context_of_the_interface_when_the_platform_calls_from_elsewhere()
    {
        var ct = TestContext.Current.CancellationToken;
        var center = Center(out _);
        var context = new RecordingContext();
        var previous = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(context);
        try { center.Configure(new AppNotificationOptions { ClearOnAppClose = false }); }
        finally { SynchronizationContext.SetSynchronizationContext(previous); }

        var notification = new SystemNotification(center, 9, null);
        await notification.ShowAsync();
        var raised = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        center.NotificationCompleted += (_, _) => raised.TrySetResult();

        // Called from a thread of the pool, as a platform does.
        await Task.Run(() => center.Complete(9, activated: true, action: null), ct);

        await raised.Task.WaitAsync(TimeSpan.FromSeconds(10), ct);
        Assert.Equal(1, context.Posts);
    }

    [Fact]
    public async Task Without_a_context_a_completion_from_the_reader_does_not_run_the_handler_on_that_thread()
    {
        var ct = TestContext.Current.CancellationToken;
        var center = Center(out _);
        var notification = new SystemNotification(center, 10, null);
        await notification.ShowAsync();
        var handlerThread = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var handled = new ManualResetEventSlim();
        center.NotificationCompleted += (_, _) => { handlerThread.TrySetResult(Environment.CurrentManagedThreadId); handled.Set(); };

        // A thread of its own plays the reader: it stays busy until the handler has run, so no one can say the handler found it free afterwards.
        var readerId = 0;
        var reader = new Thread(() =>
        {
            readerId = Environment.CurrentManagedThreadId;
            center.Complete(10, activated: false, action: null, offThread: true);
            handled.Wait(TimeSpan.FromSeconds(10));
        });
        reader.Start();

        var ranOn = await handlerThread.Task.WaitAsync(TimeSpan.FromSeconds(10), ct);
        reader.Join(TimeSpan.FromSeconds(10));
        Assert.NotEqual(readerId, ranOn);
    }

    [Fact]
    public async Task A_completion_for_a_notification_of_an_earlier_run_is_ignored()
    {
        var center = Center(out _);
        center.SetIdBaseForTests(5u << 16);
        var notification = new SystemNotification(center, center.NextId(), null);
        await notification.ShowAsync();
        var raised = false;
        center.NotificationCompleted += (_, _) => raised = true;

        center.Complete((2u << 16) + 1, activated: true, action: null);   // the number 1 of another run

        Assert.False(raised);
        Assert.Single(center.ActiveNotifications());
    }
}
#endif
