#if WINDOWS

using Microsoft.Windows.AppNotifications;
using Microsoft.Windows.AppNotifications.Builder;
using System.Runtime.CompilerServices;

namespace Routeables.Native;

// Windows, from the Windows target (net10.0-windows10.0.x). Two ways, chosen once at the first notification:
// - AppNotificationManager of the Windows App SDK, where the application runs with it (WinUI 3, MAUI). Windows reports a tap, not a dismissal.
// - The toasts of Windows itself (SystemNotificationCenter.winrt.cs) everywhere else. An Avalonia or WPF application that targets
//   net10.0-windows10.0.x gets this build of the library, and has no Windows App SDK runtime: AppNotificationManager.Default cannot even be
//   created there. It shows the same toasts as an application of the plain net10.0 target.
public sealed partial class SystemNotificationCenter
{
    private const string IdKey = "id";

    private readonly object _appSdkGate = new();
    private bool? _appSdk;

    /// <summary>Whether the notifications go through the Windows App SDK: asked once, when the first notification needs it.</summary>
    private bool UsesAppSdk()
    {
        lock (_appSdkGate)
        {
            if (_appSdk is { } known)
                return known;

            bool usable;
            try
            {
                usable = AppSdkStart();
            }
            catch (Exception ex) // no runtime of the Windows App SDK, no bootstrap, an assembly that is not there: whatever it is, the toasts are used
            {
                System.Diagnostics.Trace.TraceInformation($"Routeables: the Windows App SDK notifications are not available ({ex.GetType().Name}), the toasts of Windows are used.");
                usable = false;
            }

            _appSdk = usable;
            return usable;
        }
    }

    // In methods of their own (NoInlining): an assembly of the Windows App SDK that is missing fails in them, where it is caught, and not while
    // the caller is compiled.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private bool AppSdkStart()
    {
        if (!AppNotificationManager.IsSupported())
            return false;

        var manager = AppNotificationManager.Default;
        manager.NotificationInvoked += (_, args) =>
        {
            if (args.Arguments.TryGetValue(IdKey, out var text) && uint.TryParse(text, out var id))
                Complete(id, activated: true, action: null);
        };
        manager.Register();
        return true;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool AppSdkEnabled() => AppNotificationManager.Default.Setting == AppNotificationSetting.Enabled;

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void AppSdkShow(SystemNotification notification)
    {
        var builder = new AppNotificationBuilder()
            .AddArgument(IdKey, notification.Id.ToString())
            .AddText(notification.Title ?? string.Empty)
            .AddText(notification.Message ?? string.Empty);

        var toast = builder.BuildNotification();
        toast.Tag = notification.Id.ToString();
        AppNotificationManager.Default.Show(toast);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void AppSdkCancel(uint id) => _ = AppNotificationManager.Default.RemoveByTagAsync(id.ToString()).AsTask();

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void AppSdkCancelAll() => _ = AppNotificationManager.Default.RemoveAllAsync().AsTask();

    private partial Task<bool> RequestPermissionAsync()
    {
        if (!UsesAppSdk())
            return Task.FromResult(WindowsIsAvailable());

        try
        {
            return Task.FromResult(AppSdkEnabled());
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.Runtime.InteropServices.COMException)
        {
            // The notification platform is not available to this application.
            return Task.FromResult(false);
        }
    }

    private partial Task PresentAsync(SystemNotification notification)
    {
        Present(notification);
        return Task.CompletedTask;
    }

    private partial void Present(SystemNotification notification)
    {
        if (UsesAppSdk())
            AppSdkShow(notification);
        else
            WindowsShow(notification);
    }

    private partial void Cancel(uint id)
    {
        if (UsesAppSdk())
            AppSdkCancel(id);
        else
            WindowsClose(id);
    }

    private partial void CancelAll()
    {
        if (UsesAppSdk())
            AppSdkCancelAll();
        else
            WindowsCloseAll();
    }
}
#endif
