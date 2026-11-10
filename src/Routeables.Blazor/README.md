# Routeables.Blazor

The Blazor host of Routeables: Blazor Server, Blazor WebAssembly and MAUI Blazor Hybrid. It plugs the router into Razor components and keeps the browser address in step with it. Targets **.NET 10**.

```bash
dotnet add package Routeables.Blazor
```

The package brings `Routeables` (routing, abstractions and the source generator): there is nothing else to add.

## Set up

```csharp
services.UseRouteablesServer();      // or UseRouteablesWebAssembly, UseRouteablesHybrid
```

An application with a menu gives its menu view model: `services.UseRouteablesServer<MainViewModel>();`.

Everything is scoped, so each Blazor Server circuit has its own history, and so has its menu. Nothing is discovered for you: the menu view model and the routes are what you give.

## A view

```razor
@* samples/XAnimals.Razor/Components/Animals/DogDetailView.razor *@
@inherits RouteableFor<DogDetailView, DogDetailViewModel>

<AnimalDetail ViewModel="ViewModel" />
```

`RouteableFor<TView, TViewModel>` gives the typed `ViewModel` and the lifecycle hooks. A zone that shows a region is `<RouteableRegion Region="shell-content" />`.

## The layout

A layout inherits `RouteableMainLayoutBase`: it follows the current view, the lock and the theme. The ready-made `RouteableMainLayout` comes with the [dialogs and notifications](#dialogs-modals-and-notifications).

`<RouteableMenu />` is the menu of the layout as a component of its own: one button per entry of the menu view model, the current one marked, the locked ones disabled (`Closed`, `Class` and `Label` are its parameters). Put it in a layout of your own the same way.

For a route declared with an explicit address (`/customers/{id:int}`), the browser address and the router stay in step: a route change updates the address, a typed address or the browser's back button route the router, and a refused change puts the address back.

Who decides the route: Routeables. The package ships `Main.razor`, whose two `@page` routes (`/` and `/{*path}`) only give the Blazor router a page for every address so that it never shows `NotFound`; it draws nothing, and the layout shows the current view of the Routeables router. Declare the routes of the application on its view models (`[Routeable("about")]`) or with `RegisterRoute`, not with `@page`: a page of your own with an `@page` is rendered by Blazor, outside the router, and neither its history nor its guards apply to it. A layout of your own can use `NavLink` with the route as `href`, since the address follows the route.

## Dialogs, modals and notifications

Drawn with plain Razor (`Routeables.Blazor.Services` namespace). `UseRouteablesServer` (and `UseRouteablesWebAssembly`, `UseRouteablesHybrid`) already registers them; call `UseRouteablesServices` yourself only to move the notifications, to replace a service or to draw the dialogs with another `IMessageBoxService`:

```csharp
services.UseRouteablesServer<MainViewModel>();
services.UseRouteablesServices(position: RouteableNotificationPosition.TopRight, maxDisplayed: 3);   // optional
```

Everything is scoped. There is one `IRouteableDialogService` (message boxes, content dialogs and task dialogs): it asks an `IMessageBoxService` to draw them, `DefaultMessageBoxService` (plain Razor, shown by the hosts) unless `UseRouteablesServices(messageBoxService: sp => ...)` gives another one; [`Reshapes.Blazor.MudBlazor`](../../reshapes/README.md) gives the MudBlazor one. The notification service has an optional factory too.

- The hosts that draw the services: `RouteableDialogHost`, `RouteableContentDialogHost`, `RouteableTaskDialogHost`, `RouteableModalHost`, `RouteableNotificationHost`. The default `RouteableMainLayout` already contains them; a layout of your own inherits `RouteableMainLayoutBase` and adds them.
- `RouteableModalFor<TView, TViewModel, TResult>`: shows a view in a modal and returns a typed result when its view model closes.
- `NotFound`, the default page for an unknown address.

Contracts: [`Routeables.Services`](../Routeables.Services/README.md).

## Good to know

- A component must be safe to dispose twice: the router disposes pages that leave its history, and Blazor disposes what it renders.
- The first time a page is shown, Blazor renders the very instance the router created, so the hooks of the view run on the displayed component. When the page comes back from the history, Blazor builds another instance: keep the state that a hook reads in the view model. See the [Blazor guide](../../docs/guides/blazor.md).
- A `Transient` service that is `IDisposable` lives until the circuit ends: register it `Scoped` or `Singleton`. See [lifecycle](../../docs/guides/lifecycle.md).
- MudBlazor flavour: [`Reshapes`](../../reshapes/README.md).

## Next

[Routeables for Blazor developers](../../docs/guides/blazor.md), [hosts guide](../../docs/guides/hosts.md), samples `samples/XAnimals.Razor` and the Blazor Server and WebAssembly heads.
