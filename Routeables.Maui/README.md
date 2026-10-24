# Routeables.Maui

The .NET MAUI host of Routeables: the setup call, a routed `ContentView` base, a `Shell` driven by the main view model, and the region control. Targets `net10.0` and the Android, iOS, Mac Catalyst and Windows frameworks (the MAUI workload is required).

```xml
<ItemGroup>
  <ProjectReference Include="..\Routeables\Routeables.Maui\Routeables.Maui.csproj" />
  <ProjectReference Include="..\Routeables\Routeables.Generated\Routeables.Generated.csproj"
                    OutputItemType="Analyzer" ReferenceOutputAssembly="false" />
</ItemGroup>
```

## Set up

```csharp
var builder = MauiApp.CreateBuilder();
builder.UseMauiApp<App>().UseRouteables();

builder.Services.AddGeneratedRoutes();
builder.Services.RegisterMainRouteables<MainViewModel>(ServiceLifetime.Singleton);
```

## A view

```xml
<routeables:RouteableFor x:Class="MyApp.HomeView"
    xmlns:routeables="clr-namespace:Routeables.Maui;assembly=Routeables.Maui" ...>
    <Label Text="{Binding Title}" />
</routeables:RouteableFor>
```

`RouteableFor` maps the context to `BindingContext`. A zone that shows a region is `<routeables:RouteableRegion Region="detail" />`.

## The shell

`RouteablesShell` is a `Shell` whose tabs or flyout entries come from the main view model, and it follows entries added later (modules). Shell owns the top level and Routeables the route changes inside each item: do not also use `Shell.GoToAsync` on those pages.

## Good to know

- Property injection understands `[RouteableParameter]` and MAUI's `[QueryProperty]`.
- `RouteableMauiEntry` shows a validation error from an `INotifyDataErrorInfo` context.
- CommunityToolkit popups and snackbars: use `UseRouteablesCommunityToolkit()` from [`Reshapes`](../Reshapes/README.md) **instead of** `UseRouteables()`.
- On CI, `-p:CiBuild=true` builds only the plain `net10.0` target.

## Next

[`Routeables.Maui.Services`](../Routeables.Maui.Services/README.md) (dialogs, modals, notifications), [Hosts guide](../docs/guides/hosts.md), samples `Samples/XAnimals.Maui` and `XAnimals.MauiHybrid`.
