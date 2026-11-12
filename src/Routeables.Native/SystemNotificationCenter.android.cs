#if ANDROID

using Android.App;
using Android.Content;
using Application = Android.App.Application;

namespace Routeables.Native;

// Android: Notification.Builder and NotificationManager (no AndroidX, no MAUI Essentials). A tap starts the application with the id of the notification as an extra, which
// SystemNotificationCenter.HandleIntent reads (the lifecycle events registered by UseRouteables); a swipe is a broadcast to a receiver of the application.
public sealed partial class SystemNotificationCenter
{
    private const string ChannelId = "routeables.notifications";
    private const string ExtraId = "routeables.notification.id";
    private const string DismissAction = "routeables.notification.dismissed";

    private bool _receiverRegistered;

    private partial Task<bool> RequestPermissionAsync() => AndroidHost.CanPostNotificationsAsync();

    private partial void Present(SystemNotification notification)
    {
        var context = Application.Context;
        EnsureChannel(context);
        EnsureReceiver(context);

        var id = (int)notification.Id;

        // Immutable (API 23) is required from Android 12 on, and does not exist before 23.
        var flags = OperatingSystem.IsAndroidVersionAtLeast(23) ? PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable : PendingIntentFlags.UpdateCurrent;
        var launch = context.PackageManager?.GetLaunchIntentForPackage(context.PackageName ?? string.Empty)
            ?? throw new InvalidOperationException("The application has no launch activity.");
        launch.SetFlags(ActivityFlags.SingleTop | ActivityFlags.ClearTop);
        launch.PutExtra(ExtraId, id);
        var tap = PendingIntent.GetActivity(context, id, launch, flags);

        var dismissed = new Intent(DismissAction).SetPackage(context.PackageName);
        dismissed.PutExtra(ExtraId, id);
        var swipe = PendingIntent.GetBroadcast(context, id, dismissed, flags);

        // Notification.Builder(context, channel) exists from Android 8, the one with the context alone (no channel) before.
#pragma warning disable CA1422 // Validate platform compatibility: the constructor without a channel is for the versions that have none.
        var builder = OperatingSystem.IsAndroidVersionAtLeast(26) ? new Notification.Builder(context, ChannelId) : new Notification.Builder(context);
#pragma warning restore CA1422
        builder.SetContentTitle(notification.Title);
        builder.SetContentText(notification.Message);
        builder.SetSmallIcon(context.ApplicationInfo?.Icon ?? 0);
        builder.SetContentIntent(tap);
        builder.SetDeleteIntent(swipe);
        builder.SetAutoCancel(true);

        (context.GetSystemService(Context.NotificationService) as NotificationManager)?.Notify(id, builder.Build());
    }

    private partial void Cancel(uint id) => (Application.Context.GetSystemService(Context.NotificationService) as NotificationManager)?.Cancel((int)id);

    private partial void CancelAll() => (Application.Context.GetSystemService(Context.NotificationService) as NotificationManager)?.CancelAll();

    /// <summary>Called with the intent that started or came back to an activity: a tap on a notification carries its id.</summary>
    public static void HandleIntent(Intent? intent)
    {
        if (intent is not null && intent.HasExtra(ExtraId))
        {
            var id = intent.GetIntExtra(ExtraId, -1);
            if (id >= 0)
                Current.Complete((uint)id, activated: true, action: null);
        }
    }

    private void EnsureChannel(Context context)
    {
        if (!OperatingSystem.IsAndroidVersionAtLeast(26))
            return;

        var manager = (NotificationManager?)context.GetSystemService(Context.NotificationService);
        if (manager?.GetNotificationChannel(ChannelId) is null)
            manager?.CreateNotificationChannel(new NotificationChannel(ChannelId, Options?.ChannelName ?? "Notifications", NotificationImportance.Default));
    }

    private void EnsureReceiver(Context context)
    {
        if (_receiverRegistered)
            return;

        _receiverRegistered = true;
        var filter = new IntentFilter(DismissAction);
        if (OperatingSystem.IsAndroidVersionAtLeast(33))
            context.RegisterReceiver(new DismissReceiver(), filter, ReceiverFlags.NotExported);
        else
            context.RegisterReceiver(new DismissReceiver(), filter);
    }

    private sealed class DismissReceiver : BroadcastReceiver
    {
        public override void OnReceive(Context? context, Intent? intent)
        {
            var id = intent?.GetIntExtra(ExtraId, -1) ?? -1;
            if (id >= 0)
                Current.Complete((uint)id, activated: false, action: null);
        }
    }
}
#endif
