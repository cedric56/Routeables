# Routeables.Services

The contracts of the optional UI services: dialogs, notifications and theme. Each host implements them in its own services package: [Razor](../Routeables.Razor.Services/README.md), [MAUI](../Routeables.Maui.Services/README.md), [Avalonia](../Routeables.Avalonia.Services/README.md), [Uno](../Routeables.Uno.Services/README.md). Routing does not need any of this. Targets **.NET 10**, namespace `Routeables.Services`.

| Inject | For |
| --- | --- |
| `IRouteableDialogs` | The one to start with: `MessageBox`, `ContentDialog` and `TaskDialog` |
| `IRouteableDialogService` | Message boxes |
| `IRouteableContentDialogService` | A dialog that hosts a view and its view model |
| `IRouteableTaskDialogService` | Dialogs with a header, buttons and progress |
| `IRouteableNotificationService` | Toast notifications |
| `IRouteableRequestedThemeService` | Light, dark or system theme (registered by the host) |

```csharp
public sealed class OrderViewModel(IRouteableDialogs dialogs)
{
    public Task AskAsync() => dialogs.MessageBox.ShowAsync("Delete this order?", "Orders", "YesNo");
}
```

Every host behaves the same way: a button press runs its command, then asks the `Closing` veto, then closes. A notification closes after five seconds unless you say otherwise (`RouteableNotificationDefaults.Never` keeps it).

The look can be replaced: see [`Reshapes`](../Reshapes/README.md) (MudBlazor, CommunityToolkit.Maui, FluentAvalonia).
