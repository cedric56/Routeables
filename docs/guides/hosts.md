# Hosts, services and samples

## View models in Core

`Routeables.Core` provides routing-aware view-model bases so a page that hosts other pages does not rebuild the plumbing:

- `RouterViewModelBase`: back/forward routing through the router's history.
- `MainViewModelBase`: a ready-to-use main view model; you only return the entries.
- `RouteableViewModelBase`: the abstract base under it; manages routing between a set of `IRouteableEntry` items (menus, tab bars), implements `IMainViewModel` and guards the change of the current item.
- `CloseableViewModelBase<TResult>`: a view model that can be closed and returns a result (dialogs).

The CommunityToolkit.Mvvm and ReactiveUI flavours are opt-in sources: they are not in the assembly. Declare the feature in your project file; `Routeables.targets` (in the `Routeables` package) adds the matching source files and the packages they use:

```xml
<PropertyGroup>
  <RouteablesFeatures>;CommunityToolkit;</RouteablesFeatures>   <!-- or ;ReactiveUI; (one of them) -->
</PropertyGroup>
```

`Routeables.targets` also adds the packages the flavour uses (`CommunityToolkit.Mvvm`; `ReactiveUI` and `ReactiveUI.SourceGenerators`), unless your project already references them: then your version wins. With central package management (`ManagePackageVersionsCentrally`) nothing is added: declare the packages in your `Directory.Packages.props`. To choose another version without referencing the package, set `RouteablesCommunityToolkitMvvmVersion`, `RouteablesReactiveUIVersion` or `RouteablesReactiveUISourceGeneratorsVersion`.

## Shared services

These interfaces live in `Routeables.Services` (namespace `Routeables.Services`) and are implemented by each host. Routing does not need them: a project that only routes references `Routeables.Abstraction` and `Routeables.Routing`, and the hosts bring `Routeables.Services` when they are used. `IRouteableBrokerService` stays in `Routeables.Abstraction` because the lock coordinator uses it.

| Interface | Purpose |
| --- | --- |
| `IRouteableDialogService` | Message boxes and simple dialogs |
| `IRouteableContentDialogService` | Dialogs that host a view and a view model |
| `IRouteableTaskDialogService` | Multi-step / button-rich task dialogs |
| `IRouteableNotificationService` | In-app toast notifications |
| `IRouteableRequestedThemeService` | Light/dark theme switching |
| `IRouteableDispatcher` | Runs work on the thread of the interface (`Post`, `InvokeAsync`, `CheckAccess`), from any thread |

## Hosts

