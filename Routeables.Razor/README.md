# Routeables.Razor

The Blazor host of Routeables: Blazor Server, Blazor WebAssembly and MAUI Blazor Hybrid. It plugs the router into Razor components and keeps the browser address in step with it. Targets **.NET 10**.

```xml
<ItemGroup>
  <ProjectReference Include="..\Routeables\Routeables.Razor\Routeables.Razor.csproj" />
  <ProjectReference Include="..\Routeables\Routeables.Generated\Routeables.Generated.csproj"
                    OutputItemType="Analyzer" ReferenceOutputAssembly="false" />
</ItemGroup>
```

## Set up

```csharp
services.UseRouteablesServer<MainViewModel>(      // or UseRouteablesWebAssembly, UseRouteablesHybrid
    r => r.AddGeneratedRoutes());
```

Everything is scoped, so each Blazor Server circuit has its own history, and so has its menu. Nothing is discovered for you: the main view model and the routes are what you give.

## A view

```razor
@inherits RouteableFor<HomeView, HomeViewModel>

<h1>@ViewModel.Title</h1>
```

`RouteableFor<TView, TViewModel>` gives the typed `ViewModel` and the lifecycle hooks. A zone that shows a region is `<RouteableRegion Region="detail" />`.

## The layout

A layout inherits `MainLayoutBase`: it follows the current view, the lock and the theme. The ready-made `MainLayout` comes with the [dialogs and notifications](#dialogs-modals-and-notifications).

For a route declared with an explicit address (`/customers/{id:int}`), the browser address and the router stay in step: a route change updates the address, a typed address or the browser's back button route the router, and a refused change puts the address back.

## Dialogs, modals and notifications

Drawn with plain Razor (`Routeables.Razor.Services` namespace). `UseRouteablesServer` (and `UseRouteablesWebAssembly`, `UseRouteablesHybrid`) already registers them; call `UseRouteablesServices` yourself only to move the notifications or replace a service:

```csharp
services.UseRouteablesServer<MainViewModel>();
services.UseRouteablesServices(position: RouteableNotificationPosition.TopRight, maxDispayments: 3);   // optional
```

Everything is scoped. Each service has an optional factory to replace it; [`Reshapes.Razor.MudBlazor`](../Reshapes/README.md) passes the MudBlazor ones.

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
