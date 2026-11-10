# Routeables.WinUI3

The [WinUI 3](https://learn.microsoft.com/windows/apps/winui/winui3/) (Windows App SDK) host of Routeables, packaged or unpackaged: the setup call, a routed view base, the region control and a list view for the menu. It is the Windows-only sibling of `Routeables.Uno`, with the same API (`Microsoft.UI.Xaml`), and contains no XAML of its own. Targets **.NET 10** (`net10.0-windows10.0.19041.0`).

```bash
dotnet add package Routeables.WinUI3
```

The package brings `Routeables` (routing, abstractions and the source generator) and `Routeables.Native` (system notifications): there is nothing else to add.

## Set up

```csharp
public App()
{
    InitializeComponent();
    Services = this.UseRouteables();
}

protected override void OnLaunched(LaunchActivatedEventArgs args) =>
    _window = this.OpenMainWindow(new MainPage());
```

An application with a menu gives its menu view model: `this.UseRouteables<MainViewModel>(r => ...)`. The window can be opened by the same call, `this.UseRouteables(r => ..., () => new MainPage(), window => window.Title = "My app")`, when the application already has a dispatcher in its constructor; in a WinUI 3 application it may not, and the call then says so: use `OpenMainWindow` in `OnLaunched`, as above. For another window, call `window.AttachRouteables()`.

## A view

```xml
<routeables:RouteableFor x:Class="MyApp.Views.HelloView"
    xmlns:routeables="using:Routeables.WinUI3" ...>
    <TextBlock Text="{Binding Greeting}" />
</routeables:RouteableFor>
```

```csharp
public sealed partial class HelloView : RouteableFor { public HelloView() => InitializeComponent(); }
```

A XAML root cannot be generic in WinUI, so XAML views use `RouteableFor`; views written in code can use `RouteableFor<TViewModel>`. The menu is a `RouteableMenuViewModelBase` and `<routeables:RouteableListView />` (or a `NavigationView` whose items name a route with `routeables:RouteableItem.Route`); the page is shown by `<routeables:RouteableRegion />`.

### Bindable view models

Put `[System.ComponentModel.Bindable(true)]` above every view model a view binds to. It takes the place of the `[Bindable]` of the platform and lets the XAML compiler generate the binding code, so `{Binding}` and `{x:Bind}` do not use reflection at run time (needed for trimming and Native AOT).

```csharp
// samples/Bases/XAnimals.Common/ViewModels/AnimalsViewModel.cs
[System.ComponentModel.Bindable(true)]
[Routeable("/hello")]
public class HelloViewModel : ViewModelBase { public string Greeting => "Hello"; }
```

Without it a binding to the view model still works through reflection, which the trimmer may remove.

## Dialogs and notifications

In the `Routeables.WinUI3.Services` namespace, registered by `UseRouteables`: dialogs, content dialogs and task dialogs as `ContentDialog`s in the window the user last worked in, toasts as `InfoBar`s at the top of the window, `DialogView<TViewModel, TResult>` for a modal view written in code.

System notifications (the notification center of Windows) are off until `services.WithAppNotifications()`; they use `AppNotificationManager` of the Windows App SDK through `Routeables.Native`. Windows reports a tap, not a dismissal.

## Good to know

- There is no system Back request on the desktop: a Back button is yours to draw (bind it to `IRouter.GoBackAsync`).
- Routeables does not use `Frame`: do not mix them in one window.
- Built on Windows only: on another OS the project builds an empty assembly so that a solution still restores.
