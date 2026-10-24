# Routeables.Uno

The [Uno Platform](https://platform.uno/) host of Routeables (Windows, Linux, macOS, WebAssembly, Android, iOS): the setup call, a routed view base, the region control and a list view for the menu. Targets **.NET 10**. It contains no XAML of its own.

> [!WARNING]
> Preview. This host is type-checked and built by CI ("Build Uno") but has not been run on a device yet. Please report what you find.

```xml
<ItemGroup>
  <ProjectReference Include="..\Routeables\Routeables.Uno\Routeables.Uno.csproj" />
  <ProjectReference Include="..\Routeables\Routeables.Generated\Routeables.Generated.csproj"
                    OutputItemType="Analyzer" ReferenceOutputAssembly="false" />
</ItemGroup>
```

## Set up

```csharp
public App()
{
    InitializeComponent();
    Services = this.UseRouteables<MainViewModel>(r => r.AddGeneratedRoutes());
}

protected override void OnLaunched(LaunchActivatedEventArgs args) =>
    _window = this.OpenMainWindow(new MainPage());
```

`OpenMainWindow` creates the window, gives the page the menu as `DataContext`, selects the first entry and disposes the container when the window closes. For another window, call `window.AttachRouteables()`.

## A view

```xml
<routeables:RouteableFor x:Class="MyApp.Views.HelloView"
    xmlns:routeables="using:Routeables.Uno" ...>
    <TextBlock Text="{Binding Greeting}" />
</routeables:RouteableFor>
```

```csharp
public sealed partial class HelloView : RouteableFor { public HelloView() => InitializeComponent(); }
```

A XAML root cannot be generic in WinUI, so XAML views use `RouteableFor`; views written in code can use `RouteableFor<TViewModel>`.

The menu is a `MainViewModelBase` and `<routeables:RouteableListView />`; the current view is `<ContentControl Content="{Binding CurrentView}" />`. A region is `<routeables:RouteableRegion Region="detail" />`.

## Good to know

- Routeables does not use `Frame` or Uno.Extensions: do not mix them in one window.
- Not trimmer-checked yet (`IsAotCompatible` is not declared).
- Images in the sample are fetched by Uno itself; Wikimedia may refuse them without a User-Agent.

## Next

[`Routeables.Uno.Services`](../Routeables.Uno.Services/README.md) (dialogs, notifications), [Hosts guide](../docs/guides/hosts.md), sample `Samples/XAnimals.Uno`.
