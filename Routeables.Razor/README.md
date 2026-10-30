# Routeables.Razor

The Blazor host of Routeables: Blazor Server, Blazor WebAssembly and MAUI Blazor Hybrid. It plugs the router into Razor components and keeps the browser address in step with it. Targets **.NET 10**.

```bash
dotnet add package Routeables.Razor
```

The package brings `Routeables` (routing, abstractions and the source generator): there is nothing else to add.

## Set up

```csharp
services.UseRouteablesServer<MainViewModel>(      // or UseRouteablesWebAssembly, UseRouteablesHybrid
    r => r.AddGeneratedRoutes());
```

Everything is scoped, so each Blazor Server circuit has its own history, and so has its menu. Nothing is discovered for you: the main view model and the routes are what you give.

## A view

```razor
@* Samples/XAnimals.Razor/Components/Animals/DogDetailView.razor *@
@inherits RouteableFor<DogDetailView, DogDetailViewModel>

<AnimalDetail ViewModel="ViewModel" />
```

`RouteableFor<TView, TViewModel>` gives the typed `ViewModel` and the lifecycle hooks. A zone that shows a region is `<RouteableRegion Region="shell-content" />`.

## The layout

A layout inherits `MainLayoutBase`: it follows the current view, the lock and the theme. The ready-made `MainLayout` comes with the [dialogs and notifications](#dialogs-modals-and-notifications).

For a route declared with an explicit address (`/customers/{id:int}`), the browser address and the router stay in step: a route change updates the address, a typed address or the browser's back button route the router, and a refused change puts the address back.

Who decides the route: Routeables. The package ships `Main.razor`, whose two `@page` routes (`/` and `/{*path}`) only give the Blazor router a page for every address so that it never shows `NotFound`; it draws nothing, and the layout shows the current view of the Routeables router. Declare the routes of the application on its view models (`[Routeable("about")]`) or with `RegisterRoute`, not with `@page`: a page of your own with an `@page` is rendered by Blazor, outside the router, and neither its history nor its guards apply to it. A layout of your own can use `NavLink` with the route as `href`, since the address follows the route.

## Dialogs, modals and notifications

Drawn with plain Razor (`Routeables.Razor.Services` namespace). `UseRouteablesServer` (and `UseRouteablesWebAssembly`, `UseRouteablesHybrid`) already registers them; call `UseRouteablesServices` yourself only to move the notifications, to replace a service or to draw the dialogs with another `IMessageBoxService`:

```csharp
services.UseRouteablesServer<MainViewModel>();
services.UseRouteablesServices(position: RouteableNotificationPosition.TopRight, maxDispayments: 3);   // optional
```

Everything is scoped. There is one `IRouteableDialogService` (message boxes, content dialogs and task dialogs): it asks an `IMessageBoxService` to draw them, `DefaultMessageBoxService` (plain Razor, shown by the hosts) unless `UseRouteablesServices(messageBoxService: sp => ...)` gives another one; [`Reshapes.Razor.MudBlazor`](../Reshapes/README.md) gives the MudBlazor one. The notification service has an optional factory too.

- The hosts that draw the services: `RouteableDialogHost`, `RouteableContentDialogHost`, `RouteableTaskDialogHost`, `RouteableModalHost`, `RouteableNotificationHost`. The default `MainLayout` already contains them; a layout of your own inherits `MainLayoutBase` and adds them.
- `RouteableModalFor<TView, TViewModel, TResult>`: shows a view in a modal and returns a typed result when its view model closes.
- `NotFound`, the default page for an unknown address.

Contracts: [`Routeables.Services`](../Routeables.Services/README.md).

## Good to know

- A component must be safe to dispose twice: the router disposes pages that leave its history, and Blazor disposes what it renders.
- A `Transient` service that is `IDisposable` lives until the circuit ends: register it `Scoped` or `Singleton`. See [lifecycle](../docs/guides/lifecycle.md).
- MudBlazor flavour: [`Reshapes`](../Reshapes/README.md).

## Next

[Hosts guide](../docs/guides/hosts.md), samples `Samples/XAnimals.Razor` and the Blazor Server and WebAssembly heads.
