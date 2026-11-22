#if !ANDROID && !IOS && !MACCATALYST && !WINDOWS

using System.Runtime.Versioning;

namespace Routeables.Native;

// The plain net10.0 target is the desktop of Avalonia, WPF and the console, and the WebAssembly browser. It dispatches by operating system
// to one partial file each: SystemNotificationCenter.linux.cs (D-Bus), .winrt.cs (Windows toasts), .browser.cs (Notification API) and
// .macos.cs (UNUserNotificationCenter through the Objective-C runtime).
public sealed partial class SystemNotificationCenter
{
    private partial async Task<bool> RequestPermissionAsync()
    {
        if (OperatingSystem.IsBrowser())
            return await BrowserPermissionAsync(ask: true) == "granted";

        return (OperatingSystem.IsLinux() && await LinuxIsAvailableAsync()) || (OperatingSystem.IsWindows() && WindowsIsAvailable()) || (OperatingSystem.IsMacOS() && await MacRequestPermissionAsync());
    }

    private partial Task PresentAsync(SystemNotification notification)
    {
        // The D-Bus is spoken without ever blocking the caller: Linux is the platform that answers late.
        if (OperatingSystem.IsLinux())
            return LinuxPresentAsync(notification);

        Present(notification);
        return Task.CompletedTask;
    }

    private partial void Present(SystemNotification notification)
    {
        if (OperatingSystem.IsBrowser())
            _ = BrowserShowAsync(notification);
        else if (OperatingSystem.IsWindowsVersionAtLeast(10, 0, 17763))
            WindowsShow(notification);
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
