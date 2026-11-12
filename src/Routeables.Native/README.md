# Routeables.Native

The system notifications of the platform for Routeables, with no UI framework: `Notification.Builder` on Android (no AndroidX, no MAUI Essentials: the activity in front is followed by the library itself), `UserNotifications` on iOS and Mac Catalyst, `AppNotificationManager` of the Windows App SDK on Windows (a WinUI 3 application included). The plain `net10.0` target (the desktop of Avalonia, WPF, a console) has them on Linux (`org.freedesktop.Notifications` on the D-Bus session bus, spoken by a small client of the library, no package) and on Windows (toasts, through the `WindowsContracts.Net.Foundation.UniversalApiContract` package, no Windows SDK target); in the browser of a WebAssembly application that is not Blazor (Avalonia, Uno) through the Notification API (the JavaScript module is shared with `Routeables.Blazor`; copy `_content/Routeables.Native/routeables-notifications-worker.js` (a static web asset of the package) to the root of the site for action buttons and a reply field); on macOS (the plain `net10.0` build) through `UNUserNotificationCenter` reached by the Objective-C runtime, for an application in a `.app` bundle (outside a bundle `CreateNotificationAsync` returns `null`).

It is the engine behind `WithAppNotifications` of `Routeables.Maui` and of `Routeables.Blazor` in a MAUI Blazor Hybrid application, so you rarely use it directly. In a WinUI 3 application:

```csharp
SystemNotificationCenter.Current.Configure(new AppNotificationOptions());
var notification = await SystemNotificationCenter.Current.CreateNotificationAsync("animals");
if (notification is null) return;
notification.Title = "New animal";
notification.Message = "A polar bear was added to the adoption list.";
await notification.ShowAsync();
```

On Android, call `SystemNotificationCenter.HandleIntent(intent)` from `OnCreate` and `OnNewIntent` of the main activity so that a tap comes back as `NotificationCompleted`. See [System integration](../../docs/guides/system-integration.md).
