# Routes and templates

## Routes and templates

A **route** is the concrete string you go to (`/orders/42`). A **template** is the registered pattern that is matched against it (`/orders/{id:int}`). The router parses the route against the template and hands the parsed values to the destination.

| Placeholder | Meaning |
| --- | --- |
| `{id}` | Required segment, string |
| `{id:int}` | Required segment with a type: `string`, `int`, `long`, `bool`, `guid`, `decimal`, `double` |
| `{id?}` | Optional segment |
| `?{tab}` / `?{tab?}` | Query-string value, required / optional |

Query values that are not declared in the template are kept as neutral values (`IRouteableArgs.Values`, read by the lifecycle hooks): they are never given to the constructor or injected into a property, so a link with `?utm_source=mail` still opens the page, and nobody can fill a constructor parameter from the address.

`[Routeable]` takes a path, an optional region and a service lifetime. It can be repeated on the same class to give a view model several routes:

```csharp
[Routeable("/orders/{id:int}")]
[Routeable("/o/{id:int}")]
public sealed class OrderViewModel(IRouter router, int id) { }
```

### Main view model per host

The menu or tab bar (`IMainRouteables`) is registered by `UseRouteables<MainViewModel>(...)` of the host (or by `services.RegisterMainRouteables<MainViewModel>()`, which does the same). Its lifetime is the host's: `Scoped` on Blazor (one per circuit), `Singleton` on Avalonia, MAUI and Uno. Pass a lifetime only to override it. A `Transient` one is refused with an `ArgumentException`, because each consumer would get another instance than the one the window shows. At start the host calls `IMainRouteables.SelectFirst()`, which selects the first entry when none is selected, the same way on the four hosts.

### Modules

A feature of the application (orders, customers...) can live in a class library of its own. There is no module catalog. A module is an extension method that the application calls at start, so the compiler, the trimmer and Native AOT see every module; one that is not known at start is loaded while the application runs ([below](#loading-a-module-while-the-application-runs)).

```csharp
// In the Orders library, which references Routeables.Generated.
public static class OrdersModule
{
    public static IServiceCollection AddOrdersModule(this IServiceCollection services)
        => services.AddGeneratedRoutes()                                   // the routes of this library
                   .AddSingleton<IOrderRepository, OrderRepository>()      // its own services
                   .AddMenuEntries(() =>
                   [
                       new RouteableEntry<OrdersViewModel>("Orders", route: "/orders", template: "/orders"),
                   ]);
}

// In the application.
services.RegisterMainRouteables<MainViewModel>();      // or UseRouteables<MainViewModel>(r => r.AddOrdersModule())
services.AddOrdersModule();
```

`AddGeneratedRoutes()` is `internal` in each project, which is why the library wraps it in a public method. `AddMenuEntries` puts the entries of the module in the menu, after those of the main view model, in the order the modules were added. The function runs once for each main view model: build new entries in it, so that a lock or a selection is not shared between the circuits of a Blazor Server application. Adding the same module twice adds its entries once. A module that needs another one calls its method (`AddCustomersModule()`) itself: the entries of the module it needs come first, and a module called by two others is added once. Register its services with `TryAdd...` so that a second call changes nothing. By default the entries go after the ones of the main view model; `AddMenuEntries(entries, index: 0)` puts them first, `index: 2` after the second entry, and a value past the end puts them last. The index counts the entries the menu has when the module is added to it, and a module loaded while the application runs is inserted the same way.

The Birds module of the samples is the other shape, a module that serves several hosts (Razor, Avalonia, MAUI, Uno) and can be shipped. Its view models, its data and its menu entry are in one library, `XAnimals.Birds`; each platform has a project of its own, `XAnimals.Razor.Birds`, `XAnimals.Avalonia.Birds`, `XAnimals.Maui.Birds` and `XAnimals.Uno.Birds`, that references it and holds the views of that platform, since a view belongs to the UI framework of its host. The views draw with controls (`AnimalList`, `AnimalDetail`) that the application also uses, so they are in a library of their own per platform (`XAnimals.Avalonia.Controls`, `XAnimals.Maui.Controls`, `XAnimals.Uno.Controls`; the Razor ones are in `XAnimals.Razor`), which both the application and the module reference. The application references the project of its platform, nothing else: its `AddGeneratedRoutes()` finds the views in the referenced assembly, and `XAnimals.ViewModels` calls `AddBirdsModule()`. Each platform project also has an entry method marked `[RouteableModule("Birds")]` (`AddAvaloniaBirdsModule()`...) so the same module can be [loaded while the application runs](#loading-a-module-while-the-application-runs): `modules.LoadFrom("XAnimals.Avalonia.Birds.dll")`. Keep one view per host for a route: two views registered for the same view model and route replace one another, and the last registered wins (ROUT203 reports it). The Birds page also shows how a module is loaded while the application runs: its **Load Module** button calls `IRouteableModules.Load` (through `BirdsViewModel.LoadMoreBirds`) with the method of its platform that registers the page of a second module, More birds (`AddAvaloniaMoreBirdsModule()`...), and the entry "More birds" appears in the menu without restarting. That page has no `[RouteableView]`, so nothing registers it at start.

#### Loading a module while the application runs

`IRouteableModules`, registered with `RegisterMainRouteables`, loads a module when the application asks for it, as Prism does with `OnDemand`. The routes of the module can be reached at once (by the router and from an address), its entries are added to the menu of every main view model alive and of the ones built later, and its services are available to its own views and view models.

```csharp
// A module linked in the application, activated on demand: nothing is read by reflection, trimming and Native AOT are safe.
modules.Load("Orders", services => services.AddOrdersModule());

// A module in a file the application does not reference: found by [RouteableModule], called on the services of the module.
modules.LoadFrom(Path.Combine(AppContext.BaseDirectory, "Modules", "Orders.dll"));
```

```csharp
// In the Orders assembly.
[RouteableModule("Orders")]
public static IServiceCollection AddOrdersModule(this IServiceCollection services)
    => services.AddGeneratedRoutes().AddMenuEntries(() => [new RouteableEntry<OrdersViewModel>("Orders", route: "/orders", template: "/orders")]);
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
// Both scoped (one pair per scope: per circuit in Blazor Server).
[Routeable("/profile", serviceLifetime: ServiceLifetime.Scoped)]
public sealed class ProfileViewModel { }

// One cart for the whole app, the view is built again for each route change:
// the lifetimes of the two halves are set on the view.
[RouteableView(typeof(CartViewModel), "/cart", ViewModelLifetime = ServiceLifetime.Singleton)]
public partial class CartView { }
```

Without the generator, `services.RegisterRoute<TView, TViewModel>(viewModelLifetime:, viewLifetime:, route)` takes the same two lifetimes.

A `Scoped` or `Singleton` instance is cached by its route and built once, with the arguments of the first route change: later route changes to the same route do not pass theirs again. Keep pages that receive a different argument each time (a detail page) `Transient`.
