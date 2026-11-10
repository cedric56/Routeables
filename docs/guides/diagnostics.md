# Diagnostics reference

## Diagnostics reference

The typed-routing generator reports these diagnostics at the attribute or the offending member:

| Id | Severity | Meaning |
| --- | --- | --- |
| `ROUT101` | Error | Missing or invalid caller type (none given, static, or generic) |
| `ROUT102` | Error | Invalid destination (abstract, no public constructor, ambiguous constructors) or invalid `MethodName` |
| `ROUT103` | Error | No route found, or several `[Routeable]` and no explicit `Route` |
| `ROUT104` | Error | Invalid or unsupported template: placeholder without a matching constructor parameter, type mismatch, optional placeholder without a default value, optional path placeholder not last, catch-all `{**name}` not last, in the query or not a string, view-bound placeholder, unsupported type, malformed placeholder |
| `ROUT105` | Error | Unsupported parameter (`ref`, pointer, ref struct) or a name that clashes with a generated one (`caller`, `router`, `cancellationToken`, ...) |
| `ROUT106` | Warning | A parameter that may not receive a value is type-compatible with another one, so constructor arguments could be matched to the wrong parameter |
| `ROUT107` | Error | Two destinations would generate the same method on the same caller |

The registration generator (`AddGeneratedRoutes`) reports:

| Id | Severity | Meaning |
| --- | --- | --- |
| `ROUT201` | Warning | A route with parameters (`/dogs/{name}`) whose view or view model is `Scoped` or `Singleton`: the instance is cached by route and built once, so the later route changes do not pass their own arguments. Use `Transient` |
| `ROUT202` | Warning | A `[Routeable]` view model with no `[RouteableView]` whose name matches several views (`DogDetailViewModel` and two classes called `DogDetailView`): none is picked. Put `[RouteableView(typeof(DogDetailViewModel))]` on the one you mean. |
| `ROUT203` | Warning | Several views registered for the same view model, route and region: the last one registered replaces the others (two hosts' views in one assembly, or a view left behind after a copy). Keep one view for it, or give the others another route or region. |
| `ROUT204` | Warning | A `.razor` file declares `@attribute [RouteableView(...)]` in a project that calls its own `AddGeneratedRoutes()`: the generator cannot read `.razor` files, so nothing is registered. Put `[Routeable]` on the view model (the view is found by its name), move the view to a referenced project, or use `RegisterRoute`. A component library whose views are registered by the application that references it is not reported. |
| `ROUT205` | Info | A `[Routeable]` view model of this project, in a project that has views of its own, with no `[RouteableView]` and no class called after it (`DogDetailViewModel` and no `DogDetailView`): it is registered without a view. Rename the view or put `[RouteableView(typeof(DogDetailViewModel))]` on it. A route with no view is legitimate; switch the message off with `dotnet_diagnostic.ROUT205.severity = none` in `.editorconfig`, or `#pragma warning disable ROUT205`. A project with no view of its own (a library of view models) is not reported. |
| `ROUT301` | Warning | `IRouteableMenuViewModel` registered with `AddTransient` / `TryAddTransient`: each consumer would get another instance than the one the window shows. Use `RegisterMenuViewModel<T>()` (the host chooses Scoped or Singleton) |
| `ROUT401` | Warning | A `[Persist]` property that cannot be saved is skipped: static, indexer, no getter or no setter, `init`-only, getter or setter that is not public or internal, a generic class, a class that is private or protected, a type that cannot be saved (a ref struct, a pointer), or a `Converter` that does not implement `IPersistConverter<T>` for the property or has no public parameterless constructor. Fix the property or remove the attribute |
| `ROUT501` | Info | A `GoToAsync("/route")` whose string matches no route known to the build: the `[Routeable]` paths of the project and the projects it references, the `route` given to `RegisterRoute`, and the strings given as `route:` or assigned to a `Route` property (menu entries). Usually a typo. Not reported for a variable, a call with a `template`, a web address, or the qualifiers `-` and `-/route`; not at all when a route is registered from a string only known at run time. Silence it with `dotnet_diagnostic.ROUT501.severity = none` when the routes come from another place |
