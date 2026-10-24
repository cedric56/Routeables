# Routeables.Maui

`Routeables.Maui` is the .NET MAUI host of Routeables. It provides the setup call, a routed `ContentView` base, a `Shell` driven by the main
view model, the region control and the theme service. The dialog, modal and notification services are optional, in [`Routeables.Maui.Services`](../Routeables.Maui.Services/README.md).

## Requirements

Version `1.0.0`. It targets `net10.0`, `net10.0-android`, `net10.0-ios`, `net10.0-maccatalyst` and `net10.0-windows10.0.19041.0`, with
minimum platform versions Android 21, iOS 15, Mac Catalyst 17 and Windows 10.0.17763. It references `Microsoft.Maui.Controls`,
`Routeables.Abstraction` and `Routeables.Core`; the MAUI workload must be installed. On CI, `-p:CiBuild=true` builds only the plain `net10.0` target.

```xml
<ItemGroup>
  <ProjectReference Include="..\Routeables.Maui\Routeables.Maui.csproj" />
</ItemGroup>
```

```bash
dotnet build Routeables.Maui/Routeables.Maui.csproj
```

## Registering Routeables

```csharp
using Routeables.Maui;

var builder = MauiApp.CreateBuilder();
builder
    .UseRouteables()
    .UseMauiApp<App>();

builder.Services.AddGeneratedRoutes();
builder.Services.RegisterMainRouteables<MainViewModel>(ServiceLifetime.Singleton);

var app = builder.Build();
```

`UseRouteables()` registers the router (singleton), the arguments factory, the environment information, property
injection (`[RouteableParameter]` and MAUI's `[QueryProperty]`, neither falling back to the container) and the theme service. Routes and the main view model are yours to register.

Dialogs, content dialogs, task dialogs, modal pages and notifications are **optional**: add `.UseRouteablesServices()` from
[`Routeables.Maui.Services`](../Routeables.Maui.Services/README.md). For CommunityToolkit.Maui popups and snackbars use `UseRouteablesCommunityToolkit()` from
[`Reshapes.Maui.Community`](../Reshapes/README.md), which calls `UseRouteables()` and `UseRouteablesServices()` itself: use it **instead of** both.

## Views

- `RouteableFor` is a `ContentView` base: it maps `Context` to `BindingContext` and has the overridable route change and initialization hooks.
- `RouteableRegion` is the zone that shows a region. Give it the name registered with `AddRegion`; it follows the router of that region:

```xml
<routeables:RouteableRegion Region="detail" />
<!-- xmlns:routeables="clr-namespace:Routeables.Maui;assembly=Routeables.Maui" -->
```

## RouteablesShell

`RouteablesShell` is a `Shell` whose top-level items (tabs or flyout entries) come from `IMainRouteables.Routeables`. Shell owns the top-level
structure and Routeables the route change inside each item, so pages managed by the router must not also be routed with `Shell.GoToAsync`.
With `AddLocking()` (package `Routeables.Locking`), a page that blocks leaving locks the main entries: they are disabled and the selection returns to the locked one.

## Other helpers

- `RouteableMauiEntry`: an `Entry` that shows a validation error (red underline, icon, tooltip) from a binding context that implements
  `INotifyDataErrorInfo`; set `ColumnName` to the validated property.
- `RouteableParameterAttribute` (namespace `Routeables.Abstraction`, shared by the hosts): marks a view or view-model property that receives a route change value.
- `RouteableNullConverter`: a value converter that gives true for null or a blank string.
- `RouteableModalFor<TViewModel, TResult>`, the toasts and the dialogs are in [`Routeables.Maui.Services`](../Routeables.Maui.Services/README.md).

For the shared view-model bases see [`Routeables.Core`](../Routeables.Core/README.md);
for the routing API, [`Routeables.Abstraction`](../Routeables.Abstraction/README.md) and [`Routeables.Routing`](../Routeables.Routing/README.md).
Samples: `Samples/XAnimals.Maui` and `XAnimals.MauiHybrid`.
