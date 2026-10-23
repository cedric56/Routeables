# Routeables.Avalonia

`Routeables.Avalonia` is the [Avalonia UI](https://avaloniaui.net/) host of Routeables (desktop, Android, iOS and browser). It provides the
application bootstrap, the routed view base, the region control, a list box for the main entries and the theme service. The dialogs and
notifications are optional, in [`Routeables.Avalonia.Services`](../Routeables.Avalonia.Services/README.md).

## Requirements

Targets **.NET 10** (`net10.0`), version `1.0.0-preview.1`. It references `Avalonia` and `Routeables.Core`. It does not depend on FluentAvalonia
or MessageBox.Avalonia: the default dialogs are drawn with Avalonia controls. A FluentAvalonia implementation is a separate project,
[`Reshapes.Avalonia.Fluent`](../Reshapes/README.md).

```xml
<ItemGroup>
  <ProjectReference Include="..\Routeables.Avalonia\Routeables.Avalonia.csproj" />
</ItemGroup>
```

```bash
dotnet build Routeables.Avalonia/Routeables.Avalonia.csproj
```

## Application startup

`AppBuilder.UseRouteables(...)` builds the container, registers the Routeables core and attaches it to the `AppBuilder` you already
have, so the application keeps the usual Avalonia shape: its own constructor, `AppBuilder.Configure<App>()`. The application still creates
its main window in its own lifetime code.

```csharp
using Routeables.Avalonia;
using Routeables.Avalonia.Services;

public static AppBuilder BuildAvaloniaApp() =>
    AppBuilder.Configure<App>()
        .UseRouteables(services =>
        {
            services.UseRouteablesServices("avares://MyApp/Assets/icon.ico");   // optional: dialogs, notifications
            services.AddGeneratedRoutes();
            services.RegisterMainRouteables<MainViewModel>(ServiceLifetime.Singleton);
        })
        .UsePlatformDetect();
```

An `Application` that needs the container reads it with `this.GetRouteableServices()`. To show the main view, call `this.OpenMainView(...)` from
`OnFrameworkInitializationCompleted`: it takes the `Window` on a desktop or the view on a single-view platform, and gives it the menu as its `DataContext`.

Parameters: `configureExternalServices` runs before the provider is built; `afterBuild` receives the provider; `onDispose` runs when the active
top-level closes, just before the provider is disposed.

The builder registers the router (singleton), the arguments factory, the environment information, property injection
(`[RouteableParameter]` and `[InjectProperty]`) and the theme service. The dialogs, content dialogs, task dialogs and notifications are **optional**:
`services.UseRouteablesServices(icon)` from [`Routeables.Avalonia.Services`](../Routeables.Avalonia.Services/README.md) registers them, with the `IMessageBoxService` that draws them.
In Debug builds the container is built with `ValidateOnBuild`, so a registration that cannot be built fails at start with its name. When the window
is first active the builder selects the first main entry.

## Routed views

Derive a view from `RouteableFor<TViewModel>`: it maps `DataContext` to `Context` and implements `IRouteable`, `IInitializeAsync` and
`IDisposable` with defaults you can override. `IsLocked` is true when the view's context is a main view model whose entries are all locked.

```csharp
public partial class HomeView : RouteableFor<HomeViewModel> { public HomeView() => InitializeComponent(); }
```

Registrations are yours: use `[RouteableView]` with `AddGeneratedRoutes()` or `RegisterRoute`.

A zone that shows a region is a `RouteableRegion`:

```xml
<!-- xmlns:routeables="using:Routeables.Avalonia" -->
<routeables:RouteableRegion Region="detail" />
```

## Services provided by the host

| Contract | Implementation |
| --- | --- |
| `IRouteableRequestedThemeService` | Sets `Application.RequestedThemeVariant` to light, dark or default |

The dialogs, content dialogs, task dialogs and notifications, with `IMessageBoxService` and its `DefaultMessageBoxService`, are in
[`Routeables.Avalonia.Services`](../Routeables.Avalonia.Services/README.md).

## Other helpers

- `RouteableListBox` is a `ListBox` bound to the entries of an `IMainRouteables`; it selects the current entry and disables locked ones.
- `RouteableDoubleClickBehavior` runs a command when a control is double-tapped (`Command`, `CommandParameter`).
- `TopLevelStateManager` follows the active `TopLevel`, so dialogs and notifications target the current window.
- `DialogView<TViewModel, TResult>`, the base of a modal view, is in `Routeables.Avalonia.Services`.
- `RouteableNullConverter` is a value converter that gives true for null (a blank string is not null here, unlike on MAUI).

For the shared view-model bases see [`Routeables.Core`](../Routeables.Core/README.md); for the routing API,
[`Routeables.Abstraction`](../Routeables.Abstraction/README.md) and [`Routeables.Routing`](../Routeables.Routing/README.md). Samples:
`Samples/XAnimals.Avalonia` and its Desktop, Android, iOS and Browser heads.
