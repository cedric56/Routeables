# Reshapes

Optional projects that change how dialogs, notifications and modals look, by building them on a UI library. Routing is unchanged, and every host works without them.

| Project | Host | Setup |
| --- | --- | --- |
| `Reshapes.Razor.MudBlazor` | [Razor](../Routeables.Razor/README.md) | `services.UseRouteablesMudBlazor()` |
| `Reshapes.Maui.Community` | [MAUI](../Routeables.Maui/README.md) | `builder.UseRouteablesCommunityToolkit()` instead of `UseRouteables()` |
| `Reshapes.Avalonia.Fluent` | [Avalonia](../Routeables.Avalonia/README.md) | `services.UseRouteablesServices(icon, new ReshapeMessageBoxService())` |

Uno has no reshape yet: its [services](../Routeables.Uno.Services/README.md) accept your own `IMessageBoxService`.

Add a project reference next to the host's.
