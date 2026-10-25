# Routeables.Generated

The source generators of Routeables. From NuGet they are inside the `Routeables` package, which every host package brings: there is nothing to add. From source, reference them as an analyzer from every project that declares routes:

```xml
<ItemGroup>
  <ProjectReference Include="..\Routeables\Routeables.Generated\Routeables.Generated.csproj"
                    OutputItemType="Analyzer" ReferenceOutputAssembly="false" />
</ItemGroup>
```

Mind the relative path: a wrong one makes `AddGeneratedRoutes` and `GoTo…Async` disappear.

## What it does

- **Registers routes.** A `[Routeable("/path")]` view model is paired with the view of the same name (`DogDetailViewModel` → `DogDetailView`), or with `[RouteableView(typeof(NoteViewModel))]` on `NoteView`. `services.AddGeneratedRoutes()` is generated in each project.
- **Types the routing methods.** `[RouteableViewModel(typeof(Caller))]` generates `GoTo{Name}Async(...)` on `Caller`, with the constructor parameters as arguments, checked at compile time.

Works the same on Razor, MAUI, Avalonia, Uno and WPF. The warnings and errors (`ROUT101` to `ROUT301`) are explained in [Diagnostics](../docs/guides/diagnostics.md).
