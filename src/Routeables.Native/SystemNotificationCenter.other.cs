#if !ANDROID && !IOS && !MACCATALYST && !WINDOWS

using System.Runtime.Versioning;

namespace Routeables.Native;

// The plain net10.0 target is the desktop of Avalonia, WPF and the console, and the browser of WebAssembly. It dispatches by operating system to the partial files
// that carry one each: SystemNotificationCenter.linux.cs (D-Bus), .winrt.cs (Windows toasts) and .browser.cs (Notification API), .macos.cs (UNUserNotificationCenter through the Objective-C runtime).
public sealed partial class SystemNotificationCenter
{
    private partial async Task<bool> RequestPermissionAsync()
    {
        if (OperatingSystem.IsBrowser())
            return await BrowserPermissionAsync(ask: true) == "granted";

        return (OperatingSystem.IsLinux() && LinuxIsAvailable()) || (OperatingSystem.IsWindows() && WindowsIsAvailable()) || (OperatingSystem.IsMacOS() && await MacRequestPermissionAsync());
    }

    private partial void Present(SystemNotification notification)
    {
        if (OperatingSystem.IsBrowser())
            _ = BrowserShowAsync(notification);
        else if (OperatingSystem.IsLinux())
            LinuxPresent(notification);
        else if (OperatingSystem.IsWindowsVersionAtLeast(10, 0, 17763))
            WindowsPresent(notification);
        else if (OperatingSystem.IsMacOS())
            MacPresent(notification);
    }

    private partial void Cancel(uint id)
    {
        if (OperatingSystem.IsBrowser())
            BrowserClose(id);
        else if (OperatingSystem.IsLinux())
            LinuxClose(id);
        else if (OperatingSystem.IsWindowsVersionAtLeast(10, 0, 17763))
            WindowsClose(id);
        else if (OperatingSystem.IsMacOS())
            MacClose(id);
    }

    private partial void CancelAll()
    {
        if (OperatingSystem.IsBrowser())
            BrowserCloseAll();
        else if (OperatingSystem.IsWindowsVersionAtLeast(10, 0, 17763))
            WindowsCloseAll();
        else if (OperatingSystem.IsMacOS())
            MacCloseAll();
        else if (OperatingSystem.IsLinux())
            foreach (var id in ActiveNotifications().Keys)
                LinuxClose(id);
    }
}
#endif
