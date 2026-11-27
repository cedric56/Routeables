#if !ANDROID && !IOS && !MACCATALYST

using Microsoft.Win32;
using System.Runtime.CompilerServices;
using System.Runtime.Versioning;
using Windows.Data.Xml.Dom;
using Windows.UI.Notifications;

namespace Routeables.Native;

// Windows, from the plain net10.0 target (the desktop of Avalonia, WPF, a console: no Windows target needed): Windows 10 and 11 toasts
// through the WindowsContracts.Net.Foundation.UniversalApiContract package (the Windows runtime types, compile-time only: PrivateAssets="all").
// An application without a package has no identity for the system, so one is registered for the user (HKCU\Software\Classes\AppUserModelId).
// The Windows target (net10.0-windows10.0.x) compiles this file too: its own types are the Windows runtime ones, and it falls back on these toasts
// when the Windows App SDK cannot be used (an Avalonia or WPF application that targets Windows: see SystemNotificationCenter.windows.cs).
// A tap is a tap; the toast going away is a dismissal.
public sealed partial class SystemNotificationCenter
{
    private readonly object _windowsGate = new();
    private readonly Dictionary<uint, object> _windowsToasts = []; // the toasts must stay referenced while shown, or their events stop

    private static string? s_windowsAppId;

    // Read once: each notification asks for it several times, and the registry does not change under a running application.
    [SupportedOSPlatform("windows")]
    private static string WindowsAppId() => s_windowsAppId ??= RegisterWindowsAppId();

    [SupportedOSPlatform("windows")]
    private static string RegisterWindowsAppId()
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

    // No toast where the system or the Windows runtime types cannot show one (a session with no desktop, missing types): the notification completes as dismissed.
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

        // The same tag replaces the toast that is shown (an update) instead of adding a second one.
        toast.Tag = id.ToString(System.Globalization.CultureInfo.InvariantCulture);
        toast.Group = "routeables";

        // The events of a toast that was replaced are no longer the notification's.
        bool IsCurrent() { lock (_windowsGate) return _windowsToasts.TryGetValue(id, out var current) && ReferenceEquals(current, toast); }
        toast.Activated += (_, _) => { if (IsCurrent()) WindowsEnded(id, activated: true); };
        toast.Dismissed += (_, e) =>
        {
            // Only the user closing it ends the notification. The banner leaving the screen after a few seconds (TimedOut) keeps it in the
            // notification center, where it can still be clicked, and the application hiding it (Cancel) is not the user either.
            if (e.Reason == ToastDismissalReason.UserCanceled && IsCurrent())
                WindowsEnded(id, activated: false);
        };
        toast.Failed += (_, _) => { if (IsCurrent()) WindowsEnded(id, activated: false); };

        lock (_windowsGate)
            _windowsToasts[id] = toast;
        try
        {
            ToastNotificationManager.CreateToastNotifier(WindowsAppId()).Show(toast);
        }
        catch
        {
            // Not shown: its events never come, and nothing else would let go of it.
            lock (_windowsGate)
            {
                if (_windowsToasts.TryGetValue(id, out var current) && ReferenceEquals(current, toast))
                    _windowsToasts.Remove(id);
            }
            throw;
        }
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