# Routeables.Generated

Roslyn source generators of Routeables. Reference it as an analyzer, from every project that contains the attributes it processes:

```xml
<ItemGroup>
  <ProjectReference Include="..\Routeables\Routeables.Generated\Routeables.Generated.csproj"
                    OutputItemType="Analyzer" ReferenceOutputAssembly="false" />
</ItemGroup>
```

It does two things:

- **Route registration.** A `[Routeable("/path")]` view model is paired with the view of the same name (`OrderViewModel` => `OrderView`), or with the view that carries `[RouteableView(typeof(OrderViewModel))]`. `services.AddGeneratedRoutes()` (generated as an `internal` extension in each project) registers them all.
- **Typed routes.** `[RouteableViewModel(typeof(Caller))]` on a view model generates `GoTo{Name}Async` on `Caller`, with the constructor parameters of the view model as arguments, checked at compile time.

The diagnostics (`ROUT101` to `ROUT301`) are listed in the [main README](../README.md#diagnostics-reference).
