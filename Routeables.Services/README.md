# Routeables.Services

The contracts of the user-interface services that the hosts implement in their optional services packages ([Razor](../Routeables.Razor.Services/README.md), [MAUI](../Routeables.Maui.Services/README.md), [Avalonia](../Routeables.Avalonia.Services/README.md)); the theme service stays in the host. They are not needed to route: a project that only uses `IRouter` references [`Routeables.Abstraction`](../Routeables.Abstraction/README.md) and [`Routeables.Routing`](../Routeables.Routing/README.md) and carries none of this.

Targets **.NET 10** (`net10.0`), version `1.0.0-preview.1`. The types are in the `Routeables.Services` namespace.

| Interface | Purpose |
| --- | --- |
| `IRouteableDialogs` | **The one to inject**: gives the three services below as `MessageBox`, `ContentDialog` and `TaskDialog`. Each host's `UseRouteablesServices` registers it. |
| `IRouteableDialogService` | Message boxes and simple dialogs |
| `IRouteableContentDialogService` | Dialogs that host a view and a view model (`RouteableContentDialog`, `RouteableContentDialogResult`) |
| `IRouteableTaskDialogService` | Multi-step / button-rich task dialogs (`RouteableTaskDialog`, `RouteableTaskDialogResult`) |
| `IRouteableNotificationService` | In-app toast notifications (`RouteableNotificationType`, `RouteableNotificationPosition`) |
| `IRouteableRequestedThemeService` | Light/dark theme switching (`RouteableRequestedTheme`) |

Two small base classes are shared by the hosts that draw their dialogs themselves (Avalonia and Uno): `RouteableDialogServiceBase` writes the confirmations of `IRouteableDialogService` once, over a single `ShowMessageAsync(message, title, buttonText)` that returns the index of the button chosen (-1 when dismissed); `RouteableContentDialogServiceBase` answers them with a content dialog, over `ShowAsync`. The words of the buttons (`"YesNoCancel"`) are split by an internal helper, not part of the API.

`RouteableContentDialogSession` and `RouteableTaskDialogSession` hold what a dialog does whatever draws it: its buttons (`RouteableDialogButton`), the command or click callback of a press, the `Closing` veto (it returns `true` to keep the dialog open), `Opening`, `Opened`, `Closed` and the close from code. Razor, Avalonia, Uno and MAUI draw their dialogs over them (and the MudBlazor and CommunityToolkit reshapes too), so they all behave alike: a press runs the command (or the click callback), then asks `Closing`, then closes.

`RouteableNotificationSession` holds what a notification does on every host: the delay (`AutoCloseAfter`, `null` for `Never`), a tap that runs `onClick` and then closes it, and an `onClose` that runs once. A notification closes after `RouteableNotificationDefaults.Expiration` (five seconds) on every host unless `expiration` says otherwise; `RouteableNotificationDefaults.Never` keeps it until it is closed.

The contract of OS notifications (`IRouteableSystemNotificationService`) is here too but hidden from IntelliSense: no host implements it yet.

A host registers its implementations; the application resolves the interface it needs. The look can be replaced by a [Reshape](../Reshapes/README.md).
