# Routeables templates

```
dotnet new install Routeables.Templates
dotnet new routeables-avalonia -n MyApp     # a desktop application
dotnet new routeables-blazor -n MyWeb       # a Blazor Web App (Interactive Server)
dotnet new routeables-maui -n MyMobile      # a .NET MAUI application (Android, iOS, Mac Catalyst, Windows)
dotnet new routeables-uno -n MyUno          # an Uno Platform application (desktop head)
dotnet new routeables-winui3 -n MyWinUI     # a WinUI 3 application (Windows, unpackaged)
cd MyApp && dotnet run
```

The template is an Avalonia application with two routed pages and no menu (the `Route` of the `RouteableRegion` in `MainWindow.axaml` chooses the first page). There is no logic in the code-behind: a view is its XAML and one line, `InitializeComponent()`. The button of the first page goes to the second with `routeables:RouteableLink.Route="/about"`, and the view model only holds what the page shows. A view derives from `RouteableFor<T>` because it is the shortest form (the generator finds the view by its name, `HelloView` goes with `HelloViewModel`, and it adds the lifecycle); a plain `UserControl` called `...View` works too. It has not been built or run by its author yet: report anything that does not compile.

## The Blazor template

`routeables-blazor` is a Blazor Web App (Interactive Server, no prerendering) with two routed pages: `HomeView` (`/`) and `HelloView` (`/hello/{name}`), each a Razor view that derives from `RouteableFor<TView, TViewModel>` and a view model that only holds what the page shows. The routes are registered **by name in `Program.cs`** (`RegisterRoute<HomeView, HomeViewModel>(route: "/")`): the source generator cannot read the Razor files of its own project, so a Razor view of the application is registered this way (or put the views in a referenced class library, where the generator finds them). `App.razor` links the two stylesheets Routeables needs, the `RouteablesApp.styles.css` bundle (the scoped styles of the layout, the menu and the dialogs) and `_content/Routeables.Blazor/theme.css`; without the first, dialogs are not overlays. The pages link to each other with `<RouteableLink Route="...">`. It has not been built or run by its author: report anything that does not compile.

## The MAUI, Uno and WinUI 3 templates

They follow the Avalonia one: two routed pages (`HelloView` at `/hello/{name}` and `AboutView` at `/about`), a main page with a `RouteableRegion` whose `Route` opens the first page, no menu, no logic in the code-behind, and a button that goes to the other page with `routeables:RouteableLink.Route`. The pages are registered by the source generator (the view model carries `[Routeable]`, the view has the same name with `View`).

- **`routeables-maui`** is a single-project MAUI application (`UseRouteables()` in `MauiProgram`, `new Window(new MainPage())` in `App`). The targets are Android, plus iOS and Mac Catalyst off Linux and Windows on Windows; it needs the MAUI workload (`dotnet workload install maui`). It is unpackaged on Windows (`WindowsPackageType=None`).
- **`routeables-uno`** uses `Uno.Sdk` (version written in the `Sdk` attribute of the project, so no `global.json` is needed) and builds the **desktop** head only; to add Android, iOS or WebAssembly, list the target and add the folders Uno asks for. The window is opened by `UseRouteables(configure, () => new MainPage(), ...)` in the constructor of `App`.
- **`routeables-winui3`** is an unpackaged, self-contained WinUI 3 application (Windows App SDK 1.8): `UseRouteables()` in `App`, `OpenMainWindow` in `OnLaunched`. Windows only.
- On Uno and WinUI 3 the view models carry `[Bindable(true)]`, which lets the XAML compiler generate the binding code (no reflection, safe for trimming and Native AOT). XAML views derive from the non-generic `RouteableFor` because a XAML root cannot be generic in WinUI.

**None of the three has been built or run by its author** (the MAUI, Uno and Windows App SDK workloads are not available where they were written): report anything that does not compile. The template packages reference `Routeables.Maui`, `Routeables.Uno` and `Routeables.WinUI3` at the same version as the Avalonia one.
