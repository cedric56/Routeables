# Hosts, services and samples

## View models in Core

`Routeables.Core` provides routing-aware view-model bases so a page that hosts other pages does not rebuild the plumbing:

- `RouterViewModelBase`: back/forward routing through the router's history.
- `MainViewModelBase`: a ready-to-use main view model; you only return the entries.
- `RouteableViewModelBase`: the abstract base under it; manages routing between a set of `IRouteableEntry` items (menus, tab bars), implements `IMainRouteables` and guards the change of the current item.
- `CloseableViewModelBase<TResult>`: a view model that can be closed and returns a result (dialogs).

The CommunityToolkit.Mvvm flavour is opt-in. Declare the feature you want in your project file and `Routeables.Core.targets` adds the packages and the matching source files:

```xml
<PropertyGroup>
  <RouteablesFeatures>;CommunityToolkit;</RouteablesFeatures>
  <!-- ;ReactiveUI; is recognised by the targets but no ReactiveUI sources ship yet -->
</PropertyGroup>
```

## Shared services

These interfaces live in `Routeables.Services` (namespace `Routeables.Services`) and are implemented by each host. Routing does not need them: a project that only routes references `Routeables.Abstraction` and `Routeables.Routing`, and the hosts bring `Routeables.Services` when they are used. `IRouteableBrokerService` stays in `Routeables.Abstraction` because the lock coordinator uses it.

| Interface | Purpose |
| --- | --- |
| `IRouteableDialogs` | **The one to inject**: gives the three services below as `MessageBox`, `ContentDialog` and `TaskDialog` |
| `IRouteableDialogService` | Message boxes and simple dialogs |
| `IRouteableContentDialogService` | Dialogs that host a view and a view model |
| `IRouteableTaskDialogService` | Multi-step / button-rich task dialogs |
| `IRouteableNotificationService` | In-app toast notifications |
| `IRouteableRequestedThemeService` | Light/dark theme switching |

## Hosts

| Host | Setup call | Notes |
| --- | --- | --- |
| Blazor ([`Routeables.Razor`](https://github.com/cedric56/Routeables/blob/master/Routeables.Razor/README.md)) | `services.UseRouteablesServer<MainViewModel>(r => ...)`, `UseRouteablesWebAssembly<MainViewModel>(...)` or `UseRouteablesHybrid<MainViewModel>(...)`, then `services.UseRouteablesServices()` for the dialogs, modals and notifications ([`Routeables.Razor.Services`](https://github.com/cedric56/Routeables/blob/master/Routeables.Razor.Services/README.md)) | Plain Razor, no component library (MudBlazor is optional, see [`Reshapes`](https://github.com/cedric56/Routeables/blob/master/Reshapes/README.md)). Keeps the browser address in step with the router (`RouteableUrlSync`). Base components: `RouteableFor<TView, TViewModel>`, `RouteableContentFor<T>`, `RouteableModalFor<...>`, `RouteableRegion` |
| .NET MAUI ([`Routeables.Maui`](https://github.com/cedric56/Routeables/blob/master/Routeables.Maui/README.md)) | `builder.UseRouteables<MainViewModel>(r => ...)`, then `.UseRouteablesServices()` ([`Routeables.Maui.Services`](https://github.com/cedric56/Routeables/blob/master/Routeables.Maui.Services/README.md)) | Targets `net10.0` plus Android, iOS, Mac Catalyst and Windows. Includes `RouteablesShell`, `RouteableRegion` and `RouteableFor`. CommunityToolkit.Maui popups are optional (`UseCommunityToolkit`, see [`Reshapes`](https://github.com/cedric56/Routeables/blob/master/Reshapes/README.md)) |
| Avalonia ([`Routeables.Avalonia`](https://github.com/cedric56/Routeables/blob/master/Routeables.Avalonia/README.md)) | `AppBuilder.UseRouteables<MainViewModel>(r => ...)`, with `services.UseRouteablesServices(icon)` inside ([`Routeables.Avalonia.Services`](https://github.com/cedric56/Routeables/blob/master/Routeables.Avalonia.Services/README.md)) | `RouteableFor<TViewModel>`, `RouteableRegion`, `RouteableListBox`, modal dialogs, themes, replaceable `IMessageBoxService` (FluentAvalonia in [`Reshapes`](https://github.com/cedric56/Routeables/blob/master/Reshapes/README.md)) |
| Uno Platform, preview ([`Routeables.Uno`](https://github.com/cedric56/Routeables/blob/master/Routeables.Uno/README.md)) | `Application.UseRouteables<MainViewModel>(r => ...)` and `Application.OpenMainWindow(page)`, with `services.UseRouteablesServices()` inside (`Routeables.Uno.Services`) | `RouteableFor`, `RouteableFor<TViewModel>`, `RouteableRegion`, `RouteableListView`, `ContentDialog` dialogs, `InfoBar` notifications, themes, replaceable `IMessageBoxService` |

The Blazor, MAUI and Avalonia hosts share one contract, checked by `HostContractTests` (Uno follows the same rules, but is not covered by that test yet): the same services (router, injection rules), the same injection rule (a value attribute never falls back to the container) and one main view model for the whole host. While developing, the Avalonia host (Debug builds) and the MAUI sample build the container with `ValidateOnBuild`, so a registration that cannot be built fails at start with its name; ASP.NET Core does the same by default in the Development environment.

`Runtime` identifies the host (`Runtime.Avalonia`, `Uno`, `Maui`, `MauiHybrid`, `BlazorServer`, `BlazorWebAssembly`) and is available through `IRuntime`.

## Samples

The [`Samples`](https://github.com/cedric56/Routeables/tree/master/Samples) folder contains the same Animals app on several stacks, sharing one `XAnimals.ViewModels` project:

| Sample | Stack |
| --- | --- |
| `XAnimals.Birds` | The shared part of a [module](routes.md#modules): a page, `AddBirdsModule()`, its view models and data. Its views are in `XAnimals.Razor.Birds`, `XAnimals.Avalonia.Birds`, `XAnimals.Maui.Birds` and `XAnimals.Uno.Birds`, which the heads reference (they draw with `XAnimals.<Platform>.Controls`). `XAnimals.ViewModels` calls it in `RegisterViewModels` (`AddAnimalModules()`) |
| `XAnimals.Common` | What the view models and the modules share (bases, `Animal`, the list and detail every animal kind derives from, icons): a module references it, not `XAnimals.ViewModels`, which references the module |
| `XAnimals.ViewModels` | Shared view models (uses `[RouteableViewModel]`) |
| `XAnimals.Avalonia` (+ `.Desktop`, `.Android`, `.iOS`, `.Browser`) | Avalonia |
| `XAnimals.Uno` | Uno Platform (desktop head; preview) |
| `XAnimals.Maui`, `XAnimals.MauiHybrid` | .NET MAUI and MAUI Blazor Hybrid |
| `XAnimals.Razor`, `XAnimals.BlazorWebAssembly`, `Xanimals.BlazorApp` | Blazor |

> [!NOTE]
> The samples are still in progress.
