# Routeables.Avalonia.Services

Dialogs and notifications for the Avalonia host, drawn with Avalonia controls. [`Routeables.Avalonia`](../Routeables.Avalonia/README.md) routes without it. Targets **.NET 10**.

```csharp
AppBuilder.Configure<App>()
    .UseRouteables(services =>
    {
        services.UseRouteablesServices("avares://MyApp/Assets/icon.ico");
        services.AddGeneratedRoutes();
    })
    .UsePlatformDetect();
```

The icon is the one of the windows that are drawn. To change the look, pass your own `IMessageBoxService` (such as the FluentAvalonia one of [`Reshapes`](../Reshapes/README.md)).

## What it brings

- Dialogs, content dialogs, task dialogs and notifications, through `IMessageBoxService` (`DefaultMessageBoxService`): modal windows on desktop, an overlay on single-view platforms (Android, iOS, browser).
- `DialogView<TViewModel, TResult>`: the base of a modal view backed by a closeable view model.

Contracts: [`Routeables.Services`](../Routeables.Services/README.md).
