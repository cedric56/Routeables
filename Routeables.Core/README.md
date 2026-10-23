# Routeables.Core

`Routeables.Core` contains the building blocks that the three UI hosts share: routing-aware view-model bases, the contract of a view
model that closes with a result, and information about the runtime. It builds on
[`Routeables.Routing`](../Routeables.Routing/README.md) and [`Routeables.Abstraction`](../Routeables.Abstraction/README.md).

## Requirements

Targets **.NET 10** (`net10.0`), version `1.0.0-preview.1`. It references `Routeables.Routing` and `CommunityToolkit.Mvvm` (the toolkit
flavour is opt-in, see below).

```xml
<ItemGroup>
  <ProjectReference Include="..\Routeables.Core\Routeables.Core.csproj" />
</ItemGroup>
```

```bash
dotnet build Routeables.Core/Routeables.Core.csproj
```

The host projects reference Core, so an application usually gets it through the host.

## View-model bases

### Which one?

| You write | Derive from | Needs |
| --- | --- | --- |
| **A page** (the usual view model) | Nothing: a plain class that takes `IRouter` in its constructor. Add `IRouteable` for the hooks (`CanLeaveAsync`, `OnEnteringAsync`...). | |
| **The menu or tab bar** | `MainViewModelBase` | Nothing |
| The menu, with CommunityToolkit.Mvvm | `RouteableViewModel` | `;CommunityToolkit;` feature (see below) |
| **A view model shown in a dialog** (it closes with a result) | `CloseableViewModel<TResult>` | `;CommunityToolkit;` feature |
| The same, without CommunityToolkit.Mvvm | `CloseableViewModelBase<TResult>` | Nothing |
| Your own base (another MVVM library) | `RouteableViewModelBase` for a menu, `RouterViewModelBase` for back and forward only | Nothing |

Only the menu and the dialogs need a base class. `RouteableViewModel` is the CommunityToolkit flavour of the menu base: it has nothing to do with the `[RouteableViewModel]` attribute, which asks the generator for a typed route method.

### What each one gives

- `RouterViewModelBase` takes an `IRouter` and gives `GoBackAsync`, `GoForwardAsync`, `CanGoBack` and `CanGoForward`. A derived type overrides
  `OnViewModelChanged` and `RaiseCanExecutesChanged` to refresh the UI when the history changes.
- `MainViewModelBase` is a main view model that works as it is: derive from it and return the entries from `GetRouteables()` (no MVVM library needed).
- `RouteableViewModelBase` is the abstract base under it, for a main view model (a menu or a tab bar). It implements `IMainRouteables`: a collection of
  `IRouteableEntry` entries, the selected entry, the current view, `CurrentViewChanged` and `LockChanged`, and it guards the change of
  the current entry. Register it with `services.RegisterMainRouteables<T>(Scoped or Singleton)`.
- `CloseableViewModelBase<TResult>` is a closeable view model without routing: give it a title and a close command, and call
  `Close(value)` to raise `CloseRequested`.

The hooks of these bases have defaults; override only what a view model needs.

### MVVM flavours

The CommunityToolkit.Mvvm flavour (`RouteableViewModel`, `CloseableViewModel<TResult>`, in `Community/`) is
compiled into your project only when you ask for it. Declare the feature in the project file and `build/Routeables.Core.targets` adds the
package and the source files:

```xml
<PropertyGroup>
  <RouteablesFeatures>;CommunityToolkit;</RouteablesFeatures>
</PropertyGroup>
```

The targets also know a `;ReactiveUI;` feature, but this repository contains no ReactiveUI sources yet, so it adds the packages and nothing else.

## Runtime information

`Runtime`, `Desktop` and `Distribution` describe the host, the operating-system family and the detected distribution. `IRuntime`,
`IDesktop` and `IDistribution` expose them. Each host registers them for you; to do it by hand:

```csharp
using Routeables.Core;

services.RegisterEnvironments(Runtime.Avalonia);
```

The values are `Runtime.Avalonia`, `Maui`, `MauiHybrid`, `BlazorServer`, `BlazorWebAssembly` and `Unknown`. `IsRazorRuntime()` tells whether the
host is Blazor or MAUI Hybrid. `Runtime.Create`, `Desktop.Create` and `Distribution.Create` accept custom identifiers.

## Related projects

- [`Routeables.Abstraction`](../Routeables.Abstraction/README.md): the contracts.
- [`Routeables.Routing`](../Routeables.Routing/README.md): the router.
- Hosts: [Avalonia](../Routeables.Avalonia/README.md), [MAUI](../Routeables.Maui/README.md), [Razor](../Routeables.Razor/README.md).
