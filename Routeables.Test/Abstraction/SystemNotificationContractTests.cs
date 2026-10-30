using Routeables.Abstraction;
using Routeables.Services;

namespace Routeables.Tests.Abstraction;

public class SystemNotificationContractTests
{
    [Fact]
    public void SystemNotificationAction_preserves_caption_tag_and_optional_icon()
    {
        var action = new RouteableSystemNotificationAction("Reply", "reply") { Icon = "reply-icon" };

        Assert.Equal("Reply", action.caption);
        Assert.Equal("reply", action.tag);
        Assert.Equal("reply-icon", action.Icon);
    }

    [Fact]
    public void SystemNotificationEventArgs_preserves_notification_interaction_details()
    {
        var userData = new { Text = "response" };
        var args = new RouteableSystemNotificationEventArgs
        {
            ActionTag = "reply",
            NotificationId = 42,
            UserData = userData,
            IsActivated = true,
        };

        Assert.Equal("reply", args.ActionTag);
        Assert.Equal((uint)42, args.NotificationId);
        Assert.Same(userData, args.UserData);
        Assert.True(args.IsActivated);
        Assert.False(args.IsCancelled);
    }

    private sealed class ToastsOnly : IRouteableNotificationService
    {
        public void Show(string title, string message, RouteableNotificationType type = RouteableNotificationType.Information,
            TimeSpan? expiration = null, Action? onClick = null, Action? onClose = null)
        {
        }
    }

    [Fact]
    public async Task A_notification_service_without_system_notifications_has_harmless_defaults()
    {
        IRouteableNotificationService service = new ToastsOnly();
        var raised = false;
        service.NotificationCompleted += (_, _) => raised = true;

        await service.InitializeAsync();
        service.CloseAll();

        Assert.Empty(service.ActiveNotifications());
        Assert.Null(await service.CreateNotificationAsync(null));
        Assert.False(raised);
    }
}
