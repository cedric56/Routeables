# Routeables.Core

What the four hosts share: view-model bases that know about routing, the contract of a view model that closes with a result, and information about the runtime. It builds on [`Routeables.Routing`](../Routeables.Routing/README.md). Targets **.NET 10**. A host references it, so an application usually gets it through the host.

## Which base?

| You write | Derive from |
| --- | --- |
| A page | nothing: a plain class that takes `IRouter` (add `IRouteable` for `CanLeaveAsync`, `OnEnteringAsync`...) |
| The menu or tab bar | `MainViewModelBase`: return the entries from `GetRouteables()` |
| The menu, with CommunityToolkit.Mvvm | `RouteableViewModel` (feature `;CommunityToolkit;`) |
| A view model shown in a dialog, closing with a result | `CloseableViewModel<TResult>` (CommunityToolkit) or `CloseableViewModelBase<TResult>` |
| Your own base (another MVVM library) | `RouteableViewModelBase` for a menu, `RouterViewModelBase` for back and forward only |

Register the menu with `services.RegisterMainRouteables<MainViewModel>(ServiceLifetime.Singleton)` (`Scoped` per circuit on Blazor Server). It implements `IMainRouteables`: the entries, the selected one, the current view, and the guard that stops a change of entry while a page blocks leaving.

The CommunityToolkit flavour is opt-in: declare it in the project file and `Routeables.Core.targets` adds the package and the sources.

```xml
<PropertyGroup>
  <RouteablesFeatures>;CommunityToolkit;</RouteablesFeatures>
</PropertyGroup>
```

## Runtime information

`Runtime`, `Desktop` and `Distribution` describe the host, the operating-system family and the distribution. Each host registers them; by hand: `services.RegisterEnvironments(Runtime.Avalonia)`. The values are `Avalonia`, `Maui`, `MauiHybrid`, `BlazorServer`, `BlazorWebAssembly`, `Uno` and `Unknown`.

Related: [`Routeables.Abstraction`](../Routeables.Abstraction/README.md), [`Routeables.Routing`](../Routeables.Routing/README.md), [hosts](../docs/guides/hosts.md).
