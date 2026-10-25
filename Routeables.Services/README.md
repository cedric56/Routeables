# Routeables.Services

The contracts of the optional UI services: dialogs, notifications and theme. Each host implements them and registers them in `UseRouteables`: [Razor](../Routeables.Razor/README.md), [MAUI](../Routeables.Maui/README.md), [Avalonia](../Routeables.Avalonia/README.md), [Uno](../Routeables.Uno/README.md), [WPF](../Routeables.Wpf/README.md). Routing does not need any of this. Targets **.NET 10**, namespace `Routeables.Services`.

| Inject | For |
| --- | --- |
| `IRouteableDialogService` | Message boxes |
| `IRouteableContentDialogService` | A dialog that hosts a view and its view model |
| `IRouteableTaskDialogService` | Dialogs with a header, buttons and progress |
| `IRouteableNotificationService` | Toast notifications |
| `IRouteableRequestedThemeService` | Light, dark or system theme (registered by the host) |
| `IRouteableDispatcher` | Runs work on the thread of the interface, from any thread (registered by the host) |

```csharp
// DialogsViewModel.cs of the XAnimals sample
public async Task ShowYesNoAsync()
{
    var yes = await _dialogs.ShowYesNoAsync("Add the polar bear to your favorites?", "Favorites");
    // ...
}
```

Every host behaves the same way: a button press runs its command, then asks the `Closing` veto, then closes. A notification closes after five seconds unless you say otherwise (`RouteableNotificationDefaults.Never` keeps it).

The look can be replaced: see [`Reshapes`](../Reshapes/README.md) (MudBlazor, CommunityToolkit.Maui, FluentAvalonia).
