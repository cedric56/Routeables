# Routeables.Uno

The [Uno Platform](https://platform.uno/) host of Routeables (Windows, Linux, macOS, WebAssembly, Android, iOS): the setup call, a routed view base, the region control and a list view for the menu. Targets **.NET 10**. It contains no XAML of its own.

```bash
dotnet add package Routeables.Uno
```

The package brings `Routeables` (routing, abstractions and the source generator): there is nothing else to add.

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

The window can be opened by the same call: `Services = this.UseRouteables<MainViewModel>(r => r.AddGeneratedRoutes(), () => new MainPage(), window => window.Title = "My app");` in the constructor of `App` builds the container and creates the window as the application runs, with no `OnLaunched` to write.

`OpenMainWindow` creates the window, gives the page the menu as `DataContext`, selects the first entry and disposes the container when the window closes. For another window, call `window.AttachRouteables()`.

## A view

```xml
<!-- Samples/XAnimals.Uno/Views/Animals/DogDetailView.xaml -->
<routeables:RouteableFor x:Class="XAnimals.Uno.Views.DogDetailView"
    xmlns:routeables="using:Routeables.Uno" ...>
    <local:AnimalDetail />
</routeables:RouteableFor>
```

```csharp
public sealed partial class DogDetailView : RouteableFor { public DogDetailView() => InitializeComponent(); }
```

A XAML root cannot be generic in WinUI, so XAML views use `RouteableFor`; views written in code can use `RouteableFor<TViewModel>`.

The menu is a `MainViewModelBase` and `<routeables:RouteableListView />`; the current view is `<ContentControl Content="{Binding CurrentView}" />`. A region is `<routeables:RouteableRegion Region="detail" />`.

## Navigation

A `NavigationView` written in XAML: each item names its route, and the router builds the page.

```xml
<NavigationView x:Name="Nav">
    <NavigationViewItem Content="Cats" routeables:RouteableItem.Route="cats" />
    <NavigationViewItem Content="Dogs" routeables:RouteableItem.Route="dogs" />
</NavigationView>
```

```csharp
Nav.AttachRouteables(provider, region: "tabs");   // returns an IDisposable
```

Without `region`, the items follow the main history.

## Dialogs and notifications

In the `Routeables.Uno.Services` namespace. `UseRouteables` already registers them; call `UseRouteablesServices(messageBoxService)` yourself only to draw them another way.

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
