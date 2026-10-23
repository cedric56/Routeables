# Routeables.Abstraction

`Routeables.Abstraction` holds the contracts and shared types of Routeables. View models and views depend on these interfaces instead of on
an API specific to Blazor, .NET MAUI or Avalonia, so the same routing code runs on all three.

> It defines contracts and a few small shared types. The router itself is in [`Routeables.Routing`](../Routeables.Routing/README.md) and the
> platform services are in the host projects.

## Requirements

Targets **.NET 10** (`net10.0`), version `1.0.0-preview.1`, nullable reference types on. Its only package is
`Microsoft.Extensions.DependencyInjection.Abstractions`.

From a project of this repository:

```xml
<ItemGroup>
  <ProjectReference Include="..\Routeables.Abstraction\Routeables.Abstraction.csproj" />
</ItemGroup>
```

```bash
dotnet build Routeables.Abstraction/Routeables.Abstraction.csproj
```

## What is in it

All types are in the `Routeables.Abstraction` namespace.

| Area | Main types |
| --- | --- |
| Router | `IRouter`, `IRouterHistory`, `IRouterEntry`, `RouteableResult`, `RouteableResult<TResult>`, `RouteableRoutingEventArgs`, `RouteableRoutedEventArgs` |
| Routes and attributes | `[Routeable]`, `[RouteableView]`, `[RouteableViewModel]`, `RouteableTable` |
| Lifecycle | `IRouteable` (guard and routing callbacks), `IInitializeAsync` and `IInitializeAsync<TData>`, `IReturnsResult<T>` |
| Arguments and injection | `IRouteableArgs`, `RouteableArgument`, `RouteablesParameters`, `IInjection`, `[InjectProperty]`, `[FromContainer]`, `ConstructorArguments` |
| Views | `IRouteableFor` (a view with a `Context`), `IRouteableEntry` (a menu entry), `IMainRouteables`, `IRouteableModalFor<T, TResult>`, `ICloseableViewModel<TResult>` |
| Regions and segments | not here: [`Routeables.Regions`](../Routeables.Regions/README.md) and [`Routeables.Segments`](../Routeables.Segments/README.md) are optional |
| Lock | `ILockCoordinator`, `ILockCoordinatorFactory`: the contract of the locking of the main entries; the broker, the messages and the coordinator are in [`Routeables.Locking`](../Routeables.Locking/README.md) |
| Errors | `IRouteableErrorSink`, `RouteableErrors` |
| Registration helpers | `RegisterRoute<TView, TViewModel>`, `RegisterMainRouteables<T>`, `RegisterLockCoordinator<T>` (the factory of a coordinator), `GetRegion`, `GetRouter`, `GoToAsync` on `IServiceProvider` |

The dialog, notification and theme services are not here: they are in [`Routeables.Services`](../Routeables.Services/README.md), so a project that only routes does not carry them.

## Navigating from a view model

A view model depends only on `IRouter`:

```csharp
using Routeables.Abstraction;

public sealed class HomeViewModel(IRouter router)
{
    public async Task OpenOrderAsync(int orderId)
    {
        RouteableResult result = await router.GoToAsync($"/orders/{orderId}");

        if (result.Cancelled)
            return;   // for example, the current page vetoed leaving

        if (!result.Success)
            throw new InvalidOperationException(result.Exception?.Message ?? "Navigation failed.");
    }
}
```

`IRouter` offers `GoToAsync(url)` (the usual call: the address, with its parameters), `GoToRouteAsync` (view model, or route plus template), `GoBackAsync`, `ForwardAsync`, `PopAsync`,
`GoBackToRootAsync`, `MoveToAsync`, `EraseHistoryAsync`, the `Routing` / `Routed` events and the state of the history. A route change that
cannot happen (an unknown address) returns a failed `RouteableResult` and does not throw. The
[typed route methods](../README.md#typed-routing-methods-source-generator) are generated from `[RouteableViewModel]`, so a call
cannot name a wrong route or argument.

## Lifecycle

Every member of `IRouteable` has a default, so implement only what you need:

```csharp
public sealed class OrderViewModel : IRouteable, IInitializeAsync
{
    public Task<bool> CanLeaveAsync(CancellationToken ct = default) => Task.FromResult(!HasUnsavedChanges);
    public Task OnEnteringAsync(IRouteableArgs context, CancellationToken ct = default) => Task.CompletedTask;
    public Task OnLeavingAsync(IRouteableArgs context, CancellationToken ct = default) => Task.CompletedTask;
    public Task InitializeAsync(IRouteableArgs args, CancellationToken ct = default) => Task.CompletedTask;
}
```

Clean-up uses `IDisposable` / `IAsyncDisposable`: the router disposes what leaves the history. `IInitializeAsync<TData>` receives one typed
value (`GoToRouteAsync<TViewModel, TData>`). A page that gives a value back implements `IReturnsResult<T>` and is opened with
`GoToForResultAsync`. The order of the calls is described in the [main README](../README.md#lifecycle-and-guards).

## Arguments and routes

`IRouteableArgs` carries three groups of values: for the view model (`ForViewModel`, matched to constructor parameters by type), for the
view (`ForView`) and neutral ones (`ForValues`, read by the lifecycle hooks and never injected). `RouteableArgs.Create()` in
`Routeables.Routing` builds one.

Routes are matched against templates: `{id}`, `{id:int}` (also `long`, `bool`, `guid`, `decimal`, `double`), `{id?}` for an optional
segment, `?{tab}` for a query value, and `{name::}` for a value set on a property that carries a platform attribute. Values in the address
that the template does not declare stay neutral: they never reach a constructor.

## Related projects

- [`Routeables.Routing`](../Routeables.Routing/README.md): the router, its history and the registration of routers.
- [`Routeables.Core`](../Routeables.Core/README.md): view-model bases and runtime information.
- Hosts: [Razor](../Routeables.Razor/README.md), [MAUI](../Routeables.Maui/README.md), [Avalonia](../Routeables.Avalonia/README.md).
