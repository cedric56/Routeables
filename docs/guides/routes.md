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

The menu or tab bar (`IMainRouteables`) is registered by the host with `services.RegisterMainRouteables<MainViewModel>(lifetime)`: `Scoped` on Blazor Server (one per circuit), `Singleton` on Avalonia and MAUI. A `Transient` one is refused with an `ArgumentException`, because each consumer would get another instance than the one the window shows. At start the host calls `IMainRouteables.SelectFirst()`, which selects the first entry when none is selected, the same way on the three hosts.

### Modules

A feature of the application (orders, customers...) can live in a class library of its own. There is no module catalog and no loading at run time: a module is an extension method that the application calls, so the compiler, the trimmer and Native AOT see every module.

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
services.RegisterMainRouteables<MainViewModel>(ServiceLifetime.Singleton);
services.AddOrdersModule();
```

`AddGeneratedRoutes()` is `internal` in each project, which is why the library wraps it in a public method. `AddMenuEntries` puts the entries of the module in the menu, after those of the main view model, in the order the modules were added. The function runs once for each main view model: build new entries in it, so that a lock or a selection is not shared between the circuits of a Blazor Server application. Adding the same module twice adds its entries once. A module that needs another one calls its method (`AddCustomersModule()`) itself.

The Birds module of the samples is the other shape, a module that serves several hosts (Razor, Avalonia, MAUI, Uno). Its view models and its data are in one library (`XAnimals.Birds`), and its views are in one folder of that project, `Platforms/<host>`, since a view belongs to the UI framework of its host. The library does not compile them: each host imports the `.props` of its folder (one line in its project file) and compiles the views with its own controls. For that reason this module does not call `AddGeneratedRoutes()`: the views are part of the host, whose `AddGeneratedRoutes()` registers them. Keep one view per host for a route: two views registered for the same view model and route replace one another, and the last registered wins. This folder layout is there for the demonstration, to show every host in one place. In a real application, give each host a project of its own (`XAnimals.Birds.Razor`, `XAnimals.Birds.Avalonia`, `XAnimals.Birds.Maui`, `XAnimals.Birds.Uno`) that references the module and the controls of its host: it is the shape that can be shipped as a package, its views are real types of their own assembly, and the host only references it (its `AddGeneratedRoutes()` finds the views in the referenced assembly).

### Lifetimes

A view and its view model are `Transient` by default: a new pair is built for each route change. Declare another lifetime on the attribute; `ViewModelLifetime` and `ViewLifetime` set one of the two, and `Lifetime` (or the constructor argument) sets both:

```csharp
// One cart for the whole app, the view is built again for each navigation.
[RouteableView(typeof(CartViewModel), "/cart", ViewModelLifetime = ServiceLifetime.Singleton)]
public partial class CartView { }

// Both scoped (one pair per scope: per circuit in Blazor Server).
[RouteableView(typeof(ProfileViewModel), "/profile", serviceLifetime: ServiceLifetime.Scoped)]
public partial class ProfileView { }
```

Without the generator, `services.RegisterRoute<TView, TViewModel>(viewModelLifetime:, viewLifetime:, route)` takes the same two lifetimes.

A `Scoped` or `Singleton` instance is cached by its route and built once, with the arguments of the first route change: later route changes to the same route do not pass theirs again. Keep pages that receive a different argument each time (a detail page) `Transient`.
