using Avalonia.Controls;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using Routeables.Services;
using Xunit;

namespace Routeables.Test.Avalonia;

/// <summary>
/// The toast notifications of Avalonia in a real (headless) window, and the system notifications, which a headless session has no native
/// manager for.
/// </summary>
[Collection("Avalonia headless")]
public sealed class HeadlessNotificationTests(HeadlessApp app)
{
    /// <summary>Resolves the notification service from the shared headless application container.</summary>
    private IRouteableNotificationService Service() => app.Provider.GetRequiredService<IRouteableNotificationService>();

    /// <summary>Polls <paramref name="condition"/> on the UI thread until it becomes true or a 5-second timeout elapses.</summary>
    private static async Task UntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (true)
        {
            Dispatcher.UIThread.RunJobs();
            if (condition()) return;
            if (DateTime.UtcNow > deadline) throw new TimeoutException("The condition was not met in 5 seconds.");
            await Task.Delay(10);
        }
    }

    /// <summary>Creates, shows and returns a temporary window for toast tests.</summary>
    private static Window Shown()
    {
        var window = new Window { Width = 400, Height = 300, Content = new TextBlock { Text = "w" } };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    /// <summary>A toast is shown in the active window and invokes its close callback when it expires.</summary>
    [Fact]
    public Task A_toast_is_shown_in_the_active_window_and_tells_when_it_closes() => app.RunAsync(async () =>
    {
        var window = Shown();
        var closed = 0;
        try
        {
            Service().Show("Title", "Message", RouteableNotificationType.Success, TimeSpan.FromMilliseconds(150), onClose: () => closed++);

            await UntilAsync(() => closed == 1);

            Assert.Equal(1, closed);
        }
        finally
        {
            window.Close();
        }
    });

    /// <summary>Consecutive toasts share one manager and both invoke their close callbacks.</summary>
    [Fact]
    public Task Two_toasts_in_a_row_share_one_manager_and_both_close() => app.RunAsync(async () =>
    {
        var window = Shown();
        var closed = new List<string>();
        try
        {
            Service().Show("A", "a", expiration: TimeSpan.FromMilliseconds(150), onClose: () => closed.Add("a"));
            Service().Show("B", "b", expiration: TimeSpan.FromMilliseconds(150), onClose: () => closed.Add("b"));

            await UntilAsync(() => closed.Count == 2);

            Assert.Equal(["a", "b"], closed.Order());
        }
        finally
        {
            window.Close();
        }
    });

    /// <summary>A toast requested from a background thread is still shown on the UI thread and closes normally.</summary>
    [Fact]
    public Task A_toast_asked_from_another_thread_is_shown_on_the_UI_thread() => app.RunAsync(async () =>
    {
        var window = Shown();
        var closed = 0;
        try
        {
            await Task.Run(() => Service().Show("T", "M", expiration: TimeSpan.FromMilliseconds(150), onClose: () => closed++));

            await UntilAsync(() => closed == 1);

            Assert.Equal(1, closed);
        }
        finally
        {
            window.Close();
        }
    });

    /// <summary>
    /// Without a native notification manager, system-notification APIs are no-ops: no active notifications,
    /// create returns null, and initialize/close/events do not throw.
    /// </summary>
    [Fact]
    public Task Without_a_native_manager_there_is_no_system_notification() => app.RunAsync(async () =>
    {
        var service = Service();

        Assert.Empty(service.ActiveNotifications());
        Assert.Null(await service.CreateNotificationAsync("category"));
        await service.InitializeAsync();
        service.CloseAll();

        EventHandler<RouteableSystemNotificationEventArgs> handler = (_, _) => { };
        service.NotificationCompleted += handler;
        service.NotificationCompleted -= handler;
    });
}