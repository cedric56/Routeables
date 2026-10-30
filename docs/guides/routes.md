# Routes and templates

## Routes and templates

A **route** is the concrete string you go to (`/dogs/Eurohound`). A **template** is the registered pattern that is matched against it (`/dogs/{name}`). The router parses the route against the template and hands the parsed values to the destination.

| Placeholder | Meaning |
| --- | --- |
| `{name}` | Required segment, string (`/dogs/{name}`) |
| `{id:int}` | Required segment with a type: `string`, `int`, `long`, `bool`, `guid`, `decimal`, `double` |
| `{name?}` | Optional segment |
| `{panda::}` | Segment bound to the **view** instead of the view model (`/pandas/{panda::}`, see [Arguments](arguments.md)) |
| `?{tab}` / `?{tab?}` | Query-string value, required / optional |

A type that is not in the table is a registered one (`RouteParser.RegisterSerializer`, trim-safe JSON). A template with any other type never matches, and the reason says so (`the constraint "foo" of {id} is unknown`): it does not throw while the router looks for a route.

### Which route wins

When several templates match a route, the **most specific** one wins, whatever the order they were registered in. Segments are compared from the left: a literal (`new`) beats a typed placeholder (`{id:int}`, a registered type), which beats an untyped one (`{id}`), which beats an optional one (`{tab?}`). With `/bears/{id}`, `/bears/{id:int}` and `/bears/new` registered, `/bears/new` opens the third, `/bears/12` the second and `/bears/polar` the first. Templates that are equally specific keep the order of registration. There is no catch-all placeholder (`{**path}`).

Details of the matching:

- The route is split into segments before it is decoded, so `files/a%2Fb` is the one segment `a/b`; an escape sequence that is not valid (`100%zz`) is kept as it is.
- Literal segments are compared without regard to case.
- An optional placeholder (`{tab?}`) is for the last segment of the path; one in the middle does not skip a segment.
- When a query name is repeated (`?tag=a&tag=b`), the last value wins.

Query values that are not declared in the template are kept as neutral values (`IRouteableArgs.Values`, read by the lifecycle hooks): they are never given to the constructor or injected into a property, so a link with `?utm_source=mail` still opens the page, and nobody can fill a constructor parameter from the address.

`[Routeable]` takes a path, an optional region and a service lifetime. It can be repeated on the same class to give a view model several routes:

```csharp
// DogDetailViewModel.cs of the XAnimals sample, with a second route
[Routeable("/dogs/{name}")]
[Routeable("/d/{name}")]
public class DogDetailViewModel(IRouter router, string name, Action? back = null)
    : AnimalDetailViewModel(router, DogData.Dogs.FirstOrDefault(d => d.Name == name), back);
```

### Main view model per host

The menu or tab bar (`IMainViewModel`) is registered by `UseRouteables<MainViewModel>(...)` of the host (or by `services.RegisterMainViewModel<MainViewModel>()`, which does the same). Its lifetime is the host's: `Scoped` on Blazor (one per circuit), `Singleton` on Avalonia, MAUI, Uno and WPF. Pass a lifetime only to override it. A `Transient` one is refused with an `ArgumentException`, because each consumer would get another instance than the one the window shows. At start the host calls `IMainViewModel.SelectFirst()`, which selects the first entry when none is selected, the same way on the five hosts.

### Modules

