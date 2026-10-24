<p align="center">
  <img src="Images/logo.svg" alt="Routeables" width="420">
</p>

<p align="center">
  <a href="https://github.com/cedric56/Routeables/actions/workflows/ci.yml"><img src="https://github.com/cedric56/Routeables/actions/workflows/ci.yml/badge.svg?branch=master" alt="CI"></a>
  <a href="LICENSE.txt"><img src="https://img.shields.io/badge/license-MIT-5B35E0" alt="MIT License"></a>
  <img src="https://img.shields.io/badge/.NET-10-512BD4?logo=dotnet&logoColor=white" alt=".NET 10">
</p>

# Routeables

**Routing from your view models, for Blazor, .NET MAUI, Avalonia and Uno Platform.**

A view model asks an `IRouter` to go somewhere. It never touches a page, a frame or a navigation API of the framework, so the same code runs on every host.

> [!WARNING]
> Preview (`1.0.0`): APIs change between commits. Uno support has not been run on a device yet.

## In one minute

```csharp
[Routeable("/hello/{name}")]                         // the route is declared on the view model
public sealed class HelloViewModel(IRouter router, string name)
{
    public Task SayHelloAgainAsync() => router.GoToAsync("/hello/Ada");
}

public partial class HelloView : RouteableFor<HelloViewModel> { }   // the page: found by its name

services.AddGeneratedRoutes();                       // once, at startup
```

A route on the view model, the view with the matching name, one registration call. That is the whole model.

## Start with your host

Reference the host package and the generator (as an analyzer), then start Routeables where your framework starts.

| Host | Package | Setup |
| --- | --- | --- |
| Blazor | [`Routeables.Razor`](Routeables.Razor/README.md) | `services.UseRouteablesServer<MainViewModel>(r => { ... })` |
| .NET MAUI | [`Routeables.Maui`](Routeables.Maui/README.md) | `builder.UseMauiApp<App>().UseRouteables<MainViewModel>(r => { ... })` |
| Avalonia | [`Routeables.Avalonia`](Routeables.Avalonia/README.md) | `AppBuilder.Configure<App>().UseRouteables<MainViewModel>(r => { ... })` |
| Uno Platform | [`Routeables.Uno`](Routeables.Uno/README.md) | `this.UseRouteables<MainViewModel>(r => { ... })` |

```xml
<ItemGroup>
  <ProjectReference Include="..\Routeables\Routeables.Avalonia\Routeables.Avalonia.csproj" />   <!-- your host -->
  <ProjectReference Include="..\Routeables\Routeables.Generated\Routeables.Generated.csproj"
                    OutputItemType="Analyzer" ReferenceOutputAssembly="false" />
</ItemGroup>
```

The call starts Routeables and registers your menu (the main view model). The block is for your own registrations, the same on every host:

```csharp
r => r.AddGeneratedRoutes()
```

Each host README has the complete setup. [Getting started](docs/getting-started.md) walks through it, and `dotnet new routeables-blazor | routeables-maui | routeables-avalonia` creates a working app.

## What you get

- **Routes from attributes**, paired with views by name, registered by a source generator.
- **Typed routing methods** generated from a view model's constructor: `GoToOrderAsync(id)` instead of a string.
- **History**: back, forward, pop, return to the root; a page can veto leaving.
- **Regions**: several areas of the screen, each with its own history.
- **Modules**: a feature is one extension method, loaded at startup or on demand.
- **Native AOT and trimming** compatible; nothing scans assemblies at run time.

Routeables routes. It ships no commands, view-model locator or event aggregator: use CommunityToolkit.Mvvm, ReactiveUI or plain C#. Coming from Prism? Read [Migrating from Prism](docs/guides/migrating-from-prism.md).

## Add what you need

| I want | Package |
| --- | --- |
| Dialogs, notifications, theme | `Routeables.Razor.Services`, `Routeables.Maui.Services`, `Routeables.Avalonia.Services` or `Routeables.Uno.Services` |
| MudBlazor, CommunityToolkit.Maui or FluentAvalonia dialogs | [`Reshapes`](Reshapes/README.md) |
| Pages that block leaving, a locked menu | [`Routeables.Locking`](Routeables.Locking/README.md) |
| Region layouts and followers | [`Routeables.Regions`](Routeables.Regions/README.md) |
| Routing by segments (`Orders?id=5/Detail`) | [`Routeables.Segments`](Routeables.Segments/README.md) |

The contracts are in [`Routeables.Abstraction`](Routeables.Abstraction/README.md), the router in [`Routeables.Routing`](Routeables.Routing/README.md), the view-model bases in [`Routeables.Core`](Routeables.Core/README.md).

## Learn more

- [Getting started](docs/getting-started.md) and the [guides](docs/guides/routes.md): routes, typed routing, arguments, lifecycle, history, regions, modules, hosts.
- [Samples](Samples): the same Animals app on Blazor, MAUI, Avalonia and Uno.
- [Documentation site](https://cedric56.github.io/Routeables/) and API reference.
- [Contributing](CONTRIBUTING.md), [third-party notices](THIRD-PARTY-NOTICES.md).

Released under the [MIT License](LICENSE.txt).
