# Routeables.Razor.Services

Dialogs, modals and notifications for the Blazor host, drawn with plain Razor, plus a default `MainLayout`. [`Routeables.Razor`](../Routeables.Razor/README.md) routes without it. Targets **.NET 10**.

```csharp
services.UseRouteables(Runtime.BlazorServer);
services.UseRouteablesServices(position: RouteableNotificationPosition.BottomLeft, maxDispayments: 5);
```

Everything is scoped. Each service has an optional factory to replace it; [`Reshapes.Razor.MudBlazor`](../Reshapes/README.md) passes the MudBlazor ones.

## What it brings

- The hosts that draw the services: `RouteableDialogHost`, `RouteableContentDialogHost`, `RouteableTaskDialogHost`, `RouteableModalHost`, `RouteableNotificationHost`. The default `MainLayout` already contains them; a layout of your own inherits `MainLayoutBase` and adds them.
- `RouteableModalFor<TView, TViewModel, TResult>`: shows a view in a modal and returns a typed result when its view model closes.
- `NotFound`, the default page for an unknown address.

Contracts: [`Routeables.Services`](../Routeables.Services/README.md).