A feature of the application (the birds of the samples, for example) can live in a class library of its own. There is no module catalog. A module is an extension method that the application calls at start, so the compiler, the trimmer and Native AOT see every module; one that is not known at start is loaded while the application runs ([below](#loading-a-module-while-the-application-runs)).

```csharp
// Samples/Modules/XAnimals.Birds/BirdsModule.cs
public static class BirdsModule
{
    public const string Route = "/birds";

    public static IServiceCollection AddBirdsModule(this IServiceCollection services)
        => services.AddMenuEntries(() =>
           [
               new RouteableEntry<BirdsViewModel>("Birds", AnimalIcons.Bird, route: Route, template: Route),
           ], 10);
}

// Samples/Bases/XAnimals.ViewModels/Extensions.cs, in the application
services.AddBirdsModule();
```

`AddGeneratedRoutes()` is `internal` in each project, which is why a library that holds views wraps it in a public method (`AddMauiBirdsModule()` of `XAnimals.Maui.Birds`: `BirdsModule.AddBirdsModule(services.AddGeneratedRoutes())`). `AddMenuEntries` puts the entries of the module in the menu, after those of the main view model, in the order the modules were added. The function runs once for each main view model: build new entries in it, so that a lock or a selection is not shared between the circuits of a Blazor Server application. Adding the same module twice adds its entries once. A module that needs another one calls its method itself: the entries of the module it needs come first, and a module called by two others is added once. Register its services with `TryAdd...` so that a second call changes nothing. By default the entries go after the ones of the main view model; `AddMenuEntries(entries, index: 0)` puts them first, `index: 2` after the second entry, and a value past the end puts them last. The index counts the entries the menu has when the module is added to it, and a module loaded while the application runs is inserted the same way.

The Birds module of the samples is the other shape, a module that serves several hosts (Razor, Avalonia, MAUI, Uno, WPF) and can be shipped. Its view models, its data and its menu entry are in one library, `XAnimals.Birds`; each platform has a project of its own, `XAnimals.Razor.Birds`, `XAnimals.Avalonia.Birds`, `XAnimals.Maui.Birds`, `XAnimals.Uno.Birds` and `XAnimals.Wpf.Birds`, that references it and holds the views of that platform, since a view belongs to the UI framework of its host. The views draw with controls (`AnimalList`, `AnimalDetail`) that the application also uses, so they are in a library of their own per platform (`XAnimals.Avalonia.Controls`, `XAnimals.Maui.Controls`, `XAnimals.Uno.Controls`, `XAnimals.Wpf.Controls`; the Razor ones are in `XAnimals.Razor`), which both the application and the module reference. The application references the project of its platform, nothing else: its `AddGeneratedRoutes()` finds the views in the referenced assembly, and `XAnimals.ViewModels` calls `AddBirdsModule()`. Each platform project also has an entry method marked `[RouteableModule("Birds")]` (`AddAvaloniaBirdsModule()`...) so the same module can be [loaded while the application runs](#loading-a-module-while-the-application-runs): `modules.LoadFrom("XAnimals.Avalonia.Birds.dll")`. Keep one view per host for a route: two views registered for the same view model and route replace one another, and the last registered wins (ROUT203 reports it). The Birds page also shows how a module is loaded while the application runs: its **Load Module** button calls `IRouteableModules.Load` (through `BirdsViewModel.LoadMoreBirds`) with the method of its platform that registers the page of a second module, More birds (`AddAvaloniaMoreBirdsModule()`...), and the entry "More birds" appears in the menu without restarting. That page has no `[RouteableView]`, so nothing registers it at start.

#### Loading a module while the application runs

`IRouteableModules`, registered with `RegisterMainViewModel`, loads a module when the application asks for it, as Prism does with `OnDemand`. The routes of the module can be reached at once (by the router and from an address), its entries are added to the menu of every main view model alive and of the ones built later, and its services are available to its own views and view models.

```csharp
// A module linked in the application, activated on demand: nothing is read by reflection, trimming and Native AOT are safe.
modules.Load("Birds", services => services.AddMauiBirdsModule());

// A module in a file the application does not reference: found by [RouteableModule], called on the services of the module.
modules.LoadFrom(Path.Combine(AppContext.BaseDirectory, "Modules", "XAnimals.Maui.Birds.dll"));
```

```csharp
// Samples/Modules/XAnimals.Maui.Birds/MauiBirdsModule.cs
[RouteableModule("Birds")]
public static IServiceCollection AddMauiBirdsModule(this IServiceCollection services)
    => global::XAnimals.Birds.BirdsModule.AddBirdsModule(services.AddGeneratedRoutes());
```

`ModuleLoaded` is raised once the entries are in the menus, and `Loaded` lists what was loaded. Loading a name that is loaded does nothing. Call `Load` from the UI thread, because the menu is an observable collection.

The container is built at start and cannot take services afterwards, so a module loaded while running keeps its own:
- It adds routes, menu entries and services. A service of the module is a singleton (one for the application) or transient; a scoped one lives as long as the application. A keyed service, a region or a segment of the module is not registered.
- A service of the application that was already built does not see the module.
- A route of the application wins over the same route of a module.
- A module is never unloaded.
- A module that throws, or whose entries cannot be built, is not loaded: none of its routes, entries or services is registered, and it can be loaded again. Loading a name that is already loaded does nothing; with another method than the first, the second is not run and the mistake goes to the `IRouteableErrorSink`.
- `LoadFrom` and `Load(Assembly)` read assemblies by reflection, so they are not for a trimmed or Native AOT application (the compiler warns); `Load(name, add)` is. The assemblies the application already has (Routeables, the UI framework) are shared with the module, the others are searched with its `.deps.json`, then next to it. A platform that cannot load files at run time (the stores of iOS) uses `Load(name, add)`.

### Lifetimes

A view and its view model are `Transient` by default: a new pair is built for each route change. Declare another lifetime on the attribute; `ViewModelLifetime` and `ViewLifetime` set one of the two, and `Lifetime` (or the constructor argument) sets both:

```csharp
// The pages of the sample are Transient. To try the other lifetimes on BoardViewModel:

// Both scoped (one pair per scope: per circuit in Blazor Server).
[Routeable("board", serviceLifetime: ServiceLifetime.Scoped)]
public class BoardViewModel { /* ... */ }

// One view model for the whole app, the view is built again for each route change:
// the lifetimes of the two halves are set on the view.
[RouteableView(typeof(BoardViewModel), "board", ViewModelLifetime = ServiceLifetime.Singleton)]
public partial class BoardView { }
```

Without the generator, `services.RegisterRoute<TView, TViewModel>(viewModelLifetime:, viewLifetime:, route)` takes the same two lifetimes.

A `Scoped` or `Singleton` instance is cached by its route and built once, with the arguments of the first route change: later route changes to the same route do not pass theirs again. Keep pages that receive a different argument each time (a detail page) `Transient`.

## Going to a route from XAML

A button can go to a route with no code in the view model. `RouteableLink.Route` is an attached property of the button on Avalonia, WPF, Uno and MAUI (the host namespace is the one of `RouteableRegion`):

```xml
<Button Content="Hello" routeables:RouteableLink.Route="/hello/World" />
<Button Content="Details" routeables:RouteableLink.Route="/dogs/Eurohound" routeables:RouteableLink.Region="detail" />
```

The click goes to the route in the router of `RouteableLink.Region` (the default router without it), like `provider.GoToAsync(route, region)`. The route is a fixed address: for one that depends on the view model, bind a command that calls the typed routing method. A failed or refused route change is reported to the `IRouteableErrorSink`, as the other errors that have no caller to throw to; the button does not need `CanExecute` because a guard decides. On Blazor, an `<a href="/hello/World">` already does this through the URL.
