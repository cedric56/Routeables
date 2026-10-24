# Getting started

## Installation

The projects target **.NET 10** (`net10.0`), so you need a .NET 10 SDK. MAUI also needs the MAUI workload. For now, use the projects from source:

```bash
git clone https://github.com/cedric56/Routeables.git
```

From NuGet, add the package of your host (`Routeables.Razor`, `Routeables.Maui`, `Routeables.Avalonia` or `Routeables.Uno`): it brings `Routeables`, which holds the routing, the abstractions and the source generator. From source, add a project reference to the host you need (it references `Routeables.Core`, which brings in routing and the abstractions), and the source generator as an analyzer:

```xml
<ItemGroup>
  <ProjectReference Include="..\Routeables\Routeables.Avalonia\Routeables.Avalonia.csproj" />
  <!-- or Routeables.Razor / Routeables.Maui -->

  <ProjectReference Include="..\Routeables\Routeables.Generated\Routeables.Generated.csproj"
                    OutputItemType="Analyzer" ReferenceOutputAssembly="false" />
</ItemGroup>
```

Adjust the relative paths to your layout. The generator must be referenced by **every project that contains attributes it should process**: the project with the views and, if you use `[RouteableViewModel]`, the project with the view models (see [Typed route methods](guides/typed-routes.md#typed-routing-methods-source-generator)).

## Quick start

### 1. Declare routes

Put `[Routeable]` on the view model to give it a route. The view is the class named after it:

```csharp
using Routeables.Abstraction;

[Routeable("/orders/{id:int}")]
public sealed class OrderViewModel(IRouter router, int id) { }

public partial class OrderView : RouteableFor<OrderViewModel> { }   // Avalonia; use the host's base type
```

`OrderViewModel` is paired with `OrderView`, a routed view (it derives from the host's `RouteableFor`) found in the project or in the assemblies it references.
Use `[RouteableView(typeof(OrderViewModel))]` on the view when the names do not follow that convention. Two views of the same name raise `ROUT202`.

The source generator turns these attributes into registrations. Register them once at startup:

```csharp
using Routeables.Generated;

services.AddGeneratedRoutes();
```

You can also register routes by hand with `services.RegisterRoute<OrderView, OrderViewModel>(...)`.

> [!NOTE]
> `AddGeneratedRoutes()` is generated as an `internal` extension in **each project** that references the generator, so it must be called from the project that generated it (normally the application project). It collects the attributes of the current project and of the assemblies it references.

> [!IMPORTANT]
> **Blazor:** the generator cannot read `.razor` files of its own project (the Razor compiler is another generator, and generators do not see each other's output). A view written in Razor is found in two cases: its `[RouteableView(...)]` is on a C# `partial class` of the same name (`HelloView.razor.cs`), or the view is in a **referenced** project, and `AddGeneratedRoutes()` is called from the application project that references it. An `@attribute [RouteableView(...)]` inside the Razor file, in the project that calls `AddGeneratedRoutes()`, registers nothing.

### 2. Set up your host

One call per host starts Routeables, registers your main view model (its lifetime is the host's choice) and takes a block `r => ...` for your own registrations. The block is the same on the four hosts. The same call registers the dialogs and notifications of the host. Only the shape of the call differs, because each framework has its own way to start.

```csharp
// Blazor (Program.cs)
builder.Services.UseRouteablesServer<MainViewModel>(      // or UseRouteablesWebAssembly, UseRouteablesHybrid
    r => r.AddGeneratedRoutes());
app.MapRazorComponents<App>()
   .AddInteractiveServerRenderMode()
   .AddAdditionalAssemblies(typeof(MainLayoutBase).Assembly);    // Routeables.Razor: catch-all page, layout, not-found page

// .NET MAUI (MauiProgram.cs)
builder.UseMauiApp<App>()
       .UseRouteables<MainViewModel>(r => r.AddGeneratedRoutes());

// Avalonia (App.axaml.cs)
AppBuilder.Configure<App>().UseRouteables<MainViewModel>(r => r.AddGeneratedRoutes());
// ...then, in OnFrameworkInitializationCompleted, show the window with its menu:
//    this.OpenMainView(new MainWindow());

// Uno Platform (App.xaml.cs)
Services = this.UseRouteables<MainViewModel>(r =>
{
    r.AddGeneratedRoutes();
});
// ...then, in OnLaunched, one call creates the window, gives the page its menu and connects it:
//    _window = this.OpenMainWindow(new MainPage());
```

Create the project with the template of your framework, add the host package, then follow the setup above. See each host's README for the details ([Razor](https://github.com/cedric56/Routeables/blob/master/Routeables.Razor/README.md), [MAUI](https://github.com/cedric56/Routeables/blob/master/Routeables.Maui/README.md), [Avalonia](https://github.com/cedric56/Routeables/blob/master/Routeables.Avalonia/README.md), [Uno](https://github.com/cedric56/Routeables/blob/master/Routeables.Uno/README.md), which has its own quick start).

#### The words

A few names look alike. They are different things:

| Name | What it is |
| --- | --- |
| `IRouter` | What a view model calls to go to another view |
| `[Routeable("/path")]` | Attribute on a **view model**: its route |
| `[RouteableView(typeof(VM))]` | Attribute on a **view**: pairs it with a view model when the names do not match |
| `RouteableFor<…>` | The **base class of a view** (page, user control) |
| `IRouteable` | Optional **lifecycle hooks** (`CanLeaveAsync`, `OnEnteringAsync`…) of a view model or a view |
| `IRouteableEntry`, `RouteableEntry<T>` | A **menu entry** (a title, an icon, a route) listed by the main view model |
| `IMainRouteables`, `MainViewModelBase` | The **menu** (or tab bar): the list of entries and the selected one |

The verbs of the setup calls follow one rule:

| Verb | Means | Examples |
| --- | --- | --- |
| `Use` | Starts Routeables on the host (once, at startup) | `UseRouteables`, `UseRouteablesServices` |
| `Register` | Declares **one thing** of your application | `RegisterRoute`, `RegisterMainRouteables`, `RegisterSegment` |
| `Add` | Turns **a feature** on | `AddGeneratedRoutes`, `AddLocking`, `AddRegion`, `AddRouteCoordinator` |

### 3. Route from a view model

View models only need `IRouter`:

```csharp
public sealed class HomeViewModel(IRouter router)
{
    public async Task OpenOrderAsync(int orderId)
    {
        RouteableResult result = await router.GoToAsync($"/orders/{orderId}");

        if (result.Cancelled)
            return; // for example, the current page vetoed leaving

        if (!result.Success)
            throw new InvalidOperationException(result.Exception?.Message ?? "Navigation failed.");
    }
}
```

`IRouter` also offers `GoBackAsync`, `ForwardAsync`, `PopAsync`, `GoBackToRootAsync`, `MoveToAsync` and `EraseHistoryAsync`. The next section shows how to avoid writing route strings by hand.
