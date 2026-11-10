# Routeables.Generated

The source generators of Routeables. They are inside the `Routeables` package, which every host package brings: there is nothing to add. A project that only declares routes or view models (a class library, a module) adds `dotnet add package Routeables` to get them; without the package, `AddGeneratedRoutes` and `GoTo…Async` do not exist.

## What it does

- **Registers routes.** A `[Routeable("/path")]` view model is paired with the view of the same name (`DogDetailViewModel` → `DogDetailView`), or with `[RouteableView(typeof(NoteViewModel))]` on `NoteView`. `services.AddGeneratedRoutes()` is generated in each project.
- **Keeps the values of a page.** `[Persist]` on a view-model property saves and restores it with the history snapshots (no reflection); see [System integration](../../docs/guides/system-integration.md).
- **Types the routing methods.** `[RouteableViewModel(typeof(Caller))]` generates `GoTo{Name}Async(...)` on `Caller`, with the constructor parameters as arguments, checked at compile time.

Works the same on Blazor, ASP.NET Core, MAUI, Avalonia, Uno, WinUI 3 and WPF. The warnings and errors (`ROUT101` to `ROUT401`) are explained in [Diagnostics](../../docs/guides/diagnostics.md).
