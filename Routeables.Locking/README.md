# Routeables.Locking

Optional locking of the main entries: while a page blocks leaving (`CanLeaveAsync` returns `false`), the menu or tab bar that shows it cannot be used to go elsewhere. The contract (`ILockCoordinator`, `ILockCoordinatorFactory`) stays in [`Routeables.Abstraction`](../Routeables.Abstraction/README.md), so routing and the view-model bases work without this package; this one adds the broker that carries the messages and the coordinator that applies them.

Namespace `Routeables.Locking`. Targets **.NET 10** (`net10.0`), version `1.0.0-preview.1`. It references `Routeables.Abstraction`.

```csharp
services.AddLocking();   // the broker, and the coordinator that a MainViewModelBase asks the router for
```

| Type | Purpose |
| --- | --- |
| `AddLocking()` | Registers the broker (scoped, an application-provided one wins) and the lock coordinator factory |
| `IRouteableBrokerService` | Publish/subscribe between components: `Publish(message)`, `Subscribe(topic, callback)`; a subscriber that throws does not stop the others |
| `RouteableMessage` | A message of a topic; derive to add your own |
| `RouteableLockMessage` | `new RouteableLockMessage(isLocked, isAllLocked)` on topic `LockChanged`: locks or unlocks the main entries (all the main view models when `isAllLocked`) |

Without `AddLocking()` no host registers the broker, `router.CreateLockCoordinator(...)` returns `null` and nothing is locked.
