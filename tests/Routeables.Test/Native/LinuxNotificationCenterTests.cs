#if !ANDROID && !IOS && !MACCATALYST && !WINDOWS
using Routeables.Native;
using Routeables.Native.Desktop;
using Routeables.Services;

namespace Routeables.Tests.Native;

/// <summary>
/// The notifications of Linux from one end to the other: the real center and the real D-Bus client, against a notification server that the test plays
/// (<see cref="FakeSessionBus"/>). Only on Linux: the bus is reached over a Unix socket.
/// </summary>
[Collection("D-Bus environment")]
public class LinuxNotificationCenterTests : IDisposable
{
    public static bool OnLinux => OperatingSystem.IsLinux();
    private const string Skipped = "Needs Linux: the notifications speak D-Bus over a Unix socket.";
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(10);

    private readonly FakeSessionBus _bus = new();
    private readonly SystemNotificationCenter _center = new();
    private readonly List<RouteableSystemNotificationEventArgs> _completed = [];
    private TaskCompletionSource<RouteableSystemNotificationEventArgs> _next = New();

    public LinuxNotificationCenterTests()
    {
        _center.Configure(new AppNotificationOptions { RequestPermissionOnInitialize = false });
        _center.NotificationCompleted += (_, e) =>
        {
            lock (_completed) _completed.Add(e);
            _next.TrySetResult(e);
        };
    }

    public void Dispose() => _bus.Dispose();

    private static TaskCompletionSource<RouteableSystemNotificationEventArgs> New() => new(TaskCreationOptions.RunContinuationsAsynchronously);

    private async Task<SystemNotification> ShowAsync(string title = "Title", string message = "Message", TimeSpan? expiration = null)
    {
        var notification = (SystemNotification?)await _center.CreateNotificationAsync(null) ?? throw new InvalidOperationException("no notification was created");
        notification.Title = title;
        notification.Message = message;
        notification.Expiration = expiration;
        await notification.ShowAsync();
        return notification;
    }

    /// <summary>Sends the signal until the notification is completed: the center knows who the server is only when the answer to GetNameOwner has been read.</summary>
    private async Task<RouteableSystemNotificationEventArgs> UntilCompletedAsync(Func<Task> send)
    {
        for (var attempt = 0; attempt < 40; attempt++)
        {
            await send();
            var done = await Task.WhenAny(_next.Task, Task.Delay(250, TestContext.Current.CancellationToken));
            if (done == _next.Task)
                return await _next.Task;
        }
        throw new TimeoutException("The notification was never completed.");
    }

    private async Task<RouteableSystemNotificationEventArgs> CompletedAsync()
        => await _next.Task.WaitAsync(Patience, TestContext.Current.CancellationToken);

    [Fact(Skip = Skipped, SkipUnless = nameof(OnLinux))]
    public async Task A_server_that_answers_who_it_is_makes_the_notifications_available()
    {
        var notification = await _center.CreateNotificationAsync("cat");

        Assert.NotNull(notification);
        await _bus.CallAsync("GetServerInformation");
        await _bus.CallAsync("AddMatch", 2);
    }

    [Fact(Skip = Skipped, SkipUnless = nameof(OnLinux))]
    public async Task A_bus_without_a_notification_server_makes_them_unavailable()
    {
        _bus.Answer = call => call.Member == "GetServerInformation" ? null : [];

        var notification = await _center.CreateNotificationAsync(null);

        Assert.Null(notification);
    }

    [Fact(Skip = Skipped, SkipUnless = nameof(OnLinux))]
    public async Task A_notification_is_sent_to_the_server_with_its_texts_and_is_active()
    {
        var notification = await ShowAsync("Hello", "Body text");

        var call = await _bus.CallAsync("Notify");
        Assert.Equal("susssasa{sv}i", call.Signature);
        var reader = call.Reader();
        Assert.Equal("Notifications", reader.String());      // the application name: the channel name by default
        Assert.Equal(0u, reader.UInt32());                    // replaces nothing
        Assert.Equal("", reader.String());                    // no icon
        Assert.Equal("Hello", reader.String());
        Assert.Equal("Body text", reader.String());
        Assert.Contains(notification.Id, _center.ActiveNotifications().Keys);
    }

    [Fact(Skip = Skipped, SkipUnless = nameof(OnLinux))]
    public async Task A_notification_that_is_shown_again_replaces_the_bubble_of_the_server()
    {
        var notification = await ShowAsync();
        await notification.ShowAsync();

        var second = await _bus.CallAsync("Notify", 2);
        var reader = second.Reader();
        reader.String();
        Assert.Equal(41u, reader.UInt32());                   // the identifier the server gave the first time
    }

    [Fact(Skip = Skipped, SkipUnless = nameof(OnLinux))]
    public async Task A_notification_the_server_refuses_fails_and_is_not_kept()
    {
        _bus.Answer = call => call.Member == "Notify" ? null : FakeSessionBus.Strings(":1.5");
        var notification = (SystemNotification)(await _center.CreateNotificationAsync(null))!;
        notification.Title = "t";

        var error = await Assert.ThrowsAsync<InvalidOperationException>(notification.ShowAsync);

        Assert.Contains("refused", error.Message);
        Assert.Empty(_center.ActiveNotifications());
    }

