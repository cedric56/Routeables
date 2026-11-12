#if ANDROID

using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Application = Android.App.Application;

namespace Routeables.Native;

// Android: the activity in front, the main thread and the permission to post (all here, with no MAUI Essentials; StartTracking must run before the first
// activity: Configure and NativeDialogs.Initialize do it), Notification.Builder and NotificationManager (no AndroidX, no MAUI Essentials). A tap starts the application with the id of the notification as an extra, which
// SystemNotificationCenter.HandleIntent reads (the lifecycle events registered by UseRouteables); a swipe is a broadcast to a receiver of the application.
public sealed partial class SystemNotificationCenter
{
    private const string ChannelId = "routeables.notifications";
    private const string ExtraId = "routeables.notification.id";
    private const string DismissAction = "routeables.notification.dismissed";

    private bool _receiverRegistered;

    private partial Task<bool> RequestPermissionAsync() => CanPostNotificationsAsync();

    private const string PostNotifications = "android.permission.POST_NOTIFICATIONS";
    private const int RequestCode = 0x5251;

    private static readonly Callbacks Tracker = new();
    private static int _tracking;

    /// <summary>The activity in front, or <see langword="null"/> when there is none (the application is starting or in the background) or when the host was started after it.</summary>
    internal static Activity? CurrentActivity => Tracker.Current;

    /// <summary>Starts following the activities. Idempotent.</summary>
    internal static void StartTracking()
    {
        if (Interlocked.Exchange(ref _tracking, 1) == 1)
            return;

        (Application.Context as Application ?? Application.Context?.ApplicationContext as Application)?.RegisterActivityLifecycleCallbacks(Tracker);
    }

    /// <summary>Runs <paramref name="work"/> on the main thread: at once when that is this one.</summary>
    private static Task<T> OnMainThreadAsync<T>(Func<Task<T>> work)
    {
        if (Looper.MyLooper() == Looper.MainLooper)
            return work();

        var source = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        new Handler(Looper.MainLooper!).Post(async () =>
        {
            try { source.TrySetResult(await work()); }
            catch (Exception ex) { source.TrySetException(ex); }
        });
        return source.Task;
    }

    /// <summary>
    /// Whether notifications may be posted. Before Android 13 they always may. From 13 the permission is asked for with the activity in front; the answer of
    /// the system dialog is read when the activity comes back to the front (no override of the activity is needed). Without an activity the answer is the
    /// permission as it stands.
    /// </summary>
    private static async Task<bool> CanPostNotificationsAsync()
    {
        if (!OperatingSystem.IsAndroidVersionAtLeast(33))
            return true;

        static bool Granted() => Application.Context.CheckSelfPermission(PostNotifications) == Permission.Granted;

        if (Granted())
            return true;

        return await OnMainThreadAsync(async () =>
        {
            // RequestPermissions exists from API 23; this is only reached from 33, which the analyzer cannot see through the lambda.
            if (!OperatingSystem.IsAndroidVersionAtLeast(23) || CurrentActivity is not { } activity)
                return Granted();

            var paused = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var resumed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            void OnPaused() => paused.TrySetResult();
            void OnResumed() { if (paused.Task.IsCompleted) resumed.TrySetResult(); }
            Tracker.Paused += OnPaused;
            Tracker.Resumed += OnResumed;
            try
            {
                activity.RequestPermissions([PostNotifications], RequestCode);

                // No dialog (the user refused twice: the system answers by itself) means the activity is never paused.
                if (await Task.WhenAny(paused.Task, Task.Delay(TimeSpan.FromSeconds(2))) != paused.Task)
                    return Granted();

                await Task.WhenAny(resumed.Task, Task.Delay(TimeSpan.FromMinutes(2)));
                return Granted();
            }
            finally
            {
                Tracker.Paused -= OnPaused;
                Tracker.Resumed -= OnResumed;
            }
        });
    }

    private sealed class Callbacks : Java.Lang.Object, Application.IActivityLifecycleCallbacks
    {
        public Activity? Current { get; private set; }

        public event Action? Paused;

        public event Action? Resumed;

        public void OnActivityCreated(Activity activity, Bundle? savedInstanceState) => Current = activity;

        public void OnActivityStarted(Activity activity) => Current = activity;

        public void OnActivityResumed(Activity activity)
        {
            Current = activity;
            Resumed?.Invoke();
        }

        public void OnActivityPaused(Activity activity) => Paused?.Invoke();

        public void OnActivityStopped(Activity activity)
        {
        }

        public void OnActivitySaveInstanceState(Activity activity, Bundle outState)
        {
        }

        public void OnActivityDestroyed(Activity activity)
        {
            if (ReferenceEquals(Current, activity))
                Current = null;
        }
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