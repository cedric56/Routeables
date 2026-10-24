# Native AOT and trimming

AOT and trimming are part of the framework's contract: no feature depends on a type that the trimmer could remove.

- Routes are discovered at build time by the source generator (`AddGeneratedRoutes()`), or declared explicitly with `RegisterRoute<TView, TViewModel>()`. Nothing scans assemblies at run time, unless you ask for it by loading a module from a file (`IRouteableModules.LoadFrom`, see [Modules](routes.md#modules)).
- Generic registration methods carry `[DynamicallyAccessedMembers]`, so the constructors and injectable properties of the types you register are kept.
- Property injection (`[Parameter]`, `[Inject]`, `[RouteableParameter]`, ...) uses a description produced by the generator, or by the annotated generic registration. A type that was neither described nor registered with its own type fails with a clear message instead of falling back on reflection.
- JSON route parameters use `JsonTypeInfo`, so they work with a `JsonSerializerContext`.

`Routeables.AotSmoke` is published with `PublishAot` and run in CI, and the Blazor WebAssembly sample is published trimmed; a trim or AOT warning raised by Routeables fails the build.
