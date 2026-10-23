# Routeables.Uno

`Routeables.Uno` is the [Uno Platform](https://platform.uno/) host of Routeables (WinUI XAML on Windows, Linux, macOS, WebAssembly, Android and iOS). It provides the application bootstrap, the routed view base, the region control, a list view for the main entries and the theme service. The dialogs and notifications are optional, in [`Routeables.Uno.Services`](../Routeables.Uno.Services/README.md).

> **Status: preview.** The Uno host was written without being able to restore Uno Platform where it was developed. It is type-checked against minimal stand-ins for the WinUI types and the CI builds it (job "Build Uno"), but it has not been run on a device yet. Please report what you find.

## Requirements

Targets **.NET 10** (`net10.0`), version `1.0.0-preview.1`. It references `Uno.WinUI` (6.7) and `Routeables.Core`. It contains no XAML of its own, only code, so it needs nothing but the `net10.0` reference assembly of Uno: a library built on it runs on every head.

```xml
<ItemGroup>
  <ProjectReference Include="..\Routeables\Routeables.Uno\Routeables.Uno.csproj" />
</ItemGroup>
```

## Quick start

Start from the Uno template (`dotnet new unoapp`), then add the references to the head project:

```xml
<ItemGroup>
  <ProjectReference Include="..\Routeables\Routeables.Uno\Routeables.Uno.csproj" />
  <ProjectReference Include="..\Routeables\Routeables.Uno.Services\Routeables.Uno.Services.csproj" />   <!-- optional: dialogs, notifications -->
  <ProjectReference Include="..\Routeables\Routeables.Generated\Routeables.Generated.csproj"
                    OutputItemType="Analyzer" ReferenceOutputAssembly="false" />
</ItemGroup>
```

**1. A view model.** Its route is the only attribute. It knows nothing about Uno.

```csharp
using Routeables.Abstraction;

[Routeable("/hello/{name}")]
public sealed class HelloViewModel(IRouter router, string name)
{
    public string Greeting { get; } = $"Hello, {name}!";
    public Task BackAsync() => router.GoBackAsync();
}
```

**2. Its view.** The XAML root is `RouteableFor`; the view called after the view model (`HelloViewModel` => `HelloView`) is found by name.

```xml
<!-- Views/HelloView.xaml -->
<routeables:RouteableFor
    x:Class="MyApp.Views.HelloView"
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    xmlns:routeables="using:Routeables.Uno">
    <StackPanel Padding="24" Spacing="12">
        <TextBlock Text="{Binding Greeting}" FontSize="28" />
        <Button Content="Back" Click="OnBack" />
    </StackPanel>
</routeables:RouteableFor>
```

```csharp
// Views/HelloView.xaml.cs
public sealed partial class HelloView : RouteableFor
{
    public HelloView() => InitializeComponent();

    private async void OnBack(object sender, RoutedEventArgs e) => await ((HelloViewModel)Context!).BackAsync();
}
```

A view written in code can derive from `RouteableFor<HelloViewModel>` to get a typed `ViewModel`; a XAML root cannot be generic in WinUI, which is why the XAML view uses the non-generic base.

**3. The menu.** A main view model returns the entries; `MainViewModelBase` does the rest.

```csharp
public sealed class MainViewModel(IRouter router) : MainViewModelBase(router)
{
    protected override ObservableCollection<IRouteableEntry> GetRouteables() =>
    [
        new RouteableEntry<HomeViewModel>("Home", route: "/home", template: "/home"),
    ];
}
```

```xml
<!-- MainPage.xaml: DataContext is the main view model -->
<Page x:Class="MyApp.MainPage"
      xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
      xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
      xmlns:routeables="using:Routeables.Uno">
    <Grid>
        <Grid.ColumnDefinitions>
            <ColumnDefinition Width="240" />
            <ColumnDefinition Width="*" />
        </Grid.ColumnDefinitions>
        <routeables:RouteableListView />
        <ContentControl Grid.Column="1" Content="{Binding CurrentView}"
                        HorizontalContentAlignment="Stretch" VerticalContentAlignment="Stretch" />
    </Grid>
</Page>
```

**4. The application.** The container is built in the constructor; `OpenMainWindow` creates the window, gives the page the menu and connects it.

```csharp
public partial class App : Application
{
    private Window? _window;

    public App()
    {
        InitializeComponent();
        Services = this.UseRouteables(services =>
        {
            services.UseRouteablesServices();                                            // optional (Routeables.Uno.Services)
            services.AddGeneratedRoutes();                                               // the routes declared by attributes
            services.RegisterMainRouteables<MainViewModel>(ServiceLifetime.Singleton);   // one menu for the application
        });
    }

    public IServiceProvider Services { get; }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        _window = this.OpenMainWindow(new MainPage());
    }
}
```

`OpenMainWindow` creates the window with the page, sets the page's `DataContext` to the main view model, then activates it. Behind it, `AttachRouteables()` selects the first entry the first time the window is activated, tells the dialogs and notifications which window to use, and disposes the container when the window closes. Call `AttachRouteables()` yourself only for a window you create some other way (a second window, say). To set the title or the size, pass a callback: `this.OpenMainWindow(new MainPage(), window => window.Title = "My app")`.

## Sample

[`Samples/XAnimals.Uno`](../Samples/XAnimals.Uno) is the Animals app on Uno (desktop head, `Uno.Sdk` single project), over the same `XAnimals.ViewModels` as the other samples: the menu, the lists and details, History, Regions, Lock and About (dialogs, notifications and a modal). It pins `Uno.Sdk` in the `global.json` of the repository. Known gap: the images are downloaded by Uno itself, and Wikimedia refuses requests without a User-Agent, so some heads may show no pictures; the Avalonia sample sets one on its own `HttpClient`.

## What the host provides

| Piece | What it does |
| --- | --- |
| `Application.UseRouteables(configure, afterBuild)` | Builds the container: router (singleton), arguments factory, environment (`Runtime.Uno`), property injection (`[RouteableParameter]`, `[InjectProperty]`) and the theme service. In Debug builds it uses `ValidateOnBuild`, so a registration that cannot be built fails at start with its name. |
| `Application.OpenMainWindow(content, configure, onDispose)` | Creates, connects and activates the main window; the content gets the main view model as `DataContext`. |
| `Window.AttachRouteables(onDispose)` | Connects any other window: first selection, active window for the services, disposal on close. |
| `RouteableFor` / `RouteableFor<TViewModel>` | The base of the views: `Context` is the `DataContext`; `IsLocked` is a dependency property, true when every entry of the main view model is locked. Lifecycle members (`CanLeaveAsync`, `InitializeAsync`, `OnEnteringAsync`, `OnLeavingAsync`) can be overridden. |
| `RouteableListView` | A `ListView` of the entries of the main view model that selects the current one; a locked entry is disabled. |
| `RouteableRegion` | The zone that shows a region: `<routeables:RouteableRegion Region="detail" />`. See [Regions](../docs/guides/regions.md). |
| `IRouteableRequestedThemeService` | Sets `RequestedTheme` on the content of the active window (`Application.RequestedTheme` cannot change at run time). |

A property that receives a `{name::}` template parameter carries `[RouteableParameter]` (namespace `Routeables.Abstraction`).

## Where this differs from the other hosts

- **No Uno route change.** Routeables does not use `Frame` or `Uno.Extensions.Navigation`: the router builds the view and the main view model shows it. Do not mix the two in one window.
- **Generic XAML roots.** WinUI cannot declare a generic root element, so XAML views use `RouteableFor`; the typed `RouteableFor<TViewModel>` is for views in code.
- **Not trimmer-checked yet.** The project does not declare `IsAotCompatible`: bindings in `RouteableListView` use `Binding` by path.
