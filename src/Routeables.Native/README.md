# Routeables.Native

The system notifications of the platform for Routeables, with no UI framework: `Notification.Builder` on Android (no AndroidX, no MAUI Essentials: the activity in front is followed by the library itself), `UserNotifications` on iOS and Mac Catalyst, `AppNotificationManager` of the Windows App SDK on Windows (a WinUI 3 application included). The plain `net10.0` target has none: `CreateNotificationAsync` returns `null`.

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
