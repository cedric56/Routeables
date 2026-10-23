# Reshapes

The `Reshapes.*` projects change how a host looks, without changing how it routes. Each one replaces the dialog, notification or
modal services of a host with ones built on a UI library. They are optional: every host works without them.

| Project | Host | What it adds |
| --- | --- | --- |
| `Reshapes.Razor.MudBlazor` | [`Routeables.Razor`](../Routeables.Razor/README.md) | `services.UseRouteablesMudBlazor(...)`: MudBlazor dialogs, content and task dialogs, snackbar notifications and a MudBlazor layout. References `MudBlazor`. |
| `Reshapes.Maui.Community` | [`Routeables.Maui`](../Routeables.Maui/README.md) | `builder.UseRouteablesCommunityToolkit()`: CommunityToolkit.Maui popups and snackbars for modals, content and task dialogs and notifications. Use it **instead of** `UseRouteables()`, and do not also call `UseMauiCommunityToolkit()`. |
| `Reshapes.Avalonia.Fluent` | [`Routeables.Avalonia`](../Routeables.Avalonia/README.md) | `ReshapeMessageBoxService`, an `IMessageBoxService` built on FluentAvaloniaUI; pass it to `RouteableBuilder.CreateRouteableApp(messageBoxService: ...)`. |

Each targets `net10.0` (the MAUI one also the platform targets of the host) and references its host project. To use one, add a project reference to it next to the host's.
