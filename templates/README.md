# Routeables templates

```
dotnet new install Routeables.Templates
dotnet new routeables-avalonia -n MyApp     # a desktop application
dotnet new routeables-blazor -n MyWeb       # a Blazor Web App (Interactive Server)
cd MyApp && dotnet run
```

The template is an Avalonia application with two routed pages and no menu (the `Route` of the `RouteableRegion` in `MainWindow.axaml` chooses the first page). There is no logic in the code-behind: a view is its XAML and one line, `InitializeComponent()`. The button of the first page goes to the second with `routeables:RouteableLink.Route="/about"`, and the view model only holds what the page shows. A view derives from `RouteableFor<T>` because it is the shortest form (the generator finds the view by its name, `HelloView` goes with `HelloViewModel`, and it adds the lifecycle); a plain `UserControl` called `...View` works too. It has not been built or run by its author yet: report anything that does not compile.

## The Blazor template

`routeables-blazor` is a Blazor Web App (Interactive Server, no prerendering) with two routed pages: `HomeView` (`/`) and `HelloView` (`/hello/{name}`), each a Razor view that derives from `RouteableFor<TView, TViewModel>` and a view model that only holds what the page shows. The routes are registered **by name in `Program.cs`** (`RegisterRoute<HomeView, HomeViewModel>(route: "/")`): the source generator cannot read the Razor files of its own project, so a Razor view of the application is registered this way (or put the views in a referenced class library, where the generator finds them). `App.razor` links the two stylesheets Routeables needs, the `RouteablesApp.styles.css` bundle (the scoped styles of the layout, the menu and the dialogs) and `_content/Routeables.Blazor/theme.css`; without the first, dialogs are not overlays. The pages link to each other with `<RouteableLink Route="...">`. It has not been built or run by its author: report anything that does not compile.
