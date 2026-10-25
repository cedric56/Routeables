# Routeables.Wpf

The WPF host of Routeables (Windows, `net10.0-windows`): the setup call, a routed view base, the region controls and a list box for the menu. It contains no XAML of its own.

The routing (`Routeables`) and the logic of the dialogs and notifications (`Routeables.Services`: what each button returns, vetoing a dismissal, the lifetime of a notification) are the ones of the other hosts. This package only draws them with WPF controls.

```xml
<ItemGroup>
  <ProjectReference Include="..\Routeables\Routeables.Wpf\Routeables.Wpf.csproj" />
  <ProjectReference Include="..\Routeables\Routeables.Generated\Routeables.Generated.csproj"
                    OutputItemType="Analyzer" ReferenceOutputAssembly="false" />
</ItemGroup>
```

## Set up

```csharp
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        this.UseRouteables<MainViewModel>(services => services.AddGeneratedRoutes());
        this.OpenMainWindow(new MainView());
    }
}
```

The two calls can be one: `this.UseRouteables<MainViewModel>(services => services.AddGeneratedRoutes(), () => new MainView(), window => window.Title = "My app");` builds the container now and opens the window as the application runs, so it works from the constructor of `App`, from `OnStartup` or from a `Startup` handler.

Leave `StartupUri` out of `App.xaml`: the container must exist before the first window. `OpenMainWindow` creates the window, gives the view the menu as `DataContext`, selects the first entry when the window is first activated and disposes the container when the last window closes. For a window you create yourself, call `window.AttachRouteables()`.

## A view

```xml
<routeables:RouteableFor x:Class="MyApp.Views.HelloView"
    xmlns:routeables="clr-namespace:Routeables.Wpf;assembly=Routeables.Wpf" ...>
    <TextBlock Text="{Binding Greeting}" />
</routeables:RouteableFor>
```

```csharp
public partial class HelloView : RouteableFor { public HelloView() => InitializeComponent(); }
```

A XAML root cannot be generic, so XAML views use `RouteableFor`; views written in code can use `RouteableFor<TViewModel>`. `IsLocked` is a read-only dependency property that follows the lock of the main view model.

The menu is a `MainViewModelBase` and `<routeables:RouteableListBox />` (its items are disabled while the menu is locked); the current view is `<ContentControl Content="{Binding CurrentView}" />`.

## Regions

- `<routeables:RouteableRegion Region="detail" />` shows the current view of a region.
- `<routeables:RouteableRegionItems Region="adopt" />` shows every view of the region, in rows and columns (a `WrapPanel` unless you set `ItemsPanel`). Put it in a `ScrollViewer` when the zone can outgrow its space.

A view has one parent in WPF: a page shown by a region must not also host its own current view. The error says which control holds it.

## Tabs

A `TabControl` written in XAML: each tab names its route, and the router builds the page.

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

In the `Routeables.Wpf.Services` namespace. `UseRouteables` already registers them; call `UseRouteablesServices(icon, messageBoxService)` yourself only to give an icon or to draw them another way.

- Dialogs, content dialogs and task dialogs are windows owned by the window the user last worked in, shown with `ShowDialog` (modal). A dialog opened over another one is owned by it.
- Notifications are small windows at the top centre of the active window, stacked, closed after five seconds, when clicked, or with the window. WPF has no notification control, and Routeables does not use the Windows toast API.
- `DialogView<TViewModel, TResult>`: the base of a modal view backed by a closeable view model, written in code (a XAML root cannot be generic). See `NoteView` in `Samples/XAnimals.Wpf`.
- `IMessageBoxService` (`DefaultMessageBoxService`): pass your own to `UseRouteablesServices` to draw them another way.

Calling a service before a window is attached throws an `InvalidOperationException` that says so.

## Themes

`RouteableRequestedTheme` is applied with the Fluent theme of WPF (`Application.ThemeMode`). WPF still marks it as an evaluation API (`WPF0001`), so a project that sets it in XAML needs `<NoWarn>$(NoWarn);WPF0001</NoWarn>`.

## Good to know

- Not covered by `HostContractTests`, as the test project targets `net10.0` and cannot reference a Windows-only project.
- Not trimmer-checked (`IsAotCompatible` is not declared): WPF itself is not trimmable.
- A notification follows its owner when it moves or is resized, but it is placed in device-independent units from the owner's top left corner: check it on a multi-monitor setup with different scaling.

## Next

[Hosts guide](../docs/guides/hosts.md), sample `Samples/XAnimals.Wpf`.
