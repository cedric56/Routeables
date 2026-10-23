# Routeables.Uno.Services

The optional dialogs and notifications of the [Uno Platform](https://platform.uno/) host. `Routeables.Uno` builds the container with the router only; add this package when the application wants the UI services too.

> **Status: preview**, like `Routeables.Uno`: type-checked and built by the CI, not run on a device yet.

```xml
<ItemGroup>
  <ProjectReference Include="..\Routeables\Routeables.Uno.Services\Routeables.Uno.Services.csproj" />
</ItemGroup>
```

```csharp
using Routeables.Uno;
using Routeables.Uno.Services;

Services = this.UseRouteables(services => services.UseRouteablesServices());
```

`UseRouteablesServices()` registers, as singletons:

| Contract | Drawn with |
| --- | --- |
| `IRouteableDialogService` | A `ContentDialog` with one button per word of `"YesNoCancel"`. The index returned is the order of the buttons (Yes = 0, No = 1, Cancel or Abort = 2); -1 when the dialog was dismissed. |
| `IRouteableContentDialogService` | A `ContentDialog` that hosts the content and up to three buttons (primary, secondary, close), with their commands, `Closing` veto and `Close()` from any thread. |
| `IRouteableTaskDialogService` | A `ContentDialog` with a header, sub-header, icon, content, progress bar, footer (in an `Expander` when `Auto`) and one button per requested `RouteableTaskDialogResult`. |
| `IRouteableNotificationService` | `InfoBar`s stacked at the top centre of the active window, in a popup that does not take the input of what is behind it. They close after the expiration (5 seconds by default) or when the user closes them. |
| `DialogView<TViewModel, TResult>` | The base of a modal view backed by an `ICloseableViewModel<TResult>`: `ServiceProvider.ShowModalAsync<TViewModel, TResult>()` shows it in a `ContentDialog`, with the buttons taken from the view model's commands. Written in code (a WinUI XAML root cannot be generic): see `NoteView` in the sample. |
| `IMessageBoxService` | `DefaultMessageBoxService`, which draws the above. Pass your own to `UseRouteablesServices(messageBoxService)` to draw them another way. |

The dialogs are shown in the window the user last worked in (the main window opened by `Application.OpenMainWindow(...)`, and the ones given to `Window.AttachRouteables()`), from any thread. A window shows one `ContentDialog` at a time, so a second dialog waits for the first to close. Calling a service before a window has content throws an `InvalidOperationException` that says so.
