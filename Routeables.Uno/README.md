# Routeables.Uno

The [Uno Platform](https://platform.uno/) host of Routeables (Windows, Linux, macOS, WebAssembly, Android, iOS): the setup call, a routed view base, the region control and a list view for the menu. Targets **.NET 10**. It contains no XAML of its own.

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

## Dialogs and notifications

Optional, in the `Routeables.Uno.Services` namespace:

```csharp
Services = this.UseRouteables<MainViewModel>(services => services.UseRouteablesServices());
```

- Dialogs, content dialogs and task dialogs, drawn as `ContentDialog`s in the window the user last worked in. A window shows one at a time; a second waits for the first.
- Notifications: `InfoBar`s stacked at the top of the active window, closed after five seconds or by the user.
- `DialogView<TViewModel, TResult>`: the base of a modal view backed by a closeable view model, written in code (a WinUI XAML root cannot be generic). See `NoteView` in `Samples/XAnimals.Uno`.
- `IMessageBoxService` (`DefaultMessageBoxService`): pass your own to `UseRouteablesServices(messageBoxService)` to draw them another way.

Calling a service before a window has content throws an `InvalidOperationException` that says so. Contracts: [`Routeables.Services`](../Routeables.Services/README.md).

## Good to know

- Routeables does not use `Frame` or Uno.Extensions: do not mix them in one window.
- Not trimmer-checked yet (`IsAotCompatible` is not declared).
- Images in the sample are fetched by Uno itself; Wikimedia may refuse them without a User-Agent.

## Next

[Hosts guide](../docs/guides/hosts.md), sample `Samples/XAnimals.Uno`.
