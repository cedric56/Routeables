# Diagnostics reference

## Diagnostics reference

The typed-routing generator reports these diagnostics at the attribute or the offending member:

| Id | Severity | Meaning |
| --- | --- | --- |
| `ROUT101` | Error | Missing or invalid caller type (none given, static, or generic) |
| `ROUT102` | Error | Invalid destination (abstract, no public constructor, ambiguous constructors) or invalid `MethodName` |
| `ROUT103` | Error | No route found, or several `[Routeable]` and no explicit `Route` |
| `ROUT104` | Error | Invalid or unsupported template: placeholder without a matching constructor parameter, type mismatch, optional placeholder without a default value, optional path placeholder not last, view-bound placeholder, unsupported type, malformed placeholder |
| `ROUT105` | Error | Unsupported parameter (`ref`, pointer, ref struct) or a name that clashes with a generated one (`caller`, `router`, `cancellationToken`, ...) |
| `ROUT106` | Warning | A parameter that may not receive a value is type-compatible with another one, so constructor arguments could be matched to the wrong parameter |
| `ROUT107` | Error | Two destinations would generate the same method on the same caller |

The registration generator (`AddGeneratedRoutes`) reports:

| Id | Severity | Meaning |
| --- | --- | --- |
| `ROUT201` | Warning | A route with parameters (`/dogs/{name}`) whose view or view model is `Scoped` or `Singleton`: the instance is cached by route and built once, so the later route changes do not pass their own arguments. Use `Transient` |
| `ROUT202` | Warning | A `[Routeable]` view model with no `[RouteableView]` whose name matches several views (`OrderViewModel` and two classes called `OrderView`): none is picked. Put `[RouteableView(typeof(OrderViewModel))]` on the one you mean. |
| `ROUT203` | Warning | Several views registered for the same view model, route and region: the last one registered replaces the others (two hosts' views in one assembly, or a view left behind after a copy). Keep one view for it, or give the others another route or region. |
| `ROUT301` | Warning | `IMainRouteables` registered with `AddTransient` / `TryAddTransient`: each consumer would get another instance than the one the window shows. Use `RegisterMainRouteables<T>()` (the host chooses Scoped or Singleton) |
