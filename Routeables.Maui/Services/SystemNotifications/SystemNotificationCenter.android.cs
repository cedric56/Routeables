#if ANDROID

using Android.App;
using Android.Content;
using Android.OS;
using AndroidX.Core.App;
using AndroidX.Core.Content;
using Application = Android.App.Application;

namespace Routeables.Maui.Services;

// Android: NotificationCompat and NotificationManagerCompat. A tap starts the application with the id of the notification as an extra, which
// SystemNotificationCenter.HandleIntent reads (the lifecycle events registered by UseRouteables); a swipe is a broadcast to a receiver of the application.
internal sealed partial class SystemNotificationCenter
{
    private const string ChannelId = "routeables.notifications";
    private const string ExtraId = "routeables.notification.id";
    private const string DismissAction = "routeables.notification.dismissed";

    private bool _receiverRegistered;

    private partial async Task<bool> RequestPermissionAsync()
    {
        // Before Android 13 a notification needs no permission.
        if (!OperatingSystem.IsAndroidVersionAtLeast(33))
            return true;

        return await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            var status = await Permissions.CheckStatusAsync<Permissions.PostNotifications>();
            if (status != PermissionStatus.Granted)
                status = await Permissions.RequestAsync<Permissions.PostNotifications>();
            return status == PermissionStatus.Granted;
        });
    }

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

        var builder = new NotificationCompat.Builder(context, ChannelId)
            .SetContentTitle(notification.Title)!
            .SetContentText(notification.Message)!
            .SetSmallIcon(context.ApplicationInfo?.Icon ?? 0)!
            .SetContentIntent(tap)!
            .SetDeleteIntent(swipe)!
            .SetAutoCancel(true)!;

        NotificationManagerCompat.From(context)?.Notify(id, builder.Build());
    }

    private partial void Cancel(uint id) => NotificationManagerCompat.From(Application.Context)?.Cancel((int)id);

    private partial void CancelAll() => NotificationManagerCompat.From(Application.Context)?.CancelAll();

    /// <summary>Called with the intent that started or came back to an activity: a tap on a notification carries its id.</summary>
    internal static void HandleIntent(Intent? intent)
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
        ContextCompat.RegisterReceiver(context, new DismissReceiver(), new IntentFilter(DismissAction), ContextCompat.ReceiverNotExported);
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
