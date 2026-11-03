# Routeables.Segments

Optional routing by segments, as in Prism: `Orders?status=open/Detail?id=5` (in the samples, `/Bears?SearchText=polar`) runs the segments one after the other, each on its router with its own parameters. Plain `IRouter` use does not need it, and does not read segments: `router.GoToAsync("/dogs/Eurohound")` is one route matched against the template `/dogs/{name}`, whereas the same string given to `CoordinateAsync` is read as an absolute address of two segments, `dogs` and `Eurohound`. A path goes to the router, segments go to the coordinator. Targets **.NET 10**.

```csharp
services.RegisterRouting();
services.AddRouteCoordinator();
services.RegisterSegment<BearsViewModel>("Bears", "/bears");   // Samples/Bases/XAnimals.ViewModels/Extensions.cs

await coordinator.CoordinateAsync("/Bears?SearchText=polar");   // MainViewModel.cs of the samples
```

| Syntax | Meaning |
| --- | --- |
| `Name?k=v` | Pushes the segment registered under `Name` |
| `region:Name` | Hands the segment to the router of that region |
| `..` | Goes back one entry |
| leading `/` | Absolute: clears the history of the routers involved first |

`AddRouteCoordinator()` takes the lifetime of the host, like the main view model; pass one only to override it. Types: `IRouteCoordinator`, `RouteCoordination` (parsed, or fluent with `RouteCoordination.Create()`), `RouteSegment`.
