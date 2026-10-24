# Routeables.Segments

Optional routing by segments, as in Prism: `Orders?status=open/Detail?id=5` runs the segments one after the other, each on its router with its own parameters. Plain `IRouter` use does not need it. Targets **.NET 10**.

```csharp
services.RegisterRouting();
services.AddRouteCoordinator();
services.RegisterSegment<OrdersViewModel>("Orders");

await coordinator.CoordinateAsync("Orders?status=open/Detail?id=5");
```

| Syntax | Meaning |
| --- | --- |
| `Name?k=v` | Pushes the segment registered under `Name` |
| `region:Name` | Hands the segment to the router of that region |
| `..` | Goes back one entry |
| leading `/` | Absolute: clears the history of the routers involved first |

`AddRouteCoordinator(lifetime)` takes the lifetime of the routers it drives (`Scoped` on Blazor Server). Types: `IRouteCoordinator`, `RouteCoordination` (parsed, or fluent with `RouteCoordination.Create()`), `RouteSegment`.
