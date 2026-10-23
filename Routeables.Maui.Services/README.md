# Routeables.Maui.Services

The optional UI services of the MAUI host: dialogs, content dialogs, task dialogs, modal pages and toast notifications.
[`Routeables.Maui`](../Routeables.Maui/README.md) alone routes; add this package when you want these services.

Version `1.0.0-preview.1`, same target frameworks as `Routeables.Maui` (`net10.0`, Android, iOS, Mac Catalyst and Windows), namespace `Routeables.Maui.Services`.
It references `Routeables.Maui` and the contracts of [`Routeables.Services`](../Routeables.Services/README.md).

```csharp
using Routeables.Maui;
using Routeables.Maui.Services;

var builder = MauiApp.CreateBuilder();
builder
    .UseRouteables()            // the router
    .UseRouteablesServices()     // the UI services
    .UseMauiApp<App>();
```

`UseRouteablesServices` takes an optional factory for the modal, content dialog, task dialog and notification services. For CommunityToolkit.Maui popups
and snackbars use `UseRouteablesCommunityToolkit()` from [`Reshapes.Maui.Community`](../Reshapes/README.md), which calls both for you.

## What it contains

- `IRouteableModalService` and `IRouteableDialogWindowContext` (the page a dialog is drawn on), and `RouteableModalFor<TViewModel, TResult>`, the base of a modal view.
- The default implementations: dialogs, content dialogs, task dialogs, modal pages and notifications. Toasts use the platform's own on Windows, Android, iOS and Mac Catalyst (UIKit cards on the key window) and an overlay on the plain `net10.0` target.

Dialogs and modals need an active page to be shown on.
