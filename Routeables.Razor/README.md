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
services.UseRouteables(Runtime.BlazorServer);        // or BlazorWebAssembly, MauiHybrid
services.AddGeneratedRoutes();
services.RegisterMainRouteables<MainViewModel>(ServiceLifetime.Scoped);   // Scoped: one per circuit
```

Everything is scoped, so each Blazor Server circuit has its own history. Nothing is discovered for you: the routes and the main view model are the two lines above.

## A view

```razor
@inherits RouteableFor<HomeView, HomeViewModel>

<h1>@ViewModel.Title</h1>
```

`RouteableFor<TView, TViewModel>` gives the typed `ViewModel` and the lifecycle hooks. A zone that shows a region is `<RouteableRegion Region="detail" />`.

## The layout

A layout inherits `MainLayoutBase`: it follows the current view, the lock and the theme. The ready-made `MainLayout` is in [`Routeables.Razor.Services`](../Routeables.Razor.Services/README.md) with the dialogs and notifications.

For a route declared with an explicit address (`/customers/{id:int}`), the browser address and the router stay in step: a route change updates the address, a typed address or the browser's back button route the router, and a refused change puts the address back.

## Good to know

- A component must be safe to dispose twice: the router disposes pages that leave its history, and Blazor disposes what it renders.
- A `Transient` service that is `IDisposable` lives until the circuit ends: register it `Scoped` or `Singleton`. See [lifecycle](../docs/guides/lifecycle.md).
- MudBlazor flavour: [`Reshapes`](../Reshapes/README.md).

## Next

[`Routeables.Razor.Services`](../Routeables.Razor.Services/README.md) (dialogs, modals, notifications), [Hosts guide](../docs/guides/hosts.md), samples `Samples/XAnimals.Razor` and the Blazor Server and WebAssembly heads.