    [Fact(Skip = Skipped, SkipUnless = nameof(OnLinux))]
    public async Task A_tap_on_the_notification_is_a_tap_without_an_action()
    {
        var notification = await ShowAsync();

        var done = await UntilCompletedAsync(() => _bus.ActionInvokedAsync(41, "default"));

        Assert.Equal(notification.Id, done.NotificationId);
        Assert.True(done.IsActivated);
        Assert.False(done.IsCancelled);
        Assert.Null(done.ActionTag);
        Assert.Empty(_center.ActiveNotifications());
    }

    [Fact(Skip = Skipped, SkipUnless = nameof(OnLinux))]
    public async Task Another_action_is_given_with_its_name()
    {
        await ShowAsync();

        var done = await UntilCompletedAsync(() => _bus.ActionInvokedAsync(41, "reply"));

        Assert.True(done.IsActivated);
        Assert.Equal("reply", done.ActionTag);
    }

    [Fact(Skip = Skipped, SkipUnless = nameof(OnLinux))]
    public async Task The_user_closing_the_notification_is_a_dismissal()
    {
        await ShowAsync();

        var done = await UntilCompletedAsync(() => _bus.NotificationClosedAsync(41));

        Assert.True(done.IsCancelled);
        Assert.False(done.IsActivated);
        Assert.Empty(_center.ActiveNotifications());
    }

    [Fact(Skip = Skipped, SkipUnless = nameof(OnLinux))]
    public async Task What_does_not_come_from_the_server_or_has_the_wrong_shape_is_ignored()
    {
        await ShowAsync();
        await _bus.CallAsync("GetNameOwner");
        await Task.Delay(300, TestContext.Current.CancellationToken);       // the owner is known by now

        await _bus.ActionInvokedAsync(41, "default", sender: ":1.66");        // another process signing as the server
        await _bus.ActionInvokedAsync(999, "default");                        // a notification that is not ours
        await _bus.SignalAsync(FakeSessionBus.Server, FakeSessionBus.Interface, "ActionInvoked", "u", FakeSessionBus.UInt(41));   // not the shape of the specification
        await _bus.SignalAsync(FakeSessionBus.Server, FakeSessionBus.Interface, "SomethingElse", "us", [0, 0, 0, 0, 0, 0, 0, 0]);
        await _bus.SignalAsync(FakeSessionBus.Server, "org.example.Other", "ActionInvoked", "us", [0, 0, 0, 0, 0, 0, 0, 0]);
        await _bus.NotificationClosedAsync(41);                               // the one that counts, and it is the first to be seen

        var first = await CompletedAsync();

        Assert.True(first.IsCancelled);
        Assert.False(first.IsActivated);
    }

    [Fact(Skip = Skipped, SkipUnless = nameof(OnLinux))]
    public async Task Closing_the_notification_tells_the_server_and_completes_it_as_dismissed()
    {
        var notification = await ShowAsync();

        await notification.CloseAsync();

        var close = await _bus.CallAsync("CloseNotification");
        Assert.Equal(41u, close.Reader().UInt32());
        var done = await CompletedAsync();
        Assert.True(done.IsCancelled);
    }

    [Fact(Skip = Skipped, SkipUnless = nameof(OnLinux))]
    public async Task A_notification_that_expires_is_withdrawn_from_the_server()
    {
        await ShowAsync(expiration: TimeSpan.FromMilliseconds(150));

        var close = await _bus.CallAsync("CloseNotification");

        Assert.Equal(41u, close.Reader().UInt32());
        Assert.True((await CompletedAsync()).IsCancelled);
    }

    [Fact(Skip = Skipped, SkipUnless = nameof(OnLinux))]
    public async Task Closing_a_notification_that_is_not_shown_does_nothing()
    {
        var notification = (SystemNotification)(await _center.CreateNotificationAsync(null))!;

        await notification.CloseAsync();

        Assert.DoesNotContain(_bus.Calls, c => c.Member == "CloseNotification");
    }

    [Fact(Skip = Skipped, SkipUnless = nameof(OnLinux))]
    public async Task Closing_everything_closes_each_notification_on_the_server()
    {
        await ShowAsync();
        await ShowAsync();

        _center.CloseAll();

        await _bus.CallAsync("CloseNotification", 2);
        Assert.Empty(_center.ActiveNotifications());
    }

    [Fact(Skip = Skipped, SkipUnless = nameof(OnLinux))]
    public async Task A_server_that_is_replaced_ends_the_notifications_it_had_given_out()
    {
        await ShowAsync();
        await _bus.CallAsync("GetNameOwner");

        var done = await UntilCompletedAsync(() => _bus.NameOwnerChangedAsync(FakeSessionBus.Server, ":1.77"));

        Assert.True(done.IsCancelled);
        Assert.Empty(_center.ActiveNotifications());
    }

    [Fact(Skip = Skipped, SkipUnless = nameof(OnLinux))]
    public async Task A_bus_that_goes_away_ends_the_notifications_as_dismissed()
    {
        await ShowAsync();

        await _bus.HangUpAsync();

        Assert.True((await CompletedAsync()).IsCancelled);
        Assert.Empty(_center.ActiveNotifications());
    }
}
#endif
