#if ANDROID

using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Application = Android.App.Application;

namespace Routeables.Native;

// What Routeables.Native needs of Android with no library of a UI framework (MAUI Essentials included): the activity in front, a way to run on the
// main thread, and the permission to post notifications. It must be started before the first activity: Configure of the notification center and
// NativeDialogs.Initialize do it (the services of MAUI and of Uno do it when the application builds them).
internal static class AndroidHost
{
    private const string PostNotifications = "android.permission.POST_NOTIFICATIONS";
    private const int RequestCode = 0x5251;

    private static readonly Callbacks Tracker = new();
    private static int _started;

    /// <summary>The activity in front, or <see langword="null"/> when there is none (the application is starting or in the background) or when the host was started after it.</summary>
    public static Activity? CurrentActivity => Tracker.Current;

    /// <summary>Starts following the activities. Idempotent.</summary>
    public static void Start()
    {
        if (Interlocked.Exchange(ref _started, 1) == 1)
            return;

        (Application.Context as Application ?? Application.Context?.ApplicationContext as Application)?.RegisterActivityLifecycleCallbacks(Tracker);
    }

    /// <summary>Runs <paramref name="work"/> on the main thread: at once when that is this one.</summary>
    public static Task<T> OnMainThreadAsync<T>(Func<Task<T>> work)
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
    public static async Task<bool> CanPostNotificationsAsync()
    {
        if (!OperatingSystem.IsAndroidVersionAtLeast(33))
            return true;

        static bool Granted() => Application.Context.CheckSelfPermission(PostNotifications) == Permission.Granted;

        if (Granted())
            return true;

        return await OnMainThreadAsync(async () =>
        {
            if (CurrentActivity is not { } activity)
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
}
#endif
