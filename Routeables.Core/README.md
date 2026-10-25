# Routeables

Routing between view models for Blazor, .NET MAUI, Avalonia, Uno and WPF. This package is what the hosts share, and an application gets it through the package of its host (`Routeables.Razor`, `Routeables.Maui`, `Routeables.Avalonia`, `Routeables.Uno`, `Routeables.Wpf`). Targets **.NET 10**.

It is one package built from several projects of the repository:

| Part | What it holds |
| --- | --- |
| [`Abstraction`](../Routeables.Abstraction/README.md) | The contracts (`IRouter`, `IRouteable`, route arguments) and the attributes that declare routes |
| [`Routing`](../Routeables.Routing/README.md) | The router: history, route locking, registration |
| `Core` (this folder) | The view-model bases that know about routing, and information about the runtime |
| [`Regions`](../Routeables.Regions/README.md) | Several areas of the screen, each with its own history |
| [`Segments`](../Routeables.Segments/README.md) | Routing by segments (`/Bears?SearchText=polar`) |
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

Register the menu with `UseRouteables<MainViewModel>(...)` on your host, or `services.RegisterMainViewModel<MainViewModel>()`. The host chooses the lifetime (`Scoped` per circuit on Blazor, `Singleton` elsewhere). It implements `IMainViewModel`: the entries, the selected one, the current view, and the guard that stops a change of entry while a page blocks leaving. To write your own instead, `IMainViewModel` asks for five members (`Routeables`, `Routeable`, `CurrentView` and the events `CurrentViewChanged` and `LockChanged`); the title, `Lock`/`Unlock` and the first selection have defaults, and `NotifyLockChanged` is where your class raises `LockChanged`.

**None of the bases implements `INotifyPropertyChanged` (`MainViewModelBase`, `RouteableViewModelBase`, `CloseableViewModelBase`, `RouterViewModelBase`): the class that derives from one must.** Put `[INotifyPropertyChanged]` (or `[ObservableObject]`) of CommunityToolkit.Mvvm or `[IReactiveObject]` of ReactiveUI on your `partial` class, or implement the interface by hand, and override `NotifyPropertyChanged` and `RaiseCanExecutesChanged`. Those generators add the interface only once in a hierarchy, which is why the bases leave it to you; `RouteableViewModel` and `CloseableViewModel<TResult>` (the flavours) already do it.

The CommunityToolkit.Mvvm and ReactiveUI flavours are opt-in sources, not part of the assembly. Declare the flavour in the project file: `Routeables.targets` adds its sources and the packages it uses.

```xml
<PropertyGroup>
  <RouteablesFeatures>;CommunityToolkit;</RouteablesFeatures>   <!-- or ;ReactiveUI; (one of them) -->
</PropertyGroup>
```

`Routeables.targets` also adds the packages the flavour uses (`CommunityToolkit.Mvvm`; `ReactiveUI` and `ReactiveUI.SourceGenerators`), unless your project already references them: then your version wins. With central package management (`ManagePackageVersionsCentrally`) nothing is added: declare the packages in your `Directory.Packages.props`. To choose another version without referencing the package, set `RouteablesCommunityToolkitMvvmVersion`, `RouteablesReactiveUIVersion` or `RouteablesReactiveUISourceGeneratorsVersion`.

## Runtime information

`Runtime`, `Desktop` and `Distribution` describe the host, the operating-system family and the distribution. Each host registers them; by hand: `services.RegisterEnvironments(Runtime.Avalonia)`. The values are `Avalonia`, `Maui`, `MauiHybrid`, `BlazorServer`, `BlazorWebAssembly`, `Uno` and `Unknown`.

Related: [hosts](../docs/guides/hosts.md), [getting started](../docs/getting-started.md).
