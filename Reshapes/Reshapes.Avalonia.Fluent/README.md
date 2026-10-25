# Reshapes.Avalonia.Fluent

`ReshapeMessageBoxService`: the dialogs and notifications of [`Routeables.Avalonia`](https://www.nuget.org/packages/Routeables.Avalonia) drawn with FluentAvaloniaUI.

```csharp
AppBuilder.Configure<App>().UseRouteables<MainViewModel>(r =>
{
    r.UseFluentAvalonia("avares://MyApp/Assets/icon.ico");   // in place of UseRouteablesServices
    r.AddGeneratedRoutes();
});
```

[Reshapes overview](https://github.com/cedric56/Routeables/blob/master/Reshapes/README.md)
