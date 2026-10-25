# Routeables.Locking

Optional locking of the menu: while a page refuses to be left (`CanLeaveAsync` returns `false`), the main entries (menu, tabs) are disabled so the user cannot go elsewhere. The contracts stay in [`Routeables.Abstraction`](../Routeables.Abstraction/README.md); routing works without this package. Works on Razor, MAUI, Avalonia, Uno and WPF. Targets **.NET 10**.

```csharp
services.AddLocking();
```

Without `AddLocking()` nothing is locked.

## To know

- `IRouteableBrokerService`: publish and subscribe between components. A subscriber that throws does not stop the others.
- `RouteableLockMessage(isLocked, isAllLocked)`: locks or unlocks the main entries (every main view model when `isAllLocked`).

See [lifecycle and guards](../docs/guides/lifecycle.md).
