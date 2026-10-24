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
        .UseRouteables(services =>
        {
            services.AddGeneratedRoutes();
            services.RegisterMainRouteables<MainViewModel>(ServiceLifetime.Singleton);
        })
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

## Good to know

- No dependency on FluentAvalonia: the default dialogs are drawn with Avalonia controls. FluentAvalonia flavour: [`Reshapes`](../Reshapes/README.md).
- In Debug builds the container is validated on build, so a registration that cannot be built fails at start with its name.
- Also here: `RouteableDoubleClickBehavior`, `TopLevelStateManager`, `RouteableNullConverter`.

## Next

[`Routeables.Avalonia.Services`](../Routeables.Avalonia.Services/README.md) (dialogs, notifications), [Hosts guide](../docs/guides/hosts.md), samples `Samples/XAnimals.Avalonia` and its Desktop, Android, iOS and Browser heads.
