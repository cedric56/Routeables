# Routeables templates

```
dotnet new install Routeables.Templates
dotnet new routeables-avalonia -n MyApp
cd MyApp && dotnet run
```

The template is an Avalonia application with two routed pages and no menu (`AddHome("/hello/World")` chooses the first page). A view derives from `RouteableFor<T>` here because it is the shortest form (the generator finds the view and it adds the lifecycle); `HelloView.axaml.cs` says what to do without it. It has not been built or run by its author yet: report anything that does not compile.
