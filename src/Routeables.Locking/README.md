# Routeables.Locking

Optional locking of the menu: while a page refuses to be left (`CanLeaveAsync` returns `false`), the main entries (menu, tabs) are disabled so the user cannot go elsewhere. The contracts stay in [`Routeables.Abstraction`](../Routeables.Abstraction/README.md); routing works without this package. Works on Razor, MAUI, Avalonia, Uno and WPF. Targets **.NET 10**.

```csharp
services.AddLocking();
```

Without `AddLocking()` nothing is locked.

## Lock the menu while a form is edited

A view model asks for `IRouteableFormLock` (contract in `Routeables.Abstraction`, registered by `AddLocking()`) and keeps the token for as long as the form holds work:

```csharp
using Routeables.Abstraction.Locking;

using var editing = formLock.Lock();   // the menu is locked until editing is disposed
```

Several forms can lock at once: the menu is unlocked when the last token is disposed. `IsLocked` tells whether one is held. To refuse a route change made by code too, implement `CanLeaveAsync`.

## To know

- `IRouteableLockBroker`: publish and subscribe between components. A subscriber that throws does not stop the others.
- `RouteableLockMessage(isLocked, isAllLocked)`: locks or unlocks the main entries (every main view model when `isAllLocked`).

See [lifecycle and guards](../../docs/guides/lifecycle.md).
