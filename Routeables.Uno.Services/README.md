# Routeables.Uno.Services

Dialogs and notifications for the [Uno Platform](https://platform.uno/) host. [`Routeables.Uno`](../Routeables.Uno/README.md) routes without it.

> [!WARNING]
> Preview, like the host: built by CI, not run on a device yet.

```csharp
Services = this.UseRouteables<MainViewModel>(services => services.UseRouteablesServices());
```

## What it brings

- Dialogs, content dialogs and task dialogs, drawn as `ContentDialog`s in the window the user last worked in. A window shows one at a time; a second waits for the first.
- Notifications: `InfoBar`s stacked at the top of the active window, closed after five seconds or by the user.
- `DialogView<TViewModel, TResult>`: the base of a modal view backed by a closeable view model, written in code (a WinUI XAML root cannot be generic). See `NoteView` in `Samples/XAnimals.Uno`.
- `IMessageBoxService` (`DefaultMessageBoxService`): pass your own to `UseRouteablesServices(messageBoxService)` to draw them another way.

Calling a service before a window has content throws an `InvalidOperationException` that says so. Contracts: [`Routeables.Services`](../Routeables.Services/README.md).
