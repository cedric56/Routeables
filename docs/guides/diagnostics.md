# Diagnostics reference

## Diagnostics reference

The typed-routing generator reports these diagnostics at the attribute or the offending member:

| Id | Severity | Meaning |
| --- | --- | --- |
| <a id="rout101"></a>`ROUT101` | Error | Missing or invalid caller type (none given, static, or generic) |
| <a id="rout102"></a>`ROUT102` | Error | Invalid destination (abstract, no public constructor, ambiguous constructors) or invalid `MethodName` |
| <a id="rout103"></a>`ROUT103` | Error | No route found, or several `[Routeable]` and no explicit `Route` |
| <a id="rout104"></a>`ROUT104` | Error | Invalid or unsupported template: placeholder without a matching constructor parameter, type mismatch, optional placeholder without a default value, optional path placeholder not last, catch-all `{**name}` not last, in the query or not a string, view-bound placeholder, unsupported type, malformed placeholder |
| <a id="rout105"></a>`ROUT105` | Error | Unsupported parameter (`ref`, pointer, ref struct) or a name that clashes with a generated one (`caller`, `router`, `cancellationToken`, ...) |
| <a id="rout106"></a>`ROUT106` | Warning | A parameter that may not receive a value is type-compatible with another one, so constructor arguments could be matched to the wrong parameter |
| <a id="rout107"></a>`ROUT107` | Error | Two destinations would generate the same method on the same caller |

The registration generator (`AddGeneratedRoutes`) reports:

| Id | Severity | Meaning |
| --- | --- | --- |
| <a id="rout201"></a>`ROUT201` | Warning | A route with parameters (`/dogs/{name}`) whose view or view model is `Scoped` or `Singleton`: the instance is cached by route and built once, so the later route changes do not pass their own arguments. Use `Transient` |
| <a id="rout202"></a>`ROUT202` | Warning | A `[Routeable]` view model with no `[RouteableView]` whose name matches several views (`DogDetailViewModel` and two classes called `DogDetailView`): none is picked. Put `[RouteableView(typeof(DogDetailViewModel))]` on the one you mean. |
| <a id="rout203"></a>`ROUT203` | Warning | Several views registered for the same view model, route and region: the last one registered replaces the others (two hosts' views in one assembly, or a view left behind after a copy). Keep one view for it, or give the others another route or region. |
| <a id="rout204"></a>`ROUT204` | Warning | A `.razor` file declares `@attribute [RouteableView(...)]` in a project that calls its own `AddGeneratedRoutes()`: the generator cannot read `.razor` files, so nothing is registered. Put `[Routeable]` on the view model (the view is found by its name), move the view to a referenced project, or use `RegisterRoute`. A component library whose views are registered by the application that references it is not reported. |
| <a id="rout205"></a>`ROUT205` | Info | A `[Routeable]` view model of this project, in a project that has views of its own, with no `[RouteableView]` and no class called after it (`DogDetailViewModel` and no `DogDetailView`): it is registered without a view. Rename the view or put `[RouteableView(typeof(DogDetailViewModel))]` on it. A route with no view is legitimate; switch the message off with `dotnet_diagnostic.ROUT205.severity = none` in `.editorconfig`, or `#pragma warning disable ROUT205`. A project with no view of its own (a library of view models) is not reported. |
| <a id="rout206"></a>`ROUT206` | Warning | Two `[Routeable]` view models (in one region) declare the same route: the table of routes keeps the first one it is given and the second is never reached (at run time it is reported as a route that is not registered). The case and a trailing slash do not tell two routes apart. Give each its own route. |
| <a id="rout207"></a>`ROUT207` | Warning | `[Routeable]` on a type the generated code cannot name: an open generic class (`OrderViewModel<T>`), a type declared `file`, or one this project cannot reach (private nested, internal in another assembly). It is left out of the registration (it would write code that does not compile). Register a closed type, or make the type internal or public. |
| <a id="rout301"></a>`ROUT301` | Warning | `IRouteableMenuViewModel` registered with `AddTransient` / `TryAddTransient`: each consumer would get another instance than the one the window shows. Use `RegisterMenuViewModel<T>()` (the host chooses Scoped or Singleton) |
| <a id="rout401"></a>`ROUT401` | Warning | A `[Persist]` property that cannot be saved is skipped: static, indexer, no getter or no setter, `init`-only, getter or setter that is not public or internal, a generic class, a class that is private or protected, a type that cannot be saved (a ref struct, a pointer), or a `Converter` that does not implement `IPersistConverter<T>` for the property or has no public parameterless constructor. Fix the property or remove the attribute |
| <a id="rout501"></a>`ROUT501` | Info | A `GoToAsync("/route")` whose string matches no route known to the build: the `[Routeable]` paths of the project and the projects it references, the `route` given to `RegisterRoute`, and the strings given as `route:` or assigned to a `Route` property (menu entries). Usually a typo. Not reported for a variable, a call with a `template`, a web address, or the qualifiers `-` and `-/route`; not at all when a route is registered from a string only known at run time. Silence it with `dotnet_diagnostic.ROUT501.severity = none` when the routes come from another place |
