# Routeables.Avalonia.Services

The optional UI services of the Avalonia host: dialogs, content dialogs, task dialogs and notifications, drawn by an `IMessageBoxService`.
[`Routeables.Avalonia`](../Routeables.Avalonia/README.md) alone routes; add this package when you want these services.

Targets **.NET 10** (`net10.0`), version `1.0.0-preview.1`, namespace `Routeables.Avalonia.Services`. It references `Avalonia`, `Routeables.Avalonia` and
the contracts of [`Routeables.Services`](../Routeables.Services/README.md).

```csharp
using Routeables.Avalonia;
using Routeables.Avalonia.Services;

AppBuilder.Configure<App>()
    .UseRouteables(services =>
    {
        services.UseRouteablesServices("avares://MyApp/Assets/icon.ico");   // the UI services
        services.AddGeneratedRoutes();
    })
    .UsePlatformDetect();
```

`UseRouteablesServices(icon, messageBoxService)` is called on the service collection, from the `configureExternalServices` callback of `UseRouteables`.
`icon` is the icon of the windows `DefaultMessageBoxService` draws; pass your own `IMessageBoxService` (such as the FluentAvalonia one of
[`Reshapes.Avalonia.Fluent`](../Reshapes/README.md)) to change the look.

## What it contains

| Contract | Implementation |
| --- | --- |
| `IRouteableDialogService` | Message boxes through `IMessageBoxService` |
| `IRouteableContentDialogService` | Dialogs that host a view and a view model |
| `IRouteableTaskDialogService` | Task dialogs with buttons and results |
| `IRouteableNotificationService` | Notifications shown by the message box service |

`DefaultMessageBoxService` draws everything with Avalonia controls: modal windows owned by the active `TopLevel` on desktop, and an overlay in the top level's layer
where there is a single view (Android, iOS, browser). `DialogView<TViewModel, TResult>` is the base of a modal view backed by a closeable view model; it needs
`UseRouteablesServices` to have run. The theme service (`IRouteableRequestedThemeService`) stays in `Routeables.Avalonia`.
