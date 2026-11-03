# Routeables.Regions

Optional helpers for a window made of several regions (header, side menu, content), each with its own router and history. Works on Razor, MAUI, Avalonia, Uno and WPF; the hosts reference it, so an application that uses `RouteableRegion` already has it. Targets **.NET 10**.

```csharp
services.AddRegionLayout<AppLayout>(/* region names */);   // scoped
```

| Type | Purpose |
| --- | --- |
| `RegionBinding` | Shows the current view of a region router in a control (used by `RouteableRegion`) |
| `RegionFollower` | Makes other regions follow one: each change of the source queues an update |
| `RegionLayout` | Derive from it to drive several regions together |

Regions themselves are registered with `AddRegion("name")` and resolved with `GetRouter("name")`. See the [Regions guide](../docs/guides/regions.md).
