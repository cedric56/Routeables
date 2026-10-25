# Routeables.Routing

The router of Routeables: its history, the one-at-a-time execution of route changes, the lifecycle calls, the parsing of routes and the registration helpers the hosts use. It implements the contracts of [`Routeables.Abstraction`](../Routeables.Abstraction/README.md). Targets **.NET 10**.

The router class is internal: an application resolves `IRouter` from the container.

```xml
<ItemGroup>
  <ProjectReference Include="..\Routeables\Routeables.Routing\Routeables.Routing.csproj" />
</ItemGroup>
```

A host's setup call (`UseRouteables`) registers the router already; call `RegisterRouting` yourself only for a host without one (tests, services).

## Register

```csharp
using Routeables.Routing;

services.RegisterRouting();                         // one history for the application
services.RegisterRouting(ServiceLifetime.Scoped);   // Blazor Server: one history per circuit

// by hand (RazorBirdsModule.cs of the samples), or with AddGeneratedRoutes()
services.RegisterRoute<MoreBirdsPage, MoreBirdsViewModel>(ServiceLifetime.Transient, ServiceLifetime.Transient, MoreBirdsModule.Route);
```

A `Transient` router is refused: every resolve would get its own history. For several independent histories use regions: `services.AddRegion("adopt")`, resolved with `provider.GetRouter("adopt")` ([regions](../docs/guides/regions.md)).

## How it behaves

- Calls on one router run one after the other, even when several are requested at once.
- Guards and lifecycle callbacks run before the history changes. A refused or failed route change commits nothing and the previous page is restored.
- The history keeps 20 entries (`RouterHistory.MaxSize`); entries that leave it are disposed.
- `Routing` and `Routed` are raised for each route change. A handler that throws never fails the route change: the error goes to your `IRouteableErrorSink`, or to `Trace`.
- Measured with BenchmarkDotNet: about 4 to 7.5 µs and 7.5 to 12 KB per route change ([benchmarks](../docs/benchmarks.md)).

Related: [`Routeables.Abstraction`](../Routeables.Abstraction/README.md), [`Routeables.Core`](../Routeables.Core/README.md), and the hosts ([Razor](../Routeables.Razor/README.md), [MAUI](../Routeables.Maui/README.md), [Avalonia](../Routeables.Avalonia/README.md), [Uno](../Routeables.Uno/README.md), [WPF](../Routeables.Wpf/README.md)).
