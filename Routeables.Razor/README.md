# Routeables.Razor

`Routeables.Razor` is the Blazor host of Routeables (Blazor Server, Blazor WebAssembly and MAUI Blazor Hybrid). It connects the router to
Razor components and keeps the browser address in step with the router. The dialog, modal and notification services are optional, in [`Routeables.Razor.Services`](../Routeables.Razor.Services/README.md).

## Requirements

Targets **.NET 10** (`net10.0`), version `1.0.0`. It references `Microsoft.AspNetCore.Components`, `Microsoft.AspNetCore.Components.Web`,
`Routeables.Abstraction` and `Routeables.Core`. It has no dependency on a component library: the layouts and hosts are plain Razor. A
MudBlazor flavour is a separate project, see [`Reshapes.Razor.MudBlazor`](../Reshapes/README.md).

```xml
<ItemGroup>
  <ProjectReference Include="..\Routeables.Razor\Routeables.Razor.csproj" />
</ItemGroup>
```

```bash
dotnet build Routeables.Razor/Routeables.Razor.csproj
```

## Registering Routeables

Call `UseRouteables` on the service collection and give the runtime:

```csharp
using Routeables.Abstraction;
using Routeables.Core;
using Routeables.Razor;

services.UseRouteables(Runtime.BlazorServer);
```

Use `Runtime.BlazorWebAssembly` for WebAssembly and `Runtime.MauiHybrid` for a MAUI Blazor Hybrid app. The call registers, all **scoped** (one
set per Blazor Server circuit):

- the router, with its own history per circuit, the arguments factory;
- property injection: `[Inject]` properties come from the container, `[Parameter]` properties take routing values and never fall back to the container;
- `RouteableUrlSync`, the theme service and the environment information.

`onCircuitProviderChanged` is registered as a `Func<IServiceProvider, Task>` for host-specific use. Register your routes (`services.AddGeneratedRoutes()` or
`RegisterRoute`) and your main view model (`RegisterMainRouteables<T>(ServiceLifetime.Scoped)`) yourself: nothing is discovered automatically.

The dialogs, content dialogs, task dialogs, modals and notifications are **optional**: add `services.UseRouteablesServices()` from
[`Routeables.Razor.Services`](../Routeables.Razor.Services/README.md), which also brings `MainLayout` and the hosts that draw them.

## Layout and address

A layout inherits `MainLayoutBase`, which holds what every layout needs: the route change state, the theme, the refresh when the view or the lock changes,
and the first initialization. `MainLayout`, the default one, is in `Routeables.Razor.Services` with the hosts of the dialogs and notifications.

`RouteableUrlSync` keeps the browser address and the router in step for the routes registered with an explicit route (`/customers/{id:int}`):
a route change updates the address (a push adds a browser history entry, back and forward replace it), a typed address or the browser's back
and forward route the router, and a refused route change (a guard) puts the address back. `MainLayoutBase` starts it; call `StartAsync` yourself
with another layout.

## Routed components

`RouteableFor<TView, TViewModel>` connects a Blazor component to a view model: it renders `TView`, exposes the typed `ViewModel`, passes it through
the `Context` parameter and implements `IRouteable`, `IInitializeAsync` and `IDisposable`. `RouteableContentFor<T>` renders a component with child
content, `RouteableCenteredFor<TView, TViewModel>` centers it, and `RouteableModalFor<TView, TViewModel, TResult>` (in `Routeables.Razor.Services`) shows it in a modal and returns a typed
result when the view model closes. `RouteableForm` is a split layout for a main view model: a list of its entries (locked ones are disabled), a header, the current view and a row of footer buttons.

```razor
@using Routeables.Razor
@inherits RouteableFor<HomeView, HomeViewModel>
```

A region of the page is a `RouteableRegion`; it shows whatever the router registered under that name displays:

```razor
<RouteableRegion Region="detail" />
```

## Notes

- A Razor component must be safe to dispose twice: the router disposes the pages that leave its history, and Blazor disposes the components it renders.
- A `Transient` service that implements `IDisposable` stays in the circuit's scope until it ends: register it `Scoped` or `Singleton`
  (see [Cleanup](../README.md#cleanup)).

## Related projects

- [`Routeables.Abstraction`](../Routeables.Abstraction/README.md): contracts and routing arguments.
- [`Routeables.Routing`](../Routeables.Routing/README.md): router lifetimes and regions.
- [`Routeables.Core`](../Routeables.Core/README.md): view-model bases.
- [`Routeables.Razor.Services`](../Routeables.Razor.Services/README.md): dialogs, modals, notifications and the default layout.
- Samples: `Samples/XAnimals.Razor`, `XAnimals.BlazorWebAssembly`, `Xanimals.BlazorApp`.
