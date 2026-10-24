# Routeables.Avalonia

The [Avalonia](https://avaloniaui.net/) host of Routeables (desktop, Android, iOS, browser): the setup call, a routed view base, the region control and a list box for the menu. Targets **.NET 10**.

```xml
<ItemGroup>
  <ProjectReference Include="..\Routeables\Routeables.Avalonia\Routeables.Avalonia.csproj" />
  <ProjectReference Include="..\Routeables\Routeables.Generated\Routeables.Generated.csproj"
                    OutputItemType="Analyzer" ReferenceOutputAssembly="false" />
</ItemGroup>
```

## Set up

```csharp
public static AppBuilder BuildAvaloniaApp() =>
    AppBuilder.Configure<App>()
        .UseRouteables<MainViewModel>(r => r.AddGeneratedRoutes())
        .UsePlatformDetect();
```

Show the main view from `OnFrameworkInitializationCompleted`: `this.OpenMainView(...)` takes the window on desktop or the view on a single-view platform, and gives it the menu as `DataContext`. `this.GetRouteableServices()` returns the container.

## A view

```csharp
public partial class HomeView : RouteableFor<HomeViewModel> { public HomeView() => InitializeComponent(); }
```

```xml
<!-- xmlns:routeables="using:Routeables.Avalonia" -->
<routeables:RouteableRegion Region="detail" />
```

`RouteableListBox` lists the entries of the main view model, selects the current one and disables locked ones.

## Dialogs and notifications

Optional, drawn with Avalonia controls (`Routeables.Avalonia.Services` namespace). Call `UseRouteablesServices` where you register the routes:

```csharp
AppBuilder.Configure<App>()
    .UseRouteables<MainViewModel>(services =>
    {
        services.UseRouteablesServices("avares://MyApp/Assets/icon.ico");
        services.AddGeneratedRoutes();
    })
    .UsePlatformDetect();
```

The icon is the one of the windows that are drawn. To change the look, pass your own `IMessageBoxService` (such as the FluentAvalonia one of [`Reshapes`](../Reshapes/README.md)).

- Dialogs, content dialogs, task dialogs and notifications, through `IMessageBoxService` (`DefaultMessageBoxService`): modal windows on desktop, an overlay on single-view platforms (Android, iOS, browser).
- `DialogView<TViewModel, TResult>`: the base of a modal view backed by a closeable view model.

Contracts: [`Routeables.Services`](../Routeables.Services/README.md).

## Good to know

- No dependency on FluentAvalonia: the default dialogs are drawn with Avalonia controls. FluentAvalonia flavour: [`Reshapes`](../Reshapes/README.md).
- In Debug builds the container is validated on build, so a registration that cannot be built fails at start with its name.
- Also here: `RouteableDoubleClickBehavior`, `TopLevelStateManager`, `RouteableNullConverter`.

## Next

[Hosts guide](../docs/guides/hosts.md), samples `Samples/XAnimals.Avalonia` and its Desktop, Android, iOS and Browser heads.
