# Routeables compared with other routers

Routeables is a navigation framework for view-model-first applications on Blazor, ASP.NET Core, .NET MAUI, Avalonia, Uno Platform, WinUI 3 and WPF. A view model asks an `IRouter` to go somewhere; routes, history, regions, guards, lifecycle, transitions, deep links, modules, dialogs and notifications are the same on every host. It is not an MVVM toolkit: commands and messaging come from CommunityToolkit.Mvvm or ReactiveUI, and Routeables has a source flavour for each of them.

This page is written from the public documentation of each project. Projects change: check the one you are weighing, and open an issue if something here is out of date.

## What sets it apart

- **One framework for seven hosts.** The same view model, with the same `[Routeable("/dogs/{name}")]`, runs on every host. Only the views are written once per UI framework.
- **Source-generated, with no reflection at run time.** Routes are registered at build time, and navigation methods such as `GoToDogDetailAsync(name)` are generated, so a wrong route is a compile error. A Native AOT application and a trimmed Blazor WebAssembly application are published in CI, and a trim or AOT warning fails the build. See [Native AOT and trimming](aot.md) and [Typed routing methods](typed-routes.md).
- **The whole navigation, behind one `IRouter`.** History with back, forward and pop, [regions](regions.md) that each have their own router and history, [guards](lifecycle.md) that allow, redirect or cancel, a lifecycle with a veto, 15 built-in [transitions](transitions.md), deep links, locking, and modules loaded at startup or on demand.
- **Dialogs and notifications included.** One dialog service (message boxes, content dialogs, task dialogs, modals) and one notification service on every host, replaceable at startup, with system notifications on Avalonia, Blazor and MAUI. The `Reshapes` packages draw them with MudBlazor, CommunityToolkit.Maui or FluentAvalonia.
- **Tested where it runs.** Unit tests on the shared core, headless, browser and on-device tests on each host, benchmarks with memory-leak checks (see [Benchmarks](../benchmarks.md)), and a guide for [migrating from Prism](migrating-from-prism.md).

## Against each alternative

### Prism

Prism is the established framework: regions, modules, a dialog service, a container abstraction, commands and an event aggregator, with a large community. It is the choice when you want that whole set from one project.

Routeables covers the navigation side of it (routes, regions, lifecycle, guards, dialogs, modules) and leaves the container, the commands and the messaging to the libraries you already use. It also runs on Blazor, and it is built to be trimmed and published with Native AOT. [Migrating from Prism](migrating-from-prism.md) maps the concepts one by one.

### ReactiveUI routing

ReactiveUI routes with a navigation stack (`IScreen`, `RoutingState`, routable view models) and supports many UI frameworks. It is the natural choice when the application is written in the reactive style throughout.

Routeables does not depend on Rx: a route is a text with parameters matched to a constructor of the view model, and regions, guards, transitions, dialogs and notifications belong to the router. Its ReactiveUI source flavour lets a view model use ReactiveUI next to it (see [Hosts, services and samples](hosts.md)).

### .NET MAUI Shell

Shell is part of MAUI, needs nothing more, and is maintained with it. For an application that is only on MAUI, it can be all you need.

Routeables adds view-model-first routes and the other hosts. The two live together: `RouteableShellRoutes.UseShellRoutes` lets `Shell.Current.GoToAsync("//orders/42")` reach the router, and an address the router does not know is left to Shell. See [Hosts, services and samples](hosts.md).

### Uno.Extensions Navigation

The navigation of Uno.Extensions is made for Uno Platform, with view maps, route maps and regions, maintained by the team behind the platform. For an application that is only on Uno, it is the closest fit.

Routeables on Uno uses the same view models as on the other hosts, which matters when the application is not only on Uno.

### MvvmCross

MvvmCross is the closest in spirit: navigation goes through a service (`IMvxNavigationService`) that navigates between view models, not views (`Navigate<TViewModel>()`), and a view model that receives a parameter derives from a generic base (`MvxViewModel<TParameter>`). It is a full MVVM framework for many UI platforms, with years of use behind it.

Routeables gives an address to each view model (`[Routeable("/orders/{id}")]`), so a page can be reached from a deep link, a menu entry or the code with the same text, and it generates the typed `GoTo...Async` methods from the constructor of the view model. It targets Blazor and ASP.NET Core too, and leaves the container, the commands and the messaging to the libraries you already use.

### Caliburn.Micro

Caliburn.Micro describes itself as an opinionated MVVM framework for Xaml platforms (WPF, UWP, WinUI 3, .NET MAUI and Avalonia in its documentation). Its strengths are naming conventions that wire views to view models, screens and conductors that manage the life of the pages, an event aggregator and a window manager.

Routeables shares the convention that pairs a view with its view model by name, and keeps it optional: an attribute names the pair explicitly. It adds routes, a history, regions and guards over those pages, and it is not limited to Xaml (Blazor and ASP.NET Core are hosts). If your application is on a Xaml platform and the conventions and conductors already do what you need, Caliburn.Micro is a smaller step.

### A navigation service of your own

Many teams write their own: an `INavigationService` with `NavigateToAsync<TViewModel>()` and `GoBackAsync()`, a view locator and the container of the application. For one application on one UI framework it is often enough, and you control every line.

The cost grows with what you add: a history that survives the process being killed, guards, several regions, deep links, trimming and Native AOT safety, dialogs and notifications, and each UI framework you port it to, with its own threading and lifecycle. Routeables is that service, written once and tested on every host. It is worth considering when your own service starts to carry those features, or when a second UI framework appears.

### The Blazor router

The router of Blazor is made of components: `@page` on a component. Routeables on Blazor routes view models, so the same view model can be shown by a MAUI or an Avalonia view. See [Routeables for Blazor developers](blazor.md).

## Which one for which application

| Your application | Where to look first |
|------------------|---------------------|
| WPF, with a framework that provides the container, the commands and the messaging | Prism |
| Only .NET MAUI, and its pages are few | MAUI Shell |
| Only Uno Platform | Uno.Extensions Navigation |
| Only Blazor, with pages made of components | The Blazor router |
| Reactive from top to bottom | ReactiveUI |
| Several UI frameworks sharing view models, trimmed or compiled with Native AOT | Routeables |
| One application, one UI framework, a few pages | A navigation service of your own |

This is a starting point, not a verdict: each of these projects does more than this table says, and the table is written from their public documentation.

## Choosing

Routeables fits when:

- the same view models must run on several UI frameworks;
- the application is trimmed or published with Native AOT;
- you want navigation errors found at compile time;
- you want routes, regions, guards, dialogs and notifications from one place;
- you are moving a Prism application to .NET 10 and want to keep the routes, the lifecycle, the regions and the dialogs, converting one screen at a time.

Another library may fit better when you want one project to provide the container, the commands and the messaging as well, when the application is on a single framework whose own router already does what you need, or when you need a library with years of use in production.

Routeables is young: 2.1.0 was published in November 2026, it has one main maintainer and a small number of users, and it has not had the years in production that Prism, ReactiveUI or MvvmCross have. What stands against that is in the repository: a [versioning policy](https://github.com/cedric56/Routeables/blob/master/VERSIONING.md) with deprecations, a CI that checks the public API of the packages against the last release, tests on every host, and a [migration guide](migrating-from-prism.md). Try it on one screen or one region of your application before you decide.
