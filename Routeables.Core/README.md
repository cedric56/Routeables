# Routeables

Routing between view models for Blazor, .NET MAUI, Avalonia and Uno. This package is what the four hosts share, and an application gets it through the package of its host (`Routeables.Razor`, `Routeables.Maui`, `Routeables.Avalonia`, `Routeables.Uno`). Targets **.NET 10**.

It is one package built from several projects of the repository:

| Part | What it holds |
| --- | --- |
| [`Abstraction`](../Routeables.Abstraction/README.md) | The contracts (`IRouter`, `IRouteable`, route arguments) and the attributes that declare routes |
| [`Routing`](../Routeables.Routing/README.md) | The router: history, route locking, registration |
| `Core` (this folder) | The view-model bases that know about routing, and information about the runtime |
| [`Regions`](../Routeables.Regions/README.md) | Several areas of the screen, each with its own history |
| [`Segments`](../Routeables.Segments/README.md) | Routing by segments (`Orders?id=5/Detail`) |
| [`Locking`](../Routeables.Locking/README.md) | Pages that block leaving, a locked menu |
| [`Services`](../Routeables.Services/README.md) | The contracts of the dialogs, notifications and theme (the host packages implement them) |
| [`Generated`](../Routeables.Generated/README.md) | The source generator, as an analyzer: `AddGeneratedRoutes()` and the typed `GoTo…Async` methods |

## Which base?

| You write | Derive from |
| --- | --- |
| A page | nothing: a plain class that takes `IRouter` (add `IRouteable` for `CanLeaveAsync`, `OnEnteringAsync`...) |
| The menu or tab bar | `MainViewModelBase`: return the entries from `GetRouteables()` |
| The menu, with CommunityToolkit.Mvvm or ReactiveUI | `RouteableViewModel` (feature `;CommunityToolkit;` or `;ReactiveUI;`) |
| A view model shown in a dialog, closing with a result | `CloseableViewModel<TResult>` (CommunityToolkit or ReactiveUI) or `CloseableViewModelBase<TResult>` |
| Your own base (another MVVM library) | `RouteableViewModelBase` for a menu, `RouterViewModelBase` for back and forward only |

Register the menu with `UseRouteables<MainViewModel>(...)` on your host, or `services.RegisterMainRouteables<MainViewModel>()`. The host chooses the lifetime (`Scoped` per circuit on Blazor, `Singleton` elsewhere). It implements `IMainRouteables`: the entries, the selected one, the current view, and the guard that stops a change of entry while a page blocks leaving.

The CommunityToolkit.Mvvm and ReactiveUI flavours are opt-in sources, not part of the assembly. Add the package of your MVVM library to your project, then declare the flavour in the project file: `Routeables.targets` adds its sources.

```xml
<PropertyGroup>
  <RouteablesFeatures>;CommunityToolkit;</RouteablesFeatures>   <!-- or ;ReactiveUI; (one of them) -->
</PropertyGroup>
<ItemGroup>
  <PackageReference Include="CommunityToolkit.Mvvm" Version="..." />   <!-- or ReactiveUI and ReactiveUI.SourceGenerators -->
</ItemGroup>
```

## Runtime information

`Runtime`, `Desktop` and `Distribution` describe the host, the operating-system family and the distribution. Each host registers them; by hand: `services.RegisterEnvironments(Runtime.Avalonia)`. The values are `Avalonia`, `Maui`, `MauiHybrid`, `BlazorServer`, `BlazorWebAssembly`, `Uno` and `Unknown`.

Related: [hosts](../docs/guides/hosts.md), [getting started](../docs/getting-started.md).
