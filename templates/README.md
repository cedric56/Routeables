# Routeables templates

```
dotnet new install Routeables.Templates
dotnet new routeables-avalonia -n MyApp
cd MyApp && dotnet run
```

The template is an Avalonia application with two routed pages and no menu (the `Route` of the `RouteableRegion` in `MainWindow.axaml` chooses the first page). There is no logic in the code-behind: a view is its XAML and one line, `InitializeComponent()`. The button of the first page goes to the second with `routeables:RouteableLink.Route="/about"`, and the view model only holds what the page shows. A view derives from `RouteableFor<T>` because it is the shortest form (the generator finds the view by its name, `HelloView` goes with `HelloViewModel`, and it adds the lifecycle); a plain `UserControl` called `...View` works too. It has not been built or run by its author yet: report anything that does not compile.
