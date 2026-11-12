#if !ANDROID && !IOS && !MACCATALYST && !WINDOWS

using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Routeables.Native.Desktop;

namespace Routeables.Native;

// macOS, from the plain net10.0 target (the desktop of Avalonia, with no net10.0-macos target): UNUserNotificationCenter, spoken to through the Objective-C runtime
// (Desktop/ObjC.cs), the way Avalonia.Labs does it. The system only serves an application that is in a bundle (.app) with an identifier: a console or a
// `dotnet run` has none, and there nothing is created (CreateNotificationAsync returns null) rather than let the system abort the process.
// A tap is a tap; a swipe away is a dismissal (the category asks the system to report it).
public sealed partial class SystemNotificationCenter
{
    private const string MacCategory = "routeables.notification";
    private const string MacDefaultAction = "com.apple.UNNotificationDefaultActionIdentifier";
    private static SystemNotificationCenter? s_macOwner;   // the callbacks of the system are static: they reach the center through it

    private IntPtr _macCenter;

    private bool MacIsAvailable()
    {
        try
        {
            ObjC.Load();
            var bundle = ObjC.Send(ObjC.Class("NSBundle"), ObjC.Sel("mainBundle"));
            return bundle != IntPtr.Zero
                && ObjC.Send(bundle, ObjC.Sel("bundleIdentifier")) != IntPtr.Zero
                && ObjC.Class("UNUserNotificationCenter") != IntPtr.Zero;
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
        {
            return false;
        }
    }

    // The center, with its delegate (taps, and showing the banner while the application is in front) and its category, set once.
    private IntPtr MacCenter()
    {
        if (_macCenter != IntPtr.Zero)
            return _macCenter;

        unsafe
        {
            var center = ObjC.Send(ObjC.Class("UNUserNotificationCenter"), ObjC.Sel("currentNotificationCenter"));
            if (center == IntPtr.Zero)
                throw new InvalidOperationException("The system has no notification center for this application.");

            s_macOwner = this;
            var delegateClass = ObjC.NewClass("RouteablesNotificationDelegate",
                ("userNotificationCenter:willPresentNotification:withCompletionHandler:",
                    (IntPtr)(delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr, IntPtr, IntPtr, void>)&MacWillPresent, "v@:@@@?"),
                ("userNotificationCenter:didReceiveNotificationResponse:withCompletionHandler:",
                    (IntPtr)(delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr, IntPtr, IntPtr, void>)&MacDidReceive, "v@:@@@?"));
            var instance = ObjC.Send(ObjC.Send(delegateClass, ObjC.Sel("alloc")), ObjC.Sel("init"));   // kept for ever: the center only keeps a weak reference
            ObjC.Send(center, ObjC.Sel("setDelegate:"), instance);

            // UNNotificationCategoryOptionCustomDismissAction (1): the delegate is told when the user dismisses the notification.
            var category = ObjC.Send(ObjC.Class("UNNotificationCategory"), ObjC.Sel("categoryWithIdentifier:actions:intentIdentifiers:options:"),
                ObjC.NSString(MacCategory), ObjC.Send(ObjC.Class("NSArray"), ObjC.Sel("array")), ObjC.Send(ObjC.Class("NSArray"), ObjC.Sel("array")), 1);
            ObjC.Send(center, ObjC.Sel("setNotificationCategories:"), ObjC.Send(ObjC.Class("NSSet"), ObjC.Sel("setWithObject:"), category));

            return _macCenter = center;
        }
    }

    private async Task<bool> MacRequestPermissionAsync()
    {
        if (!MacIsAvailable())
            return false;

        var center = MacCenter();
        var source = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var handle = GCHandle.Alloc(source);
        try
        {
            unsafe
            {
                var block = ObjC.NewBlock((IntPtr)(delegate* unmanaged[Cdecl]<IntPtr, byte, IntPtr, void>)&MacAuthorized, GCHandle.ToIntPtr(handle));
                // UNAuthorizationOptionSound (2) | UNAuthorizationOptionAlert (4)
                ObjC.Send(center, ObjC.Sel("requestAuthorizationWithOptions:completionHandler:"), 6, block);
            }

            return await source.Task;
        }
        finally
        {
            handle.Free();
        }
    }

    private void MacPresent(SystemNotification notification)
    {
        var center = MacCenter();
        var content = ObjC.New("UNMutableNotificationContent");
        ObjC.Send(content, ObjC.Sel("setTitle:"), ObjC.NSString(notification.Title));
        ObjC.Send(content, ObjC.Sel("setBody:"), ObjC.NSString(notification.Message));
        ObjC.Send(content, ObjC.Sel("setCategoryIdentifier:"), ObjC.NSString(MacCategory));

        // No trigger: delivered at once. No completion handler: a failure is not reported (the system shows nothing).
        var request = ObjC.Send(ObjC.Class("UNNotificationRequest"), ObjC.Sel("requestWithIdentifier:content:trigger:"),
            ObjC.NSString(notification.Id.ToString(CultureInfo.InvariantCulture)), content, IntPtr.Zero);
        ObjC.Send(center, ObjC.Sel("addNotificationRequest:withCompletionHandler:"), request, IntPtr.Zero);
    }

    private void MacClose(uint id)
    {
        var center = MacCenter();
        var ids = ObjC.Send(ObjC.Class("NSArray"), ObjC.Sel("arrayWithObject:"), ObjC.NSString(id.ToString(CultureInfo.InvariantCulture)));
        ObjC.Send(center, ObjC.Sel("removePendingNotificationRequestsWithIdentifiers:"), ids);
        ObjC.Send(center, ObjC.Sel("removeDeliveredNotificationsWithIdentifiers:"), ids);
    }

    private void MacCloseAll()
    {
        var center = MacCenter();
        ObjC.Send(center, ObjC.Sel("removeAllPendingNotificationRequests"));
        ObjC.Send(center, ObjC.Sel("removeAllDeliveredNotifications"));
    }

    // While the application is in front the system shows nothing by itself: ask for the banner and the list (UNNotificationPresentationOptionBanner | List).
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static void MacWillPresent(IntPtr self, IntPtr command, IntPtr center, IntPtr notification, IntPtr completion)
        => ObjC.CallBlock(completion, (1 << 4) | (1 << 3));

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static void MacDidReceive(IntPtr self, IntPtr command, IntPtr center, IntPtr response, IntPtr completion)
    {
        try
        {
            var action = ObjC.String(ObjC.Send(response, ObjC.Sel("actionIdentifier")));
            var request = ObjC.Send(ObjC.Send(response, ObjC.Sel("notification")), ObjC.Sel("request"));
            var identifier = ObjC.String(ObjC.Send(request, ObjC.Sel("identifier")));
            if (uint.TryParse(identifier, NumberStyles.None, CultureInfo.InvariantCulture, out var id))
                s_macOwner?.Complete(id, activated: action == MacDefaultAction, action: null);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.TraceWarning($"Routeables: a notification response could not be read: {ex.Message}");
        }
        finally
        {
            ObjC.CallBlock(completion);
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static void MacAuthorized(IntPtr block, byte granted, IntPtr error)
    {
        var state = ObjC.BlockState(block);
        if (state != IntPtr.Zero && GCHandle.FromIntPtr(state).Target is TaskCompletionSource<bool> source)
            source.TrySetResult(granted != 0);
    }
}
#endif
