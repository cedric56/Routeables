#if IOS || MACCATALYST

using Foundation;
using UserNotifications;

namespace Routeables.Maui.Services;

// iOS and Mac Catalyst: UserNotifications. The delegate shows a notification while the application is in front, and tells a tap or a dismissal.
internal sealed partial class SystemNotificationCenter
{
    private const string CategoryId = "routeables.notifications";
    private const string IdKey = "id";

    // The values of UNNotificationDefaultActionIdentifier (a tap) and UNNotificationDismissActionIdentifier (a swipe), constants of the system.
    private const string DefaultAction = "com.apple.UNNotificationDefaultActionIdentifier";
    private const string DismissAction = "com.apple.UNNotificationDismissActionIdentifier";

    private bool _delegateSet;

    private partial async Task<bool> RequestPermissionAsync()
    {
        var center = UNUserNotificationCenter.Current;
        EnsureDelegate(center);

        var (granted, _) = await center.RequestAuthorizationAsync(UNAuthorizationOptions.Alert | UNAuthorizationOptions.Sound | UNAuthorizationOptions.Badge);
        return granted;
    }

    private partial void Present(SystemNotification notification)
    {
        var content = new UNMutableNotificationContent
        {
            Title = notification.Title ?? string.Empty,
            Body = notification.Message ?? string.Empty,
            Sound = UNNotificationSound.Default,
            CategoryIdentifier = CategoryId,
            UserInfo = NSDictionary.FromObjectAndKey(new NSNumber(notification.Id), new NSString(IdKey))
        };

        var request = UNNotificationRequest.FromIdentifier(notification.Id.ToString(), content, UNTimeIntervalNotificationTrigger.CreateTrigger(0.1, false));
        UNUserNotificationCenter.Current.AddNotificationRequest(request, error =>
        {
            if (error is not null)
                System.Diagnostics.Trace.TraceWarning($"Routeables: system notification: {error.LocalizedDescription}");
        });
    }

    private partial void Cancel(uint id)
    {
        var ids = new[] { id.ToString() };
        UNUserNotificationCenter.Current.RemovePendingNotificationRequests(ids);
        UNUserNotificationCenter.Current.RemoveDeliveredNotifications(ids);
    }

    private partial void CancelAll()
    {
        UNUserNotificationCenter.Current.RemoveAllPendingNotificationRequests();
        UNUserNotificationCenter.Current.RemoveAllDeliveredNotifications();
    }

    private void EnsureDelegate(UNUserNotificationCenter center)
    {
        if (_delegateSet)
            return;

        _delegateSet = true;
        center.Delegate = new NotificationDelegate(this);

        // A category that reports the dismissal: without it the system says nothing when the user swipes a notification away.
        var category = UNNotificationCategory.FromIdentifier(CategoryId, [], [], UNNotificationCategoryOptions.CustomDismissAction);
        center.SetNotificationCategories(new NSSet<UNNotificationCategory>(category));
    }

    private sealed class NotificationDelegate(SystemNotificationCenter owner) : UNUserNotificationCenterDelegate
    {
        public override void WillPresentNotification(UNUserNotificationCenter center, UNNotification notification, Action<UNNotificationPresentationOptions> completionHandler)
            => completionHandler(UNNotificationPresentationOptions.Banner | UNNotificationPresentationOptions.List | UNNotificationPresentationOptions.Sound);

        public override void DidReceiveNotificationResponse(UNUserNotificationCenter center, UNNotificationResponse response, Action completionHandler)
        {
            if (response.Notification.Request.Content.UserInfo[IdKey] is NSNumber number)
            {
                var action = response.ActionIdentifier?.ToString();
                var activated = action == DefaultAction;
                owner.Complete(number.UInt32Value, activated, activated || action == DismissAction ? null : action);
            }

            completionHandler();
        }
    }
}
#endif
