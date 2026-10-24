# Reshapes

Optional projects that change how dialogs, notifications and modals look, by building them on a UI library. Routing is unchanged, and every host works without them. Each one is a single call after `UseRouteables`, and replaces `UseRouteablesServices`.

| Project | Host | Setup |
| --- | --- | --- |
| `Reshapes.Razor.MudBlazor` | [Razor](../Routeables.Razor/README.md) | `services.UseMudBlazor()` |
| `Reshapes.Maui.Community` | [MAUI](../Routeables.Maui/README.md) | `builder.UseCommunityToolkit()` |
| `Reshapes.Avalonia.Fluent` | [Avalonia](../Routeables.Avalonia/README.md) | `services.UseFluentAvalonia(icon)` |

Uno has no reshape yet: its [services](../Routeables.Uno.Services/README.md) accept your own `IMessageBoxService`.

Add a project reference next to the host's.
