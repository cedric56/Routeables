# Getting started

## Installation

The packages target **.NET 10** (`net10.0`), so you need a .NET 10 SDK. MAUI also needs the MAUI workload.

Add the package of your host (`Routeables.Razor`, `Routeables.Maui`, `Routeables.Avalonia`, `Routeables.Uno` or `Routeables.Wpf`) to your application project:

```bash
dotnet add package Routeables.Avalonia
```

It brings `Routeables`, which holds the routing, the abstractions and the source generator: there is nothing else to add.

The generator must run in **every project that contains attributes it should process**: the project with the views and, if you use `[RouteableViewModel]`, the project with the view models. A project that is not a host (a class library with view models, a module) adds `dotnet add package Routeables` (see [Typed route methods](guides/typed-routes.md#typed-routing-methods-source-generator)).

## Quick start

### 1. Declare routes

Put `[Routeable]` on the view model to give it a route. The view is the class named after it:

```csharp
// Samples/Bases/XAnimals.ViewModels/ViewModels/Animals/DogDetailViewModel.cs
using Routeables.Abstraction;

[Routeable("/dogs/{name}")]
public class DogDetailViewModel(IRouter router, string name, Action? back = null)
    : AnimalDetailViewModel(router, DogData.Dogs.FirstOrDefault(d => d.Name == name), back);

// Samples/XAnimals.Avalonia/Views/Animals/DogDetailView.axaml.cs (use the host's base type)
public partial class DogDetailView : RouteableFor<DogDetailViewModel> { }
```

`DogDetailViewModel` is paired with `DogDetailView`, a routed view (it derives from the host's `RouteableFor`) found in the project or in the assemblies it references.
Use `[RouteableView(typeof(DogDetailViewModel))]` on the view when the names do not follow that convention (`NoteView` of the samples does). Two views of the same name raise `ROUT202`.

The source generator turns these attributes into registrations. Register them once at startup:

```csharp
using Routeables.Generated;

services.AddGeneratedRoutes();
```

You can also register routes by hand with `services.RegisterRoute<DogDetailView, DogDetailViewModel>(...)`.

> [!NOTE]
> `AddGeneratedRoutes()` is generated as an `internal` extension in **each project** that references the generator, so it must be called from the project that generated it (normally the application project). It collects the attributes of the current project and of the assemblies it references.

> [!IMPORTANT]
> **Blazor:** the generator cannot read `.razor` files of its own project (the Razor compiler is another generator, and generators do not see each other's output). A view written in Razor is found in two cases: its `[RouteableView(...)]` is on a C# `partial class` of the same name (`DogDetailView.razor.cs`), or the view is in a **referenced** project, and `AddGeneratedRoutes()` is called from the application project that references it. An `@attribute [RouteableView(...)]` inside the Razor file, in the project that calls `AddGeneratedRoutes()`, registers nothing.

### 2. Set up your host

One call per host starts Routeables, registers your main view model (its lifetime is the host's choice) and takes a block `r => ...` for your own registrations. The block is the same on the five hosts. The same call registers the dialogs and notifications of the host. Only the shape of the call differs, because each framework has its own way to start.

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

// Avalonia (App.axaml.cs): the view argument shows the window with its menu (or: OpenMainView in OnFrameworkInitializationCompleted)
AppBuilder.Configure<App>().UseRouteables<MainViewModel>(r => r.AddGeneratedRoutes(),
    view: lifetime => lifetime is IClassicDesktopStyleApplicationLifetime ? new MainWindow() : new MainView());

// Uno Platform (App.xaml.cs, in the constructor): the container and the window in one call (or: OpenMainWindow in OnLaunched)
Services = this.UseRouteables<MainViewModel>(r => r.AddGeneratedRoutes(), () => new MainPage());

// WPF (App.xaml.cs, in the constructor or in OnStartup): the container and the main window in one call
this.UseRouteables<MainViewModel>(r => r.AddGeneratedRoutes(), () => new MainView());
```

Create the project with the template of your framework, add the host package, then follow the setup above. See each host's README for the details ([Razor](https://github.com/cedric56/Routeables/blob/master/Routeables.Razor/README.md), [MAUI](https://github.com/cedric56/Routeables/blob/master/Routeables.Maui/README.md), [Avalonia](https://github.com/cedric56/Routeables/blob/master/Routeables.Avalonia/README.md), [Uno](https://github.com/cedric56/Routeables/blob/master/Routeables.Uno/README.md), which has its own quick start, [WPF](https://github.com/cedric56/Routeables/blob/master/Routeables.Wpf/README.md)).

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
| `IMainViewModel`, `MainViewModelBase` | The **menu** (or tab bar): the list of entries and the selected one |

The verbs of the setup calls follow one rule:

| Verb | Means | Examples |
| --- | --- | --- |
| `Use` | Starts Routeables on the host (once, at startup) | `UseRouteables`, `UseRouteablesServices` |
| `Register` | Declares **one thing** of your application | `RegisterRoute`, `RegisterMainViewModel`, `RegisterSegment` |
| `Add` | Turns **a feature** on | `AddGeneratedRoutes`, `AddLocking`, `AddRegion`, `AddRouteCoordinator` |

### 3. Route from a view model

View models only need `IRouter`:

```csharp
public class DogsViewModel(IRouter router)
{
    public async Task OpenDogAsync(string name)
    {
        RouteableResult result = await router.GoToAsync($"/dogs/{Uri.EscapeDataString(name)}");

        if (result.Cancelled)
            return; // for example, the current page vetoed leaving

        if (!result.Success)
            throw new InvalidOperationException(result.Exception?.Message ?? "Navigation failed.");
    }
}
```

`IRouter` also offers `GoBackAsync`, `ForwardAsync`, `PopAsync`, `GoBackToRootAsync`, `MoveToAsync` and `EraseHistoryAsync`. The next section shows how to avoid writing route strings by hand.
