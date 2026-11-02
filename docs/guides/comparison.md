# Routeables compared with other routers

Routeables does one job: it changes the route, from a view model, on Blazor, .NET MAUI, Avalonia, Uno Platform and WPF. This page says where that is enough, where another library is the better choice, and what Routeables is not (yet).

It is written from the public documentation of each project. Projects change: check the one you are weighing before you decide, and open an issue if something here is out of date.

## Where Routeables stands today

- **Young.** The API can still change between versions, and the project has one maintainer. A router sits under every page of an application, so this weighs more than any feature.
- **Narrow on purpose.** It ships no container, no commands, no event aggregator and no view-model locator. Use CommunityToolkit.Mvvm or ReactiveUI for those; Routeables has a source flavour for each of them.
- **Proven by its own tests.** The shared core runs in unit tests, each host has headless, browser or on-device tests, and a Native AOT application and a trimmed Blazor WebAssembly application are published in CI. That says the code does what it claims. It does not say how it behaves in your own application.

## What it offers that is hard to find together

- **One route table for several frameworks.** The same view model, with the same `[Routeable("/dogs/{name}")]`, runs on five hosts. Only the views are written once per framework.
- **No reflection at run time.** Routes are registered by a source generator, and navigation methods such as `GoToDogDetailAsync(name)` are generated, so a wrong route is a compile error. See [Native AOT and trimming](aot.md) and [Typed routing methods](typed-routes.md).
- **Regions, history, guards and a lifecycle** behind the same `IRouter`, with a redirect from a guard, deep links and Back handling. See [Regions](regions.md), [History](history.md) and [Lifecycle, guards and events](lifecycle.md).

## Against each alternative

### Prism

Prism is the established choice: navigation with regions, modules, a dialog service, a container abstraction, commands and an event aggregator, with a large community and years of use in production. Choose it when you want that whole framework from one project, or when you need the support and the examples that a mature library has.

Choose Routeables instead when you only want the routing, when the application has to run on Blazor as well, or when trimming and Native AOT matter to you. [Migrating from Prism](migrating-from-prism.md) maps the concepts one by one.

### ReactiveUI routing

ReactiveUI routes with a navigation stack (`IScreen`, `RoutingState`, routable view models) and works with many UI frameworks. It is the natural pick when the application is already written in the reactive style.

Routeables has no opinion about Rx and does not use ReactiveUI's routing: a route is a text with parameters, matched to a constructor of the view model, and regions and guards belong to the router. Its ReactiveUI source flavour lets a view model use ReactiveUI next to it (see [Hosts, services and samples](hosts.md)).

### .NET MAUI Shell

Shell is part of MAUI, needs nothing more and is maintained with it. On a MAUI application that will stay a MAUI application, it can be all you need.

Routeables adds view-model-first routes and the other hosts. The two can live in the same application: `ShellRoutes.UseShellRoutes` lets `Shell.Current.GoToAsync("//orders/42")` reach the router, and an address that the router does not know is left to Shell. See [Hosts, services and samples](hosts.md).

### Uno.Extensions Navigation

The navigation of Uno.Extensions is made for Uno Platform: view maps, route maps and regions, maintained by the team behind the platform. For an application that is only on Uno, it is the closer fit.

Routeables on Uno uses the same view models as on the other hosts. It is the better fit when the application is not only on Uno.

### The Blazor router

The router of Blazor is made of components: `@page` on a component. Routeables on Blazor routes view models, so the same view model can be shown by a MAUI or an Avalonia view. See [Routeables for Blazor developers](blazor.md).

## When Routeables is not the right choice

- You need a library that has been in production for years, with a community to ask.
- You want one framework to give you the container, the commands, the messaging and the navigation.
- The application is on one framework only, and that framework's own router does what you need.
- Your team cannot absorb an API that may change while the library is a preview.

## When it is worth a try

- The same view models must run on several frameworks.
- The application is trimmed or published with Native AOT, and a router that scans assemblies at run time is a problem.
- You want navigation errors found at compile time.
- You are leaving Prism and want to keep the routes, the lifecycle and the regions, and nothing else.
