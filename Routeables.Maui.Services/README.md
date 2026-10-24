# Routeables.Maui.Services

Dialogs, modal pages and toast notifications for the MAUI host. [`Routeables.Maui`](../Routeables.Maui/README.md) routes without it. Same target frameworks as the host.

```csharp
builder
    .UseRouteables()
    .UseRouteablesServices()
    .UseMauiApp<App>();
```

`UseRouteablesServices` takes optional factories to replace a service. For CommunityToolkit.Maui popups and snackbars use `UseRouteablesCommunityToolkit()` from [`Reshapes`](../Reshapes/README.md), which calls both setup methods for you.

## What it brings

- Dialogs, content dialogs, task dialogs and notifications. Toasts are native on Windows, Android, iOS and Mac Catalyst, and an overlay on the plain `net10.0` target.
- `RouteableModalFor<TViewModel, TResult>`: the base of a modal view that returns a typed result.

Dialogs and modals need an active page to appear on. Contracts: [`Routeables.Services`](../Routeables.Services/README.md).
