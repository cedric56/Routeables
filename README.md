<p align="center">
  <img src="Images/logo.svg" alt="Routeables" width="420">
</p>

<p align="center">
  <a href="https://github.com/cedric56/Routeables/actions/workflows/ci.yml"><img src="https://github.com/cedric56/Routeables/actions/workflows/ci.yml/badge.svg?branch=master" alt="CI"></a>
  <a href="LICENSE.txt"><img src="https://img.shields.io/badge/license-MIT-5B35E0" alt="MIT License"></a>
  <img src="https://img.shields.io/badge/.NET-10-512BD4?logo=dotnet&logoColor=white" alt=".NET 10">
  <a href="https://www.nuget.org/packages/Routeables">
  <img src="https://img.shields.io/nuget/v/Routeables?label=Routeables" alt="NuGet"></a>
  <a href="https://www.nuget.org/packages/Routeables.Avalonia"><img src="https://img.shields.io/nuget/v/Routeables.Avalonia?label=Routeables.Avalonia" alt="NuGet"></a>
  <a href="https://www.nuget.org/packages/Routeables.Razor"><img src="https://img.shields.io/nuget/v/Routeables.Razor?label=Routeables.Razor" alt="NuGet"></a>
  <a href="https://www.nuget.org/packages/Routeables.Maui"><img src="https://img.shields.io/nuget/v/Routeables.Maui?label=Routeables.Maui" alt="NuGet"></a>
  <a href="https://www.nuget.org/packages/Routeables.Uno"><img src="https://img.shields.io/nuget/v/Routeables.Uno?label=Routeables.Uno" alt="NuGet"></a>
  <a href="https://www.nuget.org/packages/Routeables.Wpf"><img src="https://img.shields.io/nuget/v/Routeables.Wpf?label=Routeables.Wpf" alt="NuGet"></a>  
</p>

# Routeables

**Routing from your view models, for Blazor, .NET MAUI, Avalonia, Uno Platform and WPF.**

A view model asks an `IRouter` to go somewhere. It never touches a page, a frame or a navigation API of the framework, so the same code runs on every host.

Try it 
<a href="https://cedric56.github.io/Routeables/demo/avalonia/">The Animals sample running in your browser (Avalonia, WebAssembly).</a>

## In one minute

```csharp
[Routeable("/hello/{name}")]                         // the route is declared on the view model
public sealed class HelloViewModel(IRouter router, string name)
{
    public Task SayHelloAgainAsync() => router.GoToAsync("/hello/World");
}

public partial class HelloView : RouteableFor<HelloViewModel> { }   // the page: found by its name

services.AddGeneratedRoutes();                       // once, at startup
```

```xml
<!-- MainView: the zone where the pages appear (Blazor: <RouteableRegion />) -->
<routeables:RouteableRegion />
```

A route on the view model, the view with the matching name, one registration call, and a `RouteableRegion` in your main view: the zone of the screen that shows whatever the router displays. That is the whole model.

### Several zones

`RouteableRegion` without a name shows the default router. To have several zones, each with its own router and history (a side panel, a detail pane, a tab), name them: `services.AddRegion("detail")`, `<routeables:RouteableRegion Region="detail" />`, and `[Routeable("/hello/{name}", region: "detail")]` on the view model. A view model that asks for an `IRouter` receives the router of its region, so `Back` stays in the region. More in [Regions](docs/guides/regions.md).

## Start with your host

Add the package of your host, then start Routeables where your framework starts. The host package brings the `Routeables` package, which carries the generator: there is nothing else to add.

| Host | Package | Setup |
| --- | --- | --- |
| Blazor | [`Routeables.Razor`](Routeables.Razor/README.md) | `services.UseRouteablesServer<MainViewModel>(r => { ... })` |
| .NET MAUI | [`Routeables.Maui`](Routeables.Maui/README.md) | `builder.UseMauiApp<App>().UseRouteables<MainViewModel>(r => { ... })` |
| Avalonia | [`Routeables.Avalonia`](Routeables.Avalonia/README.md) | `AppBuilder.Configure<App>().UseRouteables<MainViewModel>(r => { ... })` |
| Uno Platform | [`Routeables.Uno`](Routeables.Uno/README.md) | `this.UseRouteables<MainViewModel>(r => { ... })` |
| WPF | [`Routeables.Wpf`](Routeables.Wpf/README.md) | `this.UseRouteables<MainViewModel>(r => { ... })` |

```bash
dotnet add package Routeables.Avalonia   # or Routeables.Razor, .Maui, .Uno, .Wpf
```

The call starts Routeables and registers your menu (the main view model). The block is for your own registrations, the same on every host:

```csharp
r => r.AddGeneratedRoutes()
```

Each host README has the complete setup. [Getting started](docs/getting-started.md) walks through it. To start, create a project with the template of your framework (`dotnet new avalonia.app`, `dotnet new maui`, `dotnet new blazor`, or the Uno one) and add the host package.

## What you get

- **Routes from attributes**, paired with views by name, registered by a source generator.
- **Typed routing methods** generated from a view model's constructor: `GoToMonkeyDetailAsync(router, name)` instead of a string.
- **History**: back, forward, pop, return to the root; a page can veto leaving.
- **Regions**: several areas of the screen, each with its own history.
- **Modules**: a feature is one extension method, loaded at startup or on demand.
- **Native AOT and trimming** compatible; nothing scans assemblies at run time.

Routeables routes. It ships no commands, view-model locator or event aggregator: use CommunityToolkit.Mvvm, ReactiveUI or plain C#. Coming from Prism? Read [Migrating from Prism](docs/guides/migrating-from-prism.md).

## Add what you need

The host package already holds the dialogs, modals and notifications of its framework, registered by `UseRouteables`. For MudBlazor, CommunityToolkit.Maui or FluentAvalonia dialogs, add a package of [`Reshapes`](Reshapes/README.md).

Everything else is in the one `Routeables` package, which every host brings: the contracts ([`Abstraction`](Routeables.Abstraction/README.md)), the router ([`Routing`](Routeables.Routing/README.md)), the view-model bases ([`Core`](Routeables.Core/README.md)), [`Regions`](Routeables.Regions/README.md), [`Segments`](Routeables.Segments/README.md) (`/Bears?SearchText=polar`), [`Locking`](Routeables.Locking/README.md) (pages that block leaving, a locked menu), the dialog and notification contracts ([`Services`](Routeables.Services/README.md)) and the source generator. They are separate projects of this repository and one package.

## Learn more

- [Getting started](docs/getting-started.md) and the [guides](docs/guides/routes.md): routes, typed routing, arguments, lifecycle, history, regions, modules, hosts.
- [Samples](Samples): the same Animals app on Blazor, MAUI, Avalonia, Uno and WPF.
- [Documentation site](https://cedric56.github.io/Routeables/) and API reference.
- [Contributing](CONTRIBUTING.md), [third-party notices](THIRD-PARTY-NOTICES.md).

Released under the [MIT License](LICENSE.txt).
