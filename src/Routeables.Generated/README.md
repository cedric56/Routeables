# Routeables.Generated

The source generators of Routeables. They are inside the `Routeables` package, which every host package brings: there is nothing to add. A project that only declares routes or view models (a class library, a module) adds `dotnet add package Routeables` to get them; without the package, `AddGeneratedRoutes` and `GoTo…Async` do not exist.

## What it does

- **Registers routes.** A `[Routeable("/path")]` view model is paired with the view of the same name (`DogDetailViewModel` → `DogDetailView`), or with `[RouteableView(typeof(NoteViewModel))]` on `NoteView`. `services.AddGeneratedRoutes()` is generated in each project.
- **Types the routing methods.** `[RouteableViewModel(typeof(Caller))]` generates `GoTo{Name}Async(...)` on `Caller`, with the constructor parameters as arguments, checked at compile time.

Works the same on Razor, MAUI, Avalonia, Uno and WPF. The warnings and errors (`ROUT101` to `ROUT301`) are explained in [Diagnostics](../docs/guides/diagnostics.md).
