using Routeables.Maui.Services;
using Routeables.Services;

namespace Routeables.Tests.Maui;

/// <summary>
/// The system notifications of MAUI on the plain <c>net10.0</c> target, which has no notification center: nothing is created and nothing breaks.
/// (Android, iOS, Mac Catalyst and Windows have their own implementation, which only runs on a device.)
/// </summary>
public class SystemNotificationCenterTests
{
    private sealed class ToastsOnly(RouteableSystemNotifications? system) : RouteableNotificationServiceBase(system)
    {
        public override void Show(string title, string message, RouteableNotificationType type = RouteableNotificationType.Information,
            TimeSpan? expiration = null, Action? onClick = null, Action? onClose = null)
        {
        }
    }

    [Fact]
    public async Task Without_WithAppNotifications_nothing_is_created()
    {
        var center = new SystemNotificationCenter();

        Assert.Null(center.Options);
        Assert.Null(await center.CreateNotificationAsync("animals"));
        Assert.Empty(center.ActiveNotifications());
        await center.InitializeAsync();
    }

    [Fact]
    public async Task The_plain_net10_target_has_no_notification_center_even_with_WithAppNotifications()
    {
        var center = new SystemNotificationCenter();
        center.Configure(new AppNotificationOptions());

        Assert.Null(await center.CreateNotificationAsync("animals"));
        center.CloseAll();
    }

    [Fact]
    public void An_unknown_notification_is_completed_without_raising_anything()
    {
        var center = new SystemNotificationCenter();
        var raised = false;
        center.NotificationCompleted += (_, _) => raised = true;

        center.Complete(42, activated: true, action: null);

        Assert.False(raised);
    }

    [Fact]
    public async Task A_notification_that_is_shown_is_active_until_the_platform_completes_it()
    {
        var center = new SystemNotificationCenter();
        var notification = new SystemNotification(center, 7, "animals") { Title = "t", Message = "m" };
        var completed = new List<RouteableSystemNotificationEventArgs>();
        center.NotificationCompleted += (_, e) => completed.Add(e);

        await notification.ShowAsync();
        Assert.Same(notification, center.ActiveNotifications()[7]);

        center.Complete(7, activated: true, action: "open");
        center.Complete(7, activated: true, action: "open");               // told twice by the platform: raised once

        Assert.Empty(center.ActiveNotifications());
        var only = Assert.Single(completed);
        Assert.Equal((uint)7, only.NotificationId);
        Assert.Equal("open", only.ActionTag);
        Assert.True(only.IsActivated);
        Assert.False(only.IsCancelled);
    }

    [Fact]
    public async Task Closing_a_notification_tells_it_was_cancelled()
    {
        var center = new SystemNotificationCenter();
        var notification = new SystemNotification(center, 3, null);
        await notification.ShowAsync();
        var completed = new List<RouteableSystemNotificationEventArgs>();
        center.NotificationCompleted += (_, e) => completed.Add(e);

        await notification.CloseAsync();

        var only = Assert.Single(completed);
        Assert.True(only.IsCancelled);
        Assert.Empty(center.ActiveNotifications());
    }

    [Fact]
    public async Task The_base_class_gives_a_service_that_only_writes_Show_the_harmless_defaults_of_the_platform()
    {
        var service = new ToastsOnly(new RouteableSystemNotifications());

        Assert.Null(await service.CreateNotificationAsync(null));
        Assert.Empty(service.ActiveNotifications());
        service.CloseAll();
        await service.InitializeAsync();
    }

    [Fact]
    public void WithAppNotifications_returns_the_builder_and_turns_the_system_notifications_on()
    {
        var builder = MauiApp.CreateBuilder();

        var returned = Routeables.Maui.Services.Extensions.WithAppNotifications(builder, new AppNotificationOptions { ChannelName = "Animals" });

        Assert.Same(builder, returned);
        Assert.Equal("Animals", SystemNotificationCenter.Current.Options?.ChannelName);
    }
}
