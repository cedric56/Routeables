# Routeables.Avalonia

The [Avalonia](https://avaloniaui.net/) host of Routeables (desktop, Android, iOS, browser): the setup call, a routed view base, the region control and a list box for the menu. Targets **.NET 10**.

```bash
dotnet add package Routeables.Avalonia
```

The package brings `Routeables` (routing, abstractions and the source generator): there is nothing else to add.

## Set up

```csharp
public static AppBuilder BuildAvaloniaApp() =>
    AppBuilder.Configure<App>()
        .UseRouteables<MainViewModel>(r => r.AddGeneratedRoutes())
        .UsePlatformDetect();
```

Or let the builder show it: `.UseRouteables<MainViewModel>(r => r.AddGeneratedRoutes(), view: lifetime => lifetime is IClassicDesktopStyleApplicationLifetime ? new MainWindow() : new MainView())` shows the view as soon as the lifetime is known, and the application needs no `OnFrameworkInitializationCompleted`. Otherwise, show the main view from `OnFrameworkInitializationCompleted`: `this.OpenMainView(...)` takes the window on desktop, or the view on Android (activity) and single-view platforms (browser), and gives it the menu as `DataContext`. `this.GetRouteableServices()` returns the container.

## A view

```csharp
// Samples/XAnimals.Avalonia/Views/Animals/DogDetailView.axaml.cs
public partial class DogDetailView : RouteableFor<DogDetailViewModel> { public DogDetailView() => InitializeComponent(); }
```

```xml
<!-- xmlns:routeables="using:Routeables.Avalonia" -->
<routeables:RouteableRegion Region="shell-content" />
```

`RouteableListBox` lists the entries of the main view model, selects the current one and disables locked ones.

## Tabs

A `TabControl` written in AXAML: each tab names its route, and the router builds the page.

```xml
<TabControl x:Name="Tabs">
    <TabItem Header="Cats" routeables:RouteableItem.Route="cats" />
    <TabItem Header="Dogs" routeables:RouteableItem.Route="dogs" />
</TabControl>
```

```csharp
Tabs.AttachRouteables(provider, region: "tabs");   // returns an IDisposable
```

Without `region`, the tabs follow the main history.

## Dialogs and notifications

Drawn with Avalonia controls (`Routeables.Avalonia.Services` namespace). `UseRouteables` already registers them, and the dialog windows take the icon of the window that opens them. To draw them another way, register your own `IMessageBoxService` in the block (such as the FluentAvalonia one of [`Reshapes`](../Reshapes/README.md)):

```csharp
AppBuilder.Configure<App>()
    .UseRouteables<MainViewModel>(services =>
    {
        services.AddSingleton<IMessageBoxService>(new MyMessageBoxService());   // optional
        services.AddGeneratedRoutes();
    })
    .UsePlatformDetect();
```

`UseRouteablesServices(icon)` is only for a dialog icon other than the one of the main window.

- Dialogs, content dialogs, task dialogs and notifications, through `IMessageBoxService` (`DefaultMessageBoxService`): modal windows on desktop, an overlay on single-view platforms (Android, iOS, browser).
- `DialogView<TViewModel, TResult>`: the base of a modal view backed by a closeable view model.

Contracts: [`Routeables.Services`](../Routeables.Services/README.md).

## Good to know

- No dependency on FluentAvalonia: the default dialogs are drawn with Avalonia controls. FluentAvalonia flavour: [`Reshapes`](../Reshapes/README.md).
- In Debug builds the container is validated on build, so a registration that cannot be built fails at start with its name.
- Also here: `RouteableDoubleClickBehavior`, `TopLevelStateManager`, `RouteableNullConverter`.

## Next

[Hosts guide](../docs/guides/hosts.md), samples `Samples/XAnimals.Avalonia` and its Desktop, Android, iOS and Browser heads.
