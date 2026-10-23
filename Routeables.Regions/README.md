# Routeables.Regions

Optional helpers for windows made of several regions (a header, a footer, a menu on the side and a content). The regions themselves, the keyed routers registered with `AddRegion` and resolved with `GetRouter`, are part of routing; this package only adds what drives them. A project that does not use it carries none of it.

Namespace `Routeables.Regions`. Targets **.NET 10** (`net10.0`), version `1.0.0-preview.1`. It references `Routeables.Abstraction` and `Routeables.Routing`.

| Type | Purpose |
| --- | --- |
| `RegionBinding` | Connects a region router to a control: the control shows the current view of the router. The `RouteableRegion` control of each host uses it |
| `RegionFollower` | Makes other regions follow one: each change of the source queues an update, run one after the other, never on the route change |
| `RegionLayout` | A class that drives several regions together; derive from it and name the regions |
| `AddRegionLayout<TLayout>(...)` | Registers the regions and the layout, scoped |

The [main README](../README.md#regions) describes each of them with examples. The hosts reference this package, so an application that uses `RouteableRegion` has it already.
