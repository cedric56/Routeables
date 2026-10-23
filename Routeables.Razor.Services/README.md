# Routeables.Razor.Services

The optional UI services of the Blazor host: dialogs, content dialogs, task dialogs, modals and toast notifications, with the plain Razor hosts that
draw them and a default `MainLayout`. [`Routeables.Razor`](../Routeables.Razor/README.md) alone routes; add this package when you want these services without a component library.

Targets **.NET 10** (`net10.0`), version `1.0.0-preview.1`, namespace `Routeables.Razor.Services`. It references `Routeables.Razor` and the contracts of
[`Routeables.Services`](../Routeables.Services/README.md).

```csharp
using Routeables.Razor;
using Routeables.Razor.Services;

services.UseRouteables(Runtime.BlazorServer);                 // the router
services.UseRouteablesServices(                                 // the UI services (all scoped)
    position: RouteableNotificationPosition.BottomLeft,
    maxDispayments: 5);
```

Each service has an optional factory (`routeableDialogService`, `routeableContentDialogService`, `routeableTaskDialogService`, `routeableNotificationService`)
to replace it; [`Reshapes.Razor.MudBlazor`](../Reshapes/README.md) passes the MudBlazor ones.

## What it contains

- `RouteableDialogHost`, `RouteableContentDialogHost`, `RouteableTaskDialogHost`, `RouteableModalHost` and `RouteableNotificationHost`: put them once in your layout.
- `MainLayout` (it holds the five hosts) and `NotFound`, the default layout and page. A layout of your own inherits `MainLayoutBase` from `Routeables.Razor`.
- `RouteableModalFor<TView, TViewModel, TResult>`: shows a view in a modal and returns a typed result when the view model closes.

Without this package nothing of this is registered: `IRouteableDialogService` and the others resolve to nothing, and the theme service
(`IRouteableRequestedThemeService`) stays in `Routeables.Razor`.