| Host | Setup call | Notes |
| --- | --- | --- |
| Blazor ([`Routeables.Razor`](https://github.com/cedric56/Routeables/blob/master/Routeables.Razor/README.md)) | `services.UseRouteablesServer<MainViewModel>(r => ...)`, `UseRouteablesWebAssembly<MainViewModel>(...)` or `UseRouteablesHybrid<MainViewModel>(...)` | Plain Razor, no component library (MudBlazor is optional, see [`Reshapes`](https://github.com/cedric56/Routeables/blob/master/Reshapes/README.md)). Keeps the browser address in step with the router (`RouteableUrlSync`). Base components: `RouteableFor<TView, TViewModel>`, `RouteableContentFor<T>`, `RouteableModalFor<...>`, `RouteableRegion` |
| .NET MAUI ([`Routeables.Maui`](https://github.com/cedric56/Routeables/blob/master/Routeables.Maui/README.md)) | `builder.UseRouteables<MainViewModel>(r => ...)` | Targets `net10.0` plus Android, iOS, Mac Catalyst and Windows. Includes `RouteablesShell`, `RouteableRegion` and `RouteableFor`. CommunityToolkit.Maui popups are optional (`UseCommunityToolkit`, see [`Reshapes`](https://github.com/cedric56/Routeables/blob/master/Reshapes/README.md)) |
| Avalonia ([`Routeables.Avalonia`](https://github.com/cedric56/Routeables/blob/master/Routeables.Avalonia/README.md)) | `AppBuilder.UseRouteables<MainViewModel>(r => ...)`, with `view:` to show the main view (or `Application.OpenMainView(view)`) | `RouteableFor<TViewModel>`, `RouteableRegion`, `RouteableListBox`, modal dialogs, themes, replaceable `IMessageBoxService` (FluentAvalonia in [`Reshapes`](https://github.com/cedric56/Routeables/blob/master/Reshapes/README.md)) |
| Uno Platform ([`Routeables.Uno`](https://github.com/cedric56/Routeables/blob/master/Routeables.Uno/README.md)) | `Application.UseRouteables<MainViewModel>(r => ..., () => new MainPage())` (or `UseRouteables(r => ...)` and `Application.OpenMainWindow(page)` in `OnLaunched`) | `RouteableFor`, `RouteableFor<TViewModel>`, `RouteableRegion`, `RouteableListView`, `ContentDialog` dialogs, `InfoBar` notifications, themes, replaceable `IMessageBoxService` |
| WPF ([`Routeables.Wpf`](https://github.com/cedric56/Routeables/blob/master/Routeables.Wpf/README.md)) | `Application.UseRouteables<MainViewModel>(r => ..., () => new MainView())` (or `UseRouteables(r => ...)` and `Application.OpenMainWindow(view)`) | `net10.0-windows`. `RouteableFor`, `RouteableFor<TViewModel>`, `RouteableRegion`, `RouteableRegionItems`, `RouteableListBox`, dialogs in owned windows, notifications in small windows, Fluent themes, replaceable `IMessageBoxService`. Shares the dialog and notification logic with the other hosts (`Routeables.Services`) |

The Razor, MAUI, Avalonia and Uno hosts share one contract, checked by `HostContractTests` (WPF is not in it: the test project targets `net10.0` and cannot reference a Windows-only project): the same services (router, injection rules), the same injection rule (a value attribute never falls back to the container) and one main view model for the whole host. While developing, the Avalonia host (Debug builds) and the MAUI sample build the container with `ValidateOnBuild`, so a registration that cannot be built fails at start with its name; ASP.NET Core does the same by default in the Development environment.

`Runtime` identifies the host (`Runtime.Avalonia`, `Uno`, `Wpf`, `Maui`, `MauiHybrid`, `BlazorServer`, `BlazorWebAssembly`) and is available through `IRuntime`.

## Samples

The [`Samples`](https://github.com/cedric56/Routeables/tree/master/Samples) folder contains the same Animals app on several stacks, sharing one `XAnimals.ViewModels` project:

| Sample | Stack |
| --- | --- |
| `XAnimals.Birds` | The shared part of a [module](routes.md#modules): a page, `AddBirdsModule()`, its view models and data. Its views are in `XAnimals.Razor.Birds`, `XAnimals.Avalonia.Birds`, `XAnimals.Maui.Birds`, `XAnimals.Uno.Birds` and `XAnimals.Wpf.Birds`, which the heads reference (they draw with `XAnimals.<Platform>.Controls`). `XAnimals.ViewModels` calls it in `RegisterViewModels` (`AddAnimalModules()`) |
| `XAnimals.Common` | What the view models and the modules share (bases, `Animal`, the list and detail every animal kind derives from, icons): a module references it, not `XAnimals.ViewModels`, which references the module |
| `XAnimals.ViewModels` | Shared view models (uses `[RouteableViewModel]`) |
| `XAnimals.Avalonia` (+ `.Desktop`, `.Android`, `.iOS`, `.Browser`) | Avalonia |
| `XAnimals.Uno` | Uno Platform (desktop head) |
| `XAnimals.Wpf` | WPF (Windows): every page of the other samples, with `XAnimals.Wpf.Controls` and the Birds module `XAnimals.Wpf.Birds` |
| `XAnimals.Maui`, `XAnimals.MauiHybrid` | .NET MAUI and MAUI Blazor Hybrid |
| `XAnimals.Razor`, `XAnimals.BlazorWebAssembly`, `Xanimals.BlazorApp` | Blazor |

> [!NOTE]
> The samples are still in progress.
