# Getting started

## Installation

The packages target **.NET 10** (`net10.0`), so you need a .NET 10 SDK. MAUI also needs the MAUI workload.

Add the package of your host (`Routeables.Blazor`, `Routeables.Maui`, `Routeables.Avalonia`, `Routeables.Uno` or `Routeables.Wpf`) to your application project:

```bash
dotnet add package Routeables.Avalonia
```

It brings `Routeables`, which holds the routing, the abstractions and the source generator: there is nothing else to add.

The generator must run in **every project that contains attributes it should process**: the project with the views and, if you use `[RouteableViewModel]`, the project with the view models. A project that is not a host (a class library with view models, a module) adds `dotnet add package Routeables` (see [Typed route methods](guides/typed-routes.md#typed-routing-methods-source-generator)).

## Your first page

To skip the typing: `dotnet new install Routeables.Templates`, then `dotnet new routeables-avalonia -n MyApp` (see `templates/`).

Two ways to register a page, both in about a minute and without a menu: with the generator (`AddGeneratedRoutes()`), or by name, one line per page, without the generator. The example is Avalonia; the other hosts start the same way (see [Set up your host](#2-set-up-your-host)).

### With the generator

**1. A page.** The route is on the view model; the view is the class with the matching name:

```csharp
[Routeable("/hello/{name}")]
public sealed class HelloViewModel(IRouter router, string name)
{
    public string Greeting { get; } = $"Hello, {name}!";
}

public partial class HelloView : RouteableFor<HelloViewModel> { }
```

**2. Start Routeables:**

```csharp
AppBuilder.Configure<App>()
    .UseRouteables(r => r.AddGeneratedRoutes(),
        view: lifetime => new MainWindow());
```

**3. Say where the pages appear, and which page opens first**, in your main view (Blazor: `<RouteableRegion Route="/hello/World" />`):

```xml
<routeables:RouteableRegion Route="/hello/World" />
```

The region starts the routing: when it appears and its router shows nothing yet, it opens its `Route`. Without a `Route`, the default region opens `/`.

**4. Go to another page**, from any view model that asks for an `IRouter`:

```csharp
await router.GoToAsync("/hello/Routeables");
```

### By name, without the generator

**1. A page.** Any view and any class as its view model; nothing to derive from, no attribute:

```csharp
public sealed class HomeViewModel(IRouter router)
{
    public string Greeting => "Hello!";
    public Task GoToAbout() => router.GoToAsync("about");
}

public partial class HomeView : UserControl { public HomeView() => InitializeComponent(); }   // plain Avalonia view
```

`AboutViewModel` and `AboutView` are written the same way (a view model without any member is enough).

**2. Register it under a name:**

```csharp
AppBuilder.Configure<App>()
    .UseRouteables(r =>
        {
            r.RegisterRoute<HomeView, HomeViewModel>(route: "home");
            r.RegisterRoute<AboutView, AboutViewModel>(route: "about");
        },
        view: lifetime => new MainWindow());
```

**3. Say where the pages appear and which page opens first** (`<routeables:RouteableRegion Route="home" />`) and **go to a page** with `await router.GoToAsync("about");`, as in the first way.

The router gives the view model to the view as its `DataContext` (`BindingContext` on MAUI) even though the view is not a `RouteableFor`. A page registered this way has no lifecycle (initialization, disposal), and nothing checks the names before the program runs: `RouteableFor<T>` and the generator add both.

That is all: the rest of this page is what you add when you need it. A main view model (a menu, a tab bar) is only needed for a menu: write one when you have one, and use `UseRouteables<MainViewModel>(...)` (see below).

## In detail

### 1. Declare routes

Put `[Routeable]` on the view model to give it a route. The view is the class named after it:

```csharp
// samples/Bases/XAnimals.ViewModels/ViewModels/Animals/DogDetailViewModel.cs
using Routeables.Abstraction;

[Routeable("/dogs/{name}")]
public class DogDetailViewModel(IRouter router, string name, Action? back = null)
    : AnimalDetailViewModel(router, DogData.Dogs.FirstOrDefault(d => d.Name == name), back);

// samples/XAnimals.Avalonia/Views/Animals/DogDetailView.axaml.cs (use the host's base type)
public partial class DogDetailView : RouteableFor<DogDetailViewModel> { }
```

`DogDetailViewModel` is paired with `DogDetailView`, a routed view (it derives from the host's `RouteableFor`) found in the project or in the assemblies it references.
Use `[RouteableView(typeof(DogDetailViewModel))]` on the view when the names do not follow that convention (`NoteView` of the samples does). Two views of the same name raise `ROUT202`.

A view that does not derive from `RouteableFor` (a plain `UserControl`, registered with `RegisterRoute<TView, TViewModel>("/route")`) still gets its view model as its `DataContext` (`BindingContext` on MAUI): each host registers an `IRouteableViewBinder` for that. It does not get the lifecycle of `RouteableFor` (initialization, disposal) and the generator does not find it.

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

One call per host starts Routeables, registers your main view model (its lifetime is the host's choice; without a menu, call it with no type: the `Route` of the main `RouteableRegion` chooses the first page) and takes a block `r => ...` for your own registrations. The block is the same on the five hosts. The same call registers the dialogs and notifications of the host. Only the shape of the call differs, because each framework has its own way to start.

```csharp
// Blazor (Program.cs)
builder.Services.UseRouteablesServer<MainViewModel>(      // or UseRouteablesWebAssembly, UseRouteablesHybrid
    r => r.AddGeneratedRoutes());
app.MapRazorComponents<App>()
   .AddInteractiveServerRenderMode()
   .AddAdditionalAssemblies(typeof(MainLayoutBase).Assembly);    // Routeables.Blazor: catch-all page, layout, not-found page

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

Create the project with the template of your framework, add the host package, then follow the setup above. See each host's README for the details ([Blazor](https://github.com/cedric56/Routeables/blob/master/src/Routeables.Blazor/README.md), [MAUI](https://github.com/cedric56/Routeables/blob/master/src/Routeables.Maui/README.md), [Avalonia](https://github.com/cedric56/Routeables/blob/master/src/Routeables.Avalonia/README.md), [Uno](https://github.com/cedric56/Routeables/blob/master/src/Routeables.Uno/README.md), which has its own quick start, [WPF](https://github.com/cedric56/Routeables/blob/master/src/Routeables.Wpf/README.md)).

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

The shortest form is one line:

```csharp
await router.GoToAsync("/dogs/Rex");
```

To react to what happened, read the result:

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
