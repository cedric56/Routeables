#if !ANDROID && !IOS && !MACCATALYST && !WINDOWS

using System.Runtime.CompilerServices;
using System.Runtime.Versioning;
using Microsoft.Win32;
using Windows.Data.Xml.Dom;
using Windows.UI.Notifications;

namespace Routeables.Native;

// Windows, from the plain net10.0 target (the desktop of Avalonia, WPF, a console: no Windows target needed): the toasts of Windows 10 and 11 through the
// WindowsContracts.Net.Foundation.UniversalApiContract package (the types of the Windows runtime, compile-time only: PrivateAssets="all"). An application with no
// package has no identity for the system, so one is registered for the user (HKCU\Software\Classes\AppUserModelId). A tap is a tap; the toast going away is a dismissal.
public sealed partial class SystemNotificationCenter
{
    private readonly object _windowsGate = new();
    private readonly Dictionary<uint, object> _windowsToasts = [];   // the toasts must stay referenced while shown, or their events stop

    [SupportedOSPlatform("windows")]
    private static string WindowsAppId()
    {
        var name = AppDomain.CurrentDomain.FriendlyName;
        var id = "Routeables." + new string(name.Where(char.IsLetterOrDigit).ToArray());
        using var key = Registry.CurrentUser.CreateSubKey(@"Software\Classes\AppUserModelId\" + id);
        if (key.GetValue("DisplayName") is null)
            key.SetValue("DisplayName", name);
        return id;
    }

    /// <summary>Whether the toasts can be shown: Windows 10 1809 or later, and the Windows runtime types are there (a package that does not flow to the application would leave them out).</summary>
    private static bool WindowsIsAvailable()
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 17763))
            return false;

        try { return WindowsCheck(); }
        catch (Exception ex) when (ex is FileNotFoundException or TypeLoadException or InvalidOperationException or System.Runtime.InteropServices.COMException)
        {
            return false;
        }
    }

    // In a method of its own so that a missing assembly fails here and not while the caller is compiled.
    [MethodImpl(MethodImplOptions.NoInlining)]
    [SupportedOSPlatform("windows10.0.17763.0")]
    private static bool WindowsCheck() => ToastNotificationManager.CreateToastNotifier(WindowsAppId()) is not null;

    // No toast where the system or the Windows runtime types cannot show one (a session with no desktop, the types missing): the notification completes as dismissed.
    [SupportedOSPlatform("windows10.0.17763.0")]
    private void WindowsShow(SystemNotification notification)
    {
        try
        {
            if (WindowsIsAvailable())
            {
                WindowsPresent(notification);
                return;
            }
        }
        catch (Exception ex) when (ex is FileNotFoundException or TypeLoadException or System.Runtime.InteropServices.COMException)
        {
            System.Diagnostics.Trace.TraceWarning($"Routeables: Windows could not show the notification: {ex.Message}");
        }

        Complete(notification.Id, activated: false, action: null);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    [SupportedOSPlatform("windows10.0.17763.0")]
    private void WindowsPresent(SystemNotification notification)
    {
        var xml = new XmlDocument();
        xml.LoadXml("<toast><visual><binding template=\"ToastGeneric\"><text>"
            + System.Security.SecurityElement.Escape(notification.Title ?? string.Empty) + "</text><text>"
            + System.Security.SecurityElement.Escape(notification.Message ?? string.Empty) + "</text></binding></visual></toast>");

        var toast = new ToastNotification(xml);
        var id = notification.Id;
        toast.Activated += (_, _) => WindowsEnded(id, activated: true);
        toast.Dismissed += (_, e) =>
        {
            // The application hiding it (Cancel) is not the user dismissing it.
            if (e.Reason != ToastDismissalReason.ApplicationHidden)
                WindowsEnded(id, activated: false);
        };
        toast.Failed += (_, _) => WindowsEnded(id, activated: false);

        lock (_windowsGate)
            _windowsToasts[id] = toast;
        ToastNotificationManager.CreateToastNotifier(WindowsAppId()).Show(toast);
    }

    private void WindowsEnded(uint id, bool activated)
    {
        lock (_windowsGate)
        {
            if (!_windowsToasts.Remove(id))
                return;
        }

        Complete(id, activated, action: null);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    [SupportedOSPlatform("windows10.0.17763.0")]
    private void WindowsClose(uint id)
    {
        object? toast;
        lock (_windowsGate)
            _windowsToasts.Remove(id, out toast);

        if (toast is ToastNotification notification)
            ToastNotificationManager.CreateToastNotifier(WindowsAppId()).Hide(notification);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    [SupportedOSPlatform("windows10.0.17763.0")]
    private void WindowsCloseAll()
    {
        uint[] ids;
        lock (_windowsGate)
            ids = [.. _windowsToasts.Keys];

        foreach (var id in ids)
            WindowsClose(id);
    }
}
#endif