<p align="center">
  <img src="images/logo.svg" alt="Routeables" width="420">
</p>

<p align="center">
  <a href="https://github.com/cedric56/Routeables/actions/workflows/ci.yml"><img src="https://github.com/cedric56/Routeables/actions/workflows/ci.yml/badge.svg?branch=master" alt="CI"></a>
  <a href="LICENSE.txt"><img src="https://img.shields.io/badge/license-MIT-5B35E0" alt="MIT License"></a>
  <img src="https://img.shields.io/badge/.NET-10-512BD4?logo=dotnet&logoColor=white" alt=".NET 10">
  <a href="https://www.nuget.org/packages/Routeables"><img src="https://img.shields.io/nuget/v/Routeables?label=Routeables" alt="NuGet Routeables"></a>
  <a href="https://www.nuget.org/packages/Routeables.Avalonia"><img src="https://img.shields.io/nuget/v/Routeables.Avalonia?label=Routeables.Avalonia" alt="NuGet"></a>
  <a href="https://www.nuget.org/packages/Routeables.Razor"><img src="https://img.shields.io/nuget/v/Routeables.Razor?label=Routeables.Razor" alt="NuGet"></a>
  <a href="https://www.nuget.org/packages/Routeables.Maui"><img src="https://img.shields.io/nuget/v/Routeables.Maui?label=Routeables.Maui" alt="NuGet"></a>
  <a href="https://www.nuget.org/packages/Routeables.Uno"><img src="https://img.shields.io/nuget/v/Routeables.Uno?label=Routeables.Uno" alt="NuGet"></a>
  <a href="https://www.nuget.org/packages/Routeables.Wpf"><img src="https://img.shields.io/nuget/v/Routeables.Wpf?label=Routeables.Wpf" alt="NuGet"></a>  
</p>

# Routeables

**View-model-first routing and navigation for Blazor, .NET MAUI, Avalonia, Uno Platform and WPF.**

Coming from Prism? Start with [Migrating from Prism](docs/guides/migrating-from-prism.md).

A view model asks an `IRouter` to go somewhere. It never touches a page, a frame or a navigation API of the framework, so the same code runs on every host.

Try it 
<a href="https://cedric56.github.io/Routeables/demo/avalonia/">The Animals sample running in your browser (Avalonia, WebAssembly).</a>

## The same sample on every host

The same view models drive the Animals sample on each host: only the views differ.

<table>
  <tr>
    <td align="center"><img src="images/demos/avalonia-desktop.gif" alt="The Animals sample on Avalonia" width="220"><br><sub>Avalonia</sub></td>
    <td align="center"><img src="images/demos/wpf.gif" alt="The Animals sample on WPF" width="220"><br><sub>WPF</sub></td>
    <td align="center"><img src="images/demos/uno.gif" alt="The Animals sample on Uno Platform" width="220"><br><sub>Uno Platform</sub></td>
  </tr>
  <tr>
    <td align="center"><img src="images/demos/maui-desktop.gif" alt="The Animals sample on .NET MAUI" width="220"><br><sub>.NET MAUI</sub></td>
    <td align="center"><img src="images/demos/mauihybrid.gif" alt="The Animals sample on MAUI Blazor Hybrid" width="220"><br><sub>MAUI Blazor Hybrid</sub></td>
    <td align="center"><img src="images/demos/blazor-server.gif" alt="The Animals sample on Blazor Server" width="220"><br><sub>Blazor Server</sub></td>
  </tr>
</table>

## In one minute

Two ways to register a page. Both start the same way and need no menu (Avalonia shown; the other hosts: [Start with your host](#start-with-your-host)).

**With the generator**: the route is on the view model, the view is found by its name.

```csharp
//You can define the entry route by default it is "/" like Blazor
public partial class MainViewModel(IRouter router) : MainViewModelDefault(router, "/hello/World") 
{

}
```
Without generated source

```csharp
public sealed class HelloViewModel(string name)
{
    public string? Name { get; } = name;
}

public partial class HelloView : UserControl { }


AppBuilder.Configure<App>()
    .UseRouteables<MainViewModel>(r =>
        {
             r.RegisterRoute<HelloView, HelloViewModel>(route: "/hello/{name}");
        },
        view: lifetime => new MainWindow());
```


```csharp
AppBuilder.Configure<App>()
    .UseRouteables<MainViewModel>(r =>
        {
             r.RegisterRoute<HelloView, HelloViewModel>(route: "/hello/{name}");
        },
        view: lifetime => new MainWindow());
```
OR with generated source 
```csharp
[Routeable("/hello/{name}")]                         // the route is declared on the view model
public sealed class HelloViewModel(string name)
{
    public string? Name { get; } = name;
}
AppBuilder.Configure<App>()
    .UseRouteables<MainViewModel>(r =>
        {
             r.AddGeneratedRoutes();
        },
        view: lifetime => new MainWindow());
```

In both, the main view says where the pages appear (Blazor: `<RouteableRegion />`):

```xml
xmlns:routeables="clr-namespace:Routeables.Avalonia;assembly=Routeables.Avalonia"
<routeables:RouteableRegion />
```

Important don't forget to remove initialization in OnFrameworkInitializationCompleted

```csharp
  public override void OnFrameworkInitializationCompleted()
  {
      //Routeables will do it
      //if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
      //{
      //    desktop.MainWindow = new MainWindow();
      //}

      base.OnFrameworkInitializationCompleted();
  }
```

A route (on the view model, or given by name), a view, one registration and a `RouteableRegion`: the zone of the screen that shows whatever the router displays. That is the whole model. A main view model is only for a menu: `UseRouteables<MainViewModel>(...)`. The first page, step by step: [Getting started](docs/getting-started.md).

### Several zones

`RouteableRegion` without a name shows the default router. To have several zones, each with its own router and history (a side panel, a detail pane, a tab), name them: `services.AddRegion("detail")`, `<routeables:RouteableRegion Region="detail" />`, and `[Routeable("/hello/{name}", region: "detail")]` on the view model. A view model that asks for an `IRouter` receives the router of its region, so `Back` stays in the region. More in [Regions](docs/guides/regions.md).

## Start with your host

Add the package of your host, then start Routeables where your framework starts. The host package brings the `Routeables` package, which carries the generator: there is nothing else to add.

| Host | Package | Setup |
| --- | --- | --- |
| Blazor | [`Routeables.Razor`](src/Routeables.Razor/README.md) | `services.UseRouteablesServer<MainViewModel>(r => { ... })` |
| .NET MAUI | [`Routeables.Maui`](src/Routeables.Maui/README.md) | `builder.UseMauiApp<App>().UseRouteables<MainViewModel>(r => { ... })` |
| Avalonia | [`Routeables.Avalonia`](src/Routeables.Avalonia/README.md) | `AppBuilder.Configure<App>().UseRouteables<MainViewModel>(r => { ... })` |
| Uno Platform | [`Routeables.Uno`](src/Routeables.Uno/README.md) | `this.UseRouteables<MainViewModel>(r => { ... })` |
| WPF | [`Routeables.Wpf`](src/Routeables.Wpf/README.md) | `this.UseRouteables<MainViewModel>(r => { ... })` |

```bash
dotnet add package Routeables.Avalonia   # or Routeables.Razor, .Maui, .Uno, .Wpf
```

The call starts Routeables and registers your menu (the main view model). The block is for your own registrations, the same on every host:

```csharp
r => r.AddGeneratedRoutes()
```

Each host README has the complete setup. [Getting started](docs/getting-started.md) walks through it. To start, `dotnet new install Routeables.Templates` then `dotnet new routeables-avalonia -n MyApp` (Avalonia only for now), or create a project with the template of your framework (`dotnet new maui`, `dotnet new blazor`, or the Uno one) and add the host package.

## Status

Routeables is young: the first commit is from October 2026, it has one maintainer, and the API is still evolving, so a few breaking changes can come with the next versions. They are named in the release notes of each version: pin the version you use and read the notes before you update. What is documented works as described, and CI tests it on every host.

## Features

| Area | What Routeables offers | Guide |
| --- | --- | --- |
| Routes | `[Routeable("/path/{id:int}")]` on the view model; view paired by name; constraints; most specific template wins; query strings | [Routes](docs/guides/routes.md) |
| Declarative navigation | `routeables:RouteableLink.Route="/hello/World"` on a button, in XAML, no code in the view model (Avalonia, WPF, Uno, MAUI) | [Routes](docs/guides/routes.md) |
| Typed routing | `GoToMonkeyDetailAsync(router, name)` generated from the constructor | [Typed routes](docs/guides/typed-routes.md) |
| Arguments | Route values, query values and objects passed to the target | [Arguments](docs/guides/arguments.md) |
| History | Back, forward, pop, back to root, move to an entry, erase; no limit by default, `LimitHistory` to set one | [History](docs/guides/history.md) |
| Regions | Several zones, each with its own router and history | [Regions](docs/guides/regions.md) |
| Lifecycle | Initialize, entering, leaving, `CanLeaveAsync` veto, `OnSubscribe`/`OnUnsubscribe`, destroy | [Lifecycle](docs/guides/lifecycle.md) |
| Guards | `IRouteGuard` with an order; allow, redirect or cancel a route change | [Routes](docs/guides/routes.md) |
| Transitions | 15 built-in transitions, custom ones registered per host | [Transitions](docs/guides/transitions.md) |
| Dialogs and notifications | One dialog service and one notification service on every host, replaceable at startup, system notifications (Avalonia, Blazor, MAUI) | [System integration](docs/guides/system-integration.md) |
| Deep links | Open a route from a URL or a system activation | [System integration](docs/guides/system-integration.md) |
| Modules | A feature is one extension method, loaded at startup or on demand | [Hosts](docs/guides/hosts.md) |
| Locking | Pages that block leaving, a locked menu | [Locking](src/Routeables.Locking/README.md) |
| Menu | `RouteableMenu` on Avalonia and WPF, items bound to routes | [Hosts](docs/guides/hosts.md) |
| Errors | `RouteableResult`, `IRouteableErrorSink` | [Lifecycle](docs/guides/lifecycle.md) |
| Diagnostics | Tracing and metrics for route changes | [Diagnostics](docs/guides/diagnostics.md) |
| Native AOT | Source generated; nothing scans assemblies at run time | [AOT](docs/guides/aot.md) |
| Hosts | Blazor, .NET MAUI, Avalonia, Uno Platform, WPF | [Hosts](docs/guides/hosts.md) |
| Dialog libraries | MudBlazor, CommunityToolkit.Maui, FluentAvalonia | [Reshapes](reshapes/README.md) |

Routeables is the navigation layer, dialogs and notifications included. It ships no commands, view-model locator or event aggregator: use CommunityToolkit.Mvvm, ReactiveUI or plain C#.

## Add what you need

The host package already holds the dialogs, modals and notifications of its framework, registered by `UseRouteables`. For MudBlazor, CommunityToolkit.Maui or FluentAvalonia dialogs, add a package of [`Reshapes`](reshapes/README.md).

Everything else is in the one `Routeables` package, which every host brings: the contracts ([`Abstraction`](src/Routeables.Abstraction/README.md)), the router ([`Routing`](src/Routeables.Routing/README.md)), the view-model bases ([`Core`](src/Routeables.Core/README.md)), [`Regions`](src/Routeables.Regions/README.md), [`Segments`](src/Routeables.Segments/README.md) (`/Bears?SearchText=polar`), [`Locking`](src/Routeables.Locking/README.md) (pages that block leaving, a locked menu), the dialog and notification contracts ([`Services`](src/Routeables.Services/README.md)) and the source generator. They are separate projects of this repository and one package.

## Learn more

- [Getting started](docs/getting-started.md) and the [guides](docs/guides/routes.md): routes, typed routing, arguments, lifecycle, history, regions, modules, hosts.
- [Compared with other routers](docs/guides/comparison.md): Prism, ReactiveUI, MAUI Shell, Uno.Extensions and the Blazor router, and where each one fits.
- [Samples](samples): the same Animals app on Blazor, MAUI, Avalonia, Uno and WPF.
- [Documentation site](https://cedric56.github.io/Routeables/) and API reference.
- [Contributing](CONTRIBUTING.md), [third-party notices](THIRD-PARTY-NOTICES.md).

Released under the [MIT License](LICENSE.txt).
