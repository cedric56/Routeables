# Routeables.Maui

The .NET MAUI host of Routeables: the setup call, a routed `ContentView` base, a `Shell` driven by the main view model, and the region control. Targets `net10.0` and the Android, iOS, Mac Catalyst and Windows frameworks (the MAUI workload is required).

```bash
dotnet add package Routeables.Maui
```

The package brings `Routeables` (routing, abstractions and the source generator): there is nothing else to add.

## Set up

```csharp
var builder = MauiApp.CreateBuilder();
builder.UseMauiApp<App>().UseRouteables<MainViewModel>(r => r.AddGeneratedRoutes());
```

## A view

```xml
<!-- Samples/XAnimals.Maui/Views/Animals/DogDetailView.xaml -->
<routeables:RouteableFor x:Class="XAnimals.Maui.Views.DogDetailView"
    xmlns:routeables="clr-namespace:Routeables.Maui;assembly=Routeables.Maui" ...>
    <views:AnimalDetail />
</routeables:RouteableFor>
```

`RouteableFor` maps the context to `BindingContext`. A zone that shows a region is `<routeables:RouteableRegion Region="shell-content" />`.

## The shell

`RouteablesShell` is a `Shell` whose tabs or flyout entries come from the main view model, and it follows entries added later (modules). Shell owns the top level and Routeables the route changes inside each item: do not also use `Shell.GoToAsync` on those pages.

### A Shell written in XAML

Keep your own `Shell` (flyout, tabs, routes in XAML) and let Routeables build each page from its route:

```csharp
xamlShell.AttachRouteables(provider);   // "cats" opens the route "/cats"
```

`ShellItems.AttachRouteables(Shell, IServiceProvider)` returns an `IDisposable`. Each `ShellContent` with a `Route` gets the view of that route, and the router stays in step with what the user selects.

## Dialogs and notifications

In the `Routeables.Maui.Services` namespace. `UseRouteables` already registers them; `UseRouteablesServices` takes optional factories, to call only to replace a service. For CommunityToolkit.Maui popups and snackbars call `UseCommunityToolkit()` from [`Reshapes`](../Reshapes/README.md) after `UseRouteables`: it replaces them.

- Dialogs, content dialogs, task dialogs and notifications. Toasts are native on Windows, Android, iOS and Mac Catalyst, and an overlay on the plain `net10.0` target.
- `RouteableModalFor<TViewModel, TResult>`: the base of a modal view that returns a typed result.

Dialogs and modals need an active page to appear on. Contracts: [`Routeables.Services`](../Routeables.Services/README.md).

## Good to know

- Property injection understands `[RouteableParameter]` and MAUI's `[QueryProperty]`.
- `RouteableMauiEntry` shows a validation error from an `INotifyDataErrorInfo` context.
- CommunityToolkit popups and snackbars: call `UseCommunityToolkit()` from [`Reshapes`](../Reshapes/README.md) after `UseRouteables`, next to the usual `UseMauiCommunityToolkit()`.
- On CI, `-p:CiBuild=true` builds only the plain `net10.0` target.

## Next

[Hosts guide](../docs/guides/hosts.md), samples `Samples/XAnimals.Maui` and `XAnimals.MauiHybrid`.
