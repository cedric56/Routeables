<p align="center">
  <img src="Images/logo.svg" alt="Routeables" width="460">
</p>

<p align="center">
  <a href="https://github.com/cedric56/Routeables/actions/workflows/ci.yml"><img src="https://github.com/cedric56/Routeables/actions/workflows/ci.yml/badge.svg?branch=master" alt="CI"></a>
  <a href="LICENSE.txt"><img src="https://img.shields.io/badge/license-MIT-5B35E0" alt="MIT License"></a>
  <img src="https://img.shields.io/badge/.NET-10-512BD4?logo=dotnet&logoColor=white" alt=".NET 10">
  <img src="https://img.shields.io/badge/version-1.0.0--preview.1-orange" alt="1.0.0-preview.1">
</p>

> [!WARNING]
> **Routeables is under active development (`1.0.0-preview.1`).** APIs change between commits. Just for experimenting.

# Routeables

**View-model-first routing for .NET, with one routing layer for Blazor, .NET MAUI, Avalonia and Uno Platform.**

Your view models depend on a single `IRouter` interface instead of framework-specific routing APIs, so the same routing code is shared across UI stacks. Routes are declared with attributes and wired by Roslyn source generators, and routing calls to a view model can be generated as strongly typed methods that mirror its constructor.

## Hello world

```csharp
public partial class HelloView : RouteableFor<HelloViewModel> { }   // the page: HelloViewModel => HelloView, by name
                                                                    // (Blazor: @inherits RouteableFor<HelloView, HelloViewModel>)

[Routeable("/hello/{name}")]                                // its route: the only attribute
public sealed class HelloViewModel(IRouter router, string name)
{
    public Task SayHelloAgainAsync() => router.GoToAsync("/hello/Ada");
}

services.AddGeneratedRoutes();                              // at startup, after the host's UseRouteables(...)
```

That is the whole model: a route on the view model, the view that has the matching name, one registration call, and an `IRouter` to move. The rest of this page adds one idea at a time; what you can leave for later is under [Advanced](#advanced).

## What do I install?

Start from the host of your UI framework and add only what you use. Every package below is optional unless it says otherwise.

| I want | I reference | I call |
| --- | --- | --- |
| Routing in a Blazor, MAUI, Avalonia or Uno app | the host: `Routeables.Razor`, `Routeables.Maui`, `Routeables.Avalonia` or `Routeables.Uno` (it brings `Abstraction`, `Routing` and `Core`) | `UseRouteables(...)` |
| Routes declared by attributes and typed route methods | `Routeables.Generated` as an analyzer | `AddGeneratedRoutes()` |
| Dialogs, content dialogs, task dialogs, notifications (and on Blazor the default layout) | `Routeables.Razor.Services`, `Routeables.Maui.Services`, `Routeables.Avalonia.Services` or `Routeables.Uno.Services` | `UseRouteablesServices(...)` |
| MudBlazor, CommunityToolkit.Maui or FluentAvalonia dialogs instead of the plain ones | the matching [Reshape](Reshapes/README.md) | `UseRouteablesMudBlazor(...)`, `UseRouteablesCommunityToolkit()` or `new ReshapeMessageBoxService()` |
| Pages that can block leaving and lock the menu | `Routeables.Locking` | `AddLocking()` |
| Routing by segments (`Orders?id=5/Detail`) | `Routeables.Segments` | `AddRouteCoordinator()` |
| A host for another UI framework, or no UI at all (tests, services) | `Routeables.Abstraction` and `Routeables.Routing` only | `RegisterRouting()` |
| Only the contracts, for a class library that routes | `Routeables.Abstraction` | nothing: view models take an `IRouter` |

A project that only routes carries no dialog, notification, region-follower or segment code unless it references the package that has it.

## Table of contents

- [Hello world](#hello-world)
- [What do I install?](#what-do-i-install)
- [Features](#features)
- [Projects](#projects)
- [Installation](#installation)
- [Quick start](#quick-start)
- [Routes and templates](#routes-and-templates)
- [Passing arguments](#passing-arguments)
- [Typed routing methods (source generator)](#typed-routing-methods-source-generator)
- [History](#history)
- [Shared services](#shared-services)
- [Hosts](#hosts)
- [Samples](#samples)
- [Advanced](#advanced)
  - [Lifecycle and guards](#lifecycle-and-guards)
  - [Regions](#regions)
  - [Events](#events)
  - [View models in Core](#view-models-in-core)
  - [Native AOT and trimming](#native-aot-and-trimming)
  - [Diagnostics reference](#diagnostics-reference)
- [Documentation](#documentation)
- [Building and testing](#building-and-testing)
- [Contributing](#contributing)
- [License](#license)

## Features

- **View-model-first routing**: route by route string with typed segments such as `/orders/{id:int}`, or by a view model's declared route.
- **Source-generated registration**: a `[Routeable]` view model is paired with the view of the same name (`OrderViewModel` => `OrderView`); `[RouteableView]` is there for the other cases. Then call `AddGeneratedRoutes()`, with no manual wiring.
- **Generated typed routing**: `[RouteableViewModel(typeof(Caller))]` generates `GoTo{Name}Async` on the caller only, with the destination's constructor parameters as arguments, checked at compile time.
- **Back/forward history**: back, forward, pop, move to an entry, or return to the root.
- **Routing guards**: an async `CanLeaveAsync` can veto leaving a page.
- **Lifecycle hooks**: `IRouteable` and `IInitializeAsync` (plain and typed), plus standard `IDisposable` / `IAsyncDisposable` cleanup when an entry leaves the history.
- **Regions**: independent history stacks (for example `main` and `side`), each with its own history.
- **Shared services**: dialogs, content dialogs, task dialogs, notifications and theme switching behind common interfaces.
- **Native AOT and trimming**: the libraries are annotated and analysed with `IsAotCompatible`; registration, routing, route parameters, segments, regions and property injection read only types the compiler or the generator described. CI publishes a Native AOT app and a trimmed Blazor WebAssembly app to prove it (see [Native AOT and trimming](#native-aot-and-trimming)).
- **Four UI hosts**: Blazor (Razor), .NET MAUI, Avalonia and Uno Platform (preview).
- **Optional MVVM helpers**: a view-model base for CommunityToolkit.Mvvm, opt in through an MSBuild property (ReactiveUI is planned, not shipped yet).

Routeables routes; it does not choose your MVVM library. It ships no commands, no view-model locator, no event aggregator and no modules: use CommunityToolkit.Mvvm, ReactiveUI or plain C#. Coming from Prism? See [Migrating from Prism](docs/guides/migrating-from-prism.md).

## Projects

| Project | Purpose |
| --- | --- |
| [`Routeables.Abstraction`](Routeables.Abstraction/README.md) | Shared contracts: `IRouter`, `IRouteable`, `RouteableArgs`, attributes, lock and error contracts |
| [`Routeables.Services`](Routeables.Services/README.md) | Optional UI service contracts: dialogs, content and task dialogs, notifications, theme |
| [`Routeables.Razor.Services`](Routeables.Razor.Services/README.md) / [`Routeables.Maui.Services`](Routeables.Maui.Services/README.md) / [`Routeables.Avalonia.Services`](Routeables.Avalonia.Services/README.md) / [`Routeables.Uno.Services`](Routeables.Uno.Services/README.md) | Optional: the dialogs, modals and notifications of each host (`UseRouteablesServices`), with the hosts and the default layout on Blazor |
| [`Routeables.Locking`](Routeables.Locking/README.md) | Optional: the broker (`IRouteableBrokerService`) and the locking of the main entries (`AddLocking()`) |
| [`Routeables.Regions`](Routeables.Regions/README.md) | Optional: `RegionBinding`, `RegionFollower`, `RegionLayout` and `AddRegionLayout` |
| [`Routeables.Segments`](Routeables.Segments/README.md) | Optional: routing by segments (`Orders?id=5/Detail`): `IRouteCoordinator`, `RouteCoordination`, `RegisterSegment` |
| [`Routeables.Routing`](Routeables.Routing/README.md) | The router, history, route locking and DI registration |
| [`Routeables.Core`](Routeables.Core/README.md) | Routing-aware view-model bases and runtime/environment info |
| [`Routeables.Generated`](Routeables.Generated) | Roslyn source generators: route registration and typed route methods |
| [`Routeables.Razor`](Routeables.Razor/README.md) | Blazor integration |
| [`Routeables.Maui`](Routeables.Maui/README.md) | .NET MAUI integration |
| [`Routeables.Avalonia`](Routeables.Avalonia/README.md) | Avalonia integration |
| [`Routeables.Uno`](Routeables.Uno/README.md) | Uno Platform integration (preview), with a quick start |
| [`Samples`](Samples) | Sample apps (see [Samples](#samples)) |
| [`Reshapes`](Reshapes/README.md) | Optional look-and-feel packages: MudBlazor, CommunityToolkit.Maui, FluentAvalonia |
| [`Routeables.Benchmarks`](Routeables.Benchmarks) | BenchmarkDotNet benchmarks and memory checks of the router (see [docs/benchmarks.md](docs/benchmarks.md)) |
| [`Routeables.Test`](Routeables.Test) | Unit tests (xunit v3) |
| [`docs`](docs) | DocFX documentation site |

## Installation

The projects target **.NET 10** (`net10.0`), so you need a .NET 10 SDK. MAUI also needs the MAUI workload. For now, use the projects from source:

```bash
git clone https://github.com/cedric56/Routeables.git
```

Add a project reference to the host you need (the host references `Routeables.Core`, which brings in routing and the abstractions), and the source generator as an analyzer:

```xml
<ItemGroup>
  <ProjectReference Include="..\Routeables\Routeables.Avalonia\Routeables.Avalonia.csproj" />
  <!-- or Routeables.Razor / Routeables.Maui -->

  <ProjectReference Include="..\Routeables\Routeables.Generated\Routeables.Generated.csproj"
                    OutputItemType="Analyzer" ReferenceOutputAssembly="false" />
</ItemGroup>
```

Adjust the relative paths to your layout. The generator must be referenced by **every project that contains attributes it should process**: the project with the views and, if you use `[RouteableViewModel]`, the project with the view models (see [Typed route methods](#typed-routing-methods-source-generator)).

### Starting a new app

The `Routeables.Templates` pack creates a working application per host, with a menu, a page that routes and a page with a route parameter:

```bash
dotnet new install ./Routeables/Routeables.Templates      # from the clone, until the packages are published
dotnet new routeables-blazor  -n MyApp --RepoPath ../Routeables
dotnet new routeables-maui    -n MyApp --RepoPath ../Routeables
dotnet new routeables-avalonia -n MyApp --RepoPath ../Routeables
```

With `--RepoPath`, the app references the projects of your clone; without it, it references the NuGet packages (not published yet).

## Quick start

### 1. Declare routes

Put `[Routeable]` on the view model to give it a route. The view is the class named after it:

```csharp
using Routeables.Abstraction;

[Routeable("/orders/{id:int}")]
public sealed class OrderViewModel(IRouter router, int id) { }

public partial class OrderView : RouteableFor<OrderViewModel> { }   // Avalonia; use the host's base type
```

`OrderViewModel` is paired with `OrderView`: a concrete class whose name is the view model's without `ViewModel`, plus `View`, and that is a routed view
(it derives from the host's `RouteableFor`, so it implements `IRouteable`). The route, region and lifetime are the ones of `[Routeable]`. The generator
looks in the project first, then in the assemblies it references. Two views of that name are not guessed between: `ROUT202` asks you to choose.

Use `[RouteableView(typeof(OrderViewModel))]` on the view when the names do not follow the convention, when one view serves several view models, or to
give the route or the lifetime on the view itself. A view model named by a `[RouteableView]` is never paired by name.

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

Each host registers the router with one call, then adds your routes and your main view model. The dialogs and notifications are a second, optional call from a separate package per host (a host that only routes carries no UI code). The forms differ because each framework has its own way to start; the three things to register are always the same: **the host**, **`AddGeneratedRoutes()`** and **your main view model**.

```csharp
// Blazor (Program.cs)
builder.Services.UseRouteables(Runtime.BlazorServer);        // or BlazorWebAssembly, MauiHybrid
builder.Services.UseRouteablesServices();                    // optional: dialogs, notifications, default layout
builder.Services.AddGeneratedRoutes();
builder.Services.RegisterMainRouteables<MainViewModel>(ServiceLifetime.Scoped);   // one menu per circuit
app.MapRazorComponents<App>()
   .AddInteractiveServerRenderMode()
   .AddAdditionalAssemblies(typeof(MainLayoutBase).Assembly,     // Routeables.Razor: the catch-all page
                            typeof(MainLayout).Assembly);        // Routeables.Razor.Services: layout, not-found page

// .NET MAUI (MauiProgram.cs)
builder.UseMauiApp<App>()
       .UseRouteables()
       .UseRouteablesServices();                             // optional: dialogs, notifications
builder.Services.AddGeneratedRoutes();
builder.Services.RegisterMainRouteables<MainViewModel>(ServiceLifetime.Singleton);

// Avalonia (App.axaml.cs)
AppBuilder.Configure<App>().UseRouteables(services =>
{
    services.UseRouteablesServices("avares://MyApp/Assets/icon.ico");   // optional; the icon is the window icon of the dialogs
    services.AddGeneratedRoutes();
    services.RegisterMainRouteables<MainViewModel>(ServiceLifetime.Singleton);
});
// ...then, in OnFrameworkInitializationCompleted, show the window with its menu:
//    this.OpenMainView(new MainWindow());

// Uno Platform (App.xaml.cs)
Services = this.UseRouteables(services =>
{
    services.UseRouteablesServices();                        // optional: dialogs, notifications
    services.AddGeneratedRoutes();
    services.RegisterMainRouteables<MainViewModel>(ServiceLifetime.Singleton);
});
// ...then, in OnLaunched, one call creates the window, gives the page its menu and connects it:
//    _window = this.OpenMainWindow(new MainPage());
```

`dotnet new routeables-blazor`, `routeables-maui` and `routeables-avalonia` (see [Starting a new app](#starting-a-new-app)) create an app with exactly this setup. See each host's README for the details ([Razor](Routeables.Razor/README.md), [MAUI](Routeables.Maui/README.md), [Avalonia](Routeables.Avalonia/README.md), [Uno](Routeables.Uno/README.md), which has its own quick start).

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

`GoToAsync(url)` takes the address as a browser would, with its parameters; no template is needed. (`GoToRouteAsync` is the older form that also takes the template; use it only for a route that is not registered.) `IRouter` also offers `GoBackAsync`, `ForwardAsync`, `PopAsync`, `GoBackToRootAsync`, `MoveToAsync` and `EraseHistoryAsync`. [Typed route methods](#typed-routing-methods-source-generator) shows how to avoid writing route strings by hand.

## Routes and templates

A **route** is the concrete string you go to (`/orders/42`). A **template** is the registered pattern that is matched against it (`/orders/{id:int}`). The router parses the route against the template and hands the parsed values to the destination.

| Placeholder | Meaning |
| --- | --- |
| `{id}` | Required segment, string |
| `{id:int}` | Required segment with a type: `string`, `int`, `long`, `bool`, `guid`, `decimal`, `double` |
| `{id?}` | Optional segment |
| `?{tab}` / `?{tab?}` | Query-string value, required / optional |

Query values that are not declared in the template are kept as neutral values (`IRouteableArgs.Values`, read by the lifecycle hooks): they are never given to the constructor or injected into a property, so a link with `?utm_source=mail` still opens the page, and nobody can fill a constructor parameter from the address.

`[Routeable]` takes a path, an optional region and a service lifetime. It can be repeated on the same class to give a view model several routes:

```csharp
[Routeable("/orders/{id:int}")]
[Routeable("/o/{id:int}")]
public sealed class OrderViewModel(IRouter router, int id) { }
```

### Main view model per host

**Which base class?** Most view models need none.

| You write | Derive from | Needs |
| --- | --- | --- |
| **A page** (the usual view model) | Nothing: a plain class that takes `IRouter` in its constructor. Add `IRouteable` for the hooks (`CanLeaveAsync`, `OnEnteringAsync`...). | |
| **The menu or tab bar** | `MainViewModelBase` | Nothing |
| The menu, with CommunityToolkit.Mvvm | `RouteableViewModel` | `;CommunityToolkit;` feature (see [Routeables.Core](Routeables.Core/README.md#mvvm-flavours)) |
| **A view model shown in a dialog** (it closes with a result) | `CloseableViewModel<TResult>` | `;CommunityToolkit;` feature |
| The same, without CommunityToolkit.Mvvm | `CloseableViewModelBase<TResult>` | Nothing |
| Your own base (another MVVM library) | `RouteableViewModelBase` for a menu, `RouterViewModelBase` for back and forward only | Nothing |

Only the menu and the dialogs need a base class. `RouteableViewModel` is the CommunityToolkit flavour of the menu base: it has nothing to do with the `[RouteableViewModel]` attribute, which asks the generator for a typed route method.

Derive from `MainViewModelBase` (in `Routeables.Core`) and return the entries: it selects the entry, shows the view, refuses a page that cannot be left and, when `AddLocking()` is called, locks its entries while a page blocks leaving, with no MVVM library:

```csharp
public sealed class MainViewModel(IRouter router) : MainViewModelBase(router)
{
    protected override ObservableCollection<IRouteableEntry> GetRouteables() =>
    [
        new RouteableEntry<CatsViewModel>("Cats", route: "/cats", template: "/cats"),
        new RouteableEntry<DogsViewModel>("Dogs", route: "/dogs", template: "/dogs"),
    ];
}
```

Build the entries from code only, not from a field or a constructor parameter: `GetRouteables` runs while the base class is constructed. Implement `IMainRouteables` yourself only when you need full control.

The menu or tab bar (`IMainRouteables`) is registered by the host with `services.RegisterMainRouteables<MainViewModel>(lifetime)`: `Scoped` on Blazor Server (one per circuit), `Singleton` on Avalonia and MAUI. A `Transient` one is refused with an `ArgumentException`, because each consumer would get another instance than the one the window shows. At start the host calls `IMainRouteables.SelectFirst()`, which selects the first entry when none is selected, the same way on the three hosts.

### Lifetimes

A view and its view model are `Transient` by default: a new pair is built for each route change. Declare another lifetime on the attribute; `ViewModelLifetime` and `ViewLifetime` set one of the two, and `Lifetime` (or the constructor argument) sets both:

```csharp
// One cart for the whole app, the view is built again for each navigation.
[RouteableView(typeof(CartViewModel), "/cart", ViewModelLifetime = ServiceLifetime.Singleton)]
public partial class CartView { }

// Both scoped (one pair per scope: per circuit in Blazor Server).
[RouteableView(typeof(ProfileViewModel), "/profile", serviceLifetime: ServiceLifetime.Scoped)]
public partial class ProfileView { }
```

Without the generator, `services.RegisterRoute<TView, TViewModel>(viewModelLifetime:, viewLifetime:, route)` takes the same two lifetimes.

A `Scoped` or `Singleton` instance is cached by its route and built once, with the arguments of the first route change: later route changes to the same route do not pass theirs again. Keep pages that receive a different argument each time (a detail page) `Transient`.

## Passing arguments

Routing arguments travel in an `IRouteableArgs`. Each entry goes to the view model, to the view, or to neither (a shared bag of values):

```csharp
var args = RouteableArgs.Create()
    .ForViewModel(order)                  // matched to a constructor parameter by type
    .WithView("mode", "highlight")        // a named value, passed to the view
    .WithValue("cid", correlationId);     // available to lifecycle hooks, not injected

await router.GoToAsync("/orders/42", args);
```

`ForViewModel`, `ForView` and `ForValues` take plain values (several at once: `ForViewModel(order, back)`); `WithViewModel`, `WithView` and `WithValue` take a name and a value. In a hook, read the values by name: `args.Get<Action>("back")` (or `args.Get<Animal>()` when only one value has that type; two of the same type throw instead of silently taking the first).

`new RouteableArgument(value, name, attribute)` is still there for the rare argument that needs the `attribute` flag in a list.

### Parameters by attribute

A template parameter written `{name::}` (two colons) is not given to a constructor: it is set on a property that has a platform attribute. The property can be on the view model or on the view; a value is given to the view only when it has a property of that name.

| Platform | Attribute | Where |
|---|---|---|
| Blazor | `[Parameter]` | a property of the component: `[Parameter] public string? Panda { get; set; }` |
| MAUI | `[QueryProperty(nameof(Panda), "panda")]` | on the class, naming the property (or `[RouteableParameter]` on the property) |
| Avalonia, Uno | `[RouteableParameter]` (namespace `Routeables.Abstraction`) | on the property |

```csharp
// "/pandas/{panda::}" opens PandaDetailView, and "Red Panda" ends up in its Panda property.
[RouteableView(typeof(PandaDetailViewModel), route: "/pandas/{panda::}")]
```

The view is created before its view model is set, so a view hands the value over when both exist (`PandaDetailView` in the samples).

View-model entries are matched to constructor parameters **by type**, in order. Registered services fill the remaining parameters. Entries with a `null` value are dropped.

For a single typed value delivered after construction, implement `IInitializeAsync<TData>` on the view model and use the typed overload, which fails to compile if the type does not match:

```csharp
public sealed class OrderViewModel : IInitializeAsync<Order>
{
    public Task InitializeAsync(Order data, CancellationToken ct = default) { /* ... */ return Task.CompletedTask; }
}

await router.GoToRouteAsync<OrderViewModel, Order>("/orders/42", "/orders/{id:int}", order);
```

Prefer constructor parameters for required data (the object is valid as soon as it exists, and the generator checks them at compile time). Use `IInitializeAsync` for work that must be asynchronous.

## Typed routing methods (source generator)

Writing route strings and argument lists by hand is easy to get wrong. `[RouteableViewModel]` generates a strongly typed method instead.

```csharp
[Routeable("/orders/{id:int}")]
[RouteableViewModel(typeof(HomeViewModel))]
public sealed class OrderViewModel(IRouter router, int id, string? note = null) { }
```

generates, **on `HomeViewModel` only**:

```csharp
Task<RouteableResult> GoToOrderAsync(
    this HomeViewModel caller,
    IRouter router,
    int id,
    string? note = null,
    CancellationToken cancellationToken = default);
```

which you call like this:

```csharp
public sealed class HomeViewModel(IRouter router)
{
    public Task OpenAsync() => this.GoToOrderAsync(router, id: 42, note: "urgent");
}
```

How it works:

- **Scoped to the caller.** The types given to the attribute are the only ones that see the method. Another type that calls it gets a compiler error. This keeps `IRouter` free of one method per destination.
- **`IRouter` stays an explicit parameter**, because it is shared with the caller (same instance, same history).
- **Parameters mirror the destination's constructor.** The constructor is chosen like `ActivatorUtilities` does: the one marked `[ActivatorUtilitiesConstructor]`, otherwise the public one with the most parameters.
- **Placeholders are filled from matching parameters.** `id` above is written into `/orders/{id}` (invariant culture, URL-escaped). Every other parameter is passed to the view model as a constructor argument.
- **Required parameters are checked.** Required reference-type parameters get a `ThrowIfNull`, and `?` is dropped from their type in the signature. Parameters with a default value keep it and are skipped when `null` or default.
- **Services come from the container.** Mark a constructor parameter with `[FromContainer]` (or `[FromServices]` / `[FromKeyedServices]`) and it is resolved by DI instead of being asked from the caller.
- **Several callers.** `[RouteableViewModel(typeof(A), typeof(B))]` generates the method on both, and the attribute can be repeated.

The attribute accepts three optional named arguments:

| Argument | Default | Purpose |
| --- | --- | --- |
| `Route` | The path of the single `[Routeable]` on the class | The route string routed to. Required when the class has several `[Routeable]` |
| `Template` | `Route` | The template handed to the router to match `Route` against |
| `MethodName` | `GoTo{Name}Async`, where `{Name}` is the class name without its `ViewModel` suffix | Name of the generated method |

```csharp
[RouteableViewModel(typeof(AnimalsViewModel), Route = "/monkeys", MethodName = "OpenMonkey")]
public class MonkeyDetailViewModel(IRouter router, Animal monkey, Action back) { }
```

Constructor arguments are matched **by type, in order** when the view model is created, so avoid two constructor parameters of the same type that can be skipped. The generator warns about this case (`ROUT106`).

### Reference the generator from the project with the view models

The generator runs per project. Add it to the project that **contains the view models** and callers, not only to the application project:

```xml
<!-- XAnimals.ViewModels.csproj -->
<ItemGroup>
  <ProjectReference Include="..\..\Routeables.Generated\Routeables.Generated.csproj"
                    OutputItemType="Analyzer" ReferenceOutputAssembly="false" />
</ItemGroup>
```

The generated methods are `internal`, which is why the caller and the destination live in the same project.

### Seeing the generated code

```xml
<PropertyGroup>
  <EmitCompilerGeneratedFiles>true</EmitCompilerGeneratedFiles>
  <CompilerGeneratedFilesOutputPath>gen</CompilerGeneratedFilesOutputPath>
</PropertyGroup>
<ItemGroup>
  <Compile Remove="gen/**" />
</ItemGroup>
```

The `Compile Remove` line matters: without it the generated files are compiled twice and you get duplicate-definition errors.

## History

Each router keeps a history of entries (view model, view and arguments):

| Operation | Effect |
| --- | --- |
| `GoToAsync` (or `GoToRouteAsync`) | Pushes a new entry. Forward entries are cut off, and the oldest entry is dropped beyond `History.MaxSize` |
| `GoBackAsync` / `ForwardAsync` | Moves the cursor. Nothing is removed, so a page you went back from stays alive for `Forward` |
| `MoveToAsync(index)` | Moves the cursor to an entry |
| `PopAsync` | Goes back and removes the entry you left |
| `GoBackToRootAsync` | Returns to the first entry |
| `EraseHistoryAsync` | Clears the history |

Which one to call: `GoBackAsync` for history you can come back to (a back button next to a forward one), `PopAsync` to leave a page for good (the detail opened from a list, a wizard step), which is what the samples do. Both answer `CanGoBack`.

`router.CanGoBack`, `router.CanGoForward`, `router.CurrentViewModel`, `router.CurrentView` and `router.History` (read-only entries) describe the current state. Operations are serialized by a gate, so two route changes never interleave.

## Shared services

These interfaces live in `Routeables.Services` (namespace `Routeables.Services`) and are implemented by the optional `Routeables.Razor.Services`, `Routeables.Maui.Services` and `Routeables.Avalonia.Services`. Routing does not need them: a project that only routes references `Routeables.Abstraction` and `Routeables.Routing`, and the hosts bring `Routeables.Services` for their theme service.

| Interface | Purpose |
| --- | --- |
| `IRouteableDialogs` | **The one to inject**: gives the three services below as `MessageBox`, `ContentDialog` and `TaskDialog` |
| `IRouteableDialogService` | Message boxes and simple dialogs |
| `IRouteableContentDialogService` | Dialogs that host a view and a view model |
| `IRouteableTaskDialogService` | Multi-step / button-rich task dialogs |
| `IRouteableNotificationService` | In-app toast notifications |
| `IRouteableRequestedThemeService` | Light/dark theme switching |

## Hosts

| Host | Setup call | Notes |
| --- | --- | --- |
| Blazor ([`Routeables.Razor`](Routeables.Razor/README.md)) | `services.UseRouteables(Runtime.BlazorServer \| BlazorWebAssembly \| MauiHybrid)`, then `services.UseRouteablesServices()` for the dialogs, modals and notifications (`Routeables.Razor.Services`) | Plain Razor, no component library (MudBlazor is optional, see [`Reshapes`](Reshapes/README.md)). Keeps the browser address in step with the router (`RouteableUrlSync`). Base components: `RouteableFor<TView, TViewModel>`, `RouteableContentFor<T>`, `RouteableModalFor<...>`, `RouteableRegion` |
| .NET MAUI ([`Routeables.Maui`](Routeables.Maui/README.md)) | `builder.UseRouteables()`, then `.UseRouteablesServices()` (`Routeables.Maui.Services`) | Targets `net10.0` plus Android, iOS, Mac Catalyst and Windows. Includes `RouteablesShell`, `RouteableRegion` and `RouteableFor`. CommunityToolkit.Maui popups are optional (`UseRouteablesCommunityToolkit`, see [`Reshapes`](Reshapes/README.md)) |
| Avalonia ([`Routeables.Avalonia`](Routeables.Avalonia/README.md)) | `AppBuilder.UseRouteables(...)`, with `services.UseRouteablesServices(icon)` inside (`Routeables.Avalonia.Services`) | `RouteableFor<TViewModel>`, `RouteableRegion`, `RouteableListBox`, modal dialogs, themes, replaceable `IMessageBoxService` (FluentAvalonia in [`Reshapes`](Reshapes/README.md)) |
| Uno Platform, preview ([`Routeables.Uno`](Routeables.Uno/README.md)) | `Application.UseRouteables(...)` and `Application.OpenMainWindow(page)`, with `services.UseRouteablesServices()` inside (`Routeables.Uno.Services`) | `RouteableFor`, `RouteableFor<TViewModel>`, `RouteableRegion`, `RouteableListView`, `ContentDialog` dialogs, `InfoBar` notifications, themes, replaceable `IMessageBoxService` |

The Blazor, MAUI and Avalonia hosts share one contract, checked by `HostContractTests` (Uno follows the same rules, but is not covered by that test yet): the same services (router, injection rules), the same injection rule (a value attribute never falls back to the container) and one main view model for the whole host. While developing, the Avalonia host (Debug builds) and the MAUI sample build the container with `ValidateOnBuild`, so a registration that cannot be built fails at start with its name; ASP.NET Core does the same by default in the Development environment.

`Runtime` identifies the host (`Runtime.Avalonia`, `Uno`, `Maui`, `MauiHybrid`, `BlazorServer`, `BlazorWebAssembly`) and is available through `IRuntime`.

## Samples

The [`Samples`](Samples) folder contains the same Animals app on several stacks, sharing one `XAnimals.ViewModels` project:

| Sample | Stack |
| --- | --- |
| `XAnimals.ViewModels` | Shared view models (uses `[RouteableViewModel]`) |
| `XAnimals.Avalonia` (+ `.Desktop`, `.Android`, `.iOS`, `.Browser`) | Avalonia |
| `XAnimals.Uno` | Uno Platform (desktop head; preview, see [`Routeables.Uno`](Routeables.Uno/README.md)) |
| `XAnimals.Maui`, `XAnimals.MauiHybrid` | .NET MAUI and MAUI Blazor Hybrid |
| `XAnimals.Razor`, `XAnimals.BlazorWebAssembly`, `Xanimals.BlazorApp` | Blazor |

> [!NOTE]
> The samples are still in progress. `XAnimals.ViewModels/Extensions.cs` holds helpers of the samples themselves (`AddHistoryRegion`, `AddShellRegions`, `AddDebugErrorSink`, `RegisterViewModels`): they are not part of Routeables.

## Advanced

None of this is needed to route. Come back to it when a page must veto leaving, when the window has several areas, or when you ship with Native AOT.

### Lifecycle and guards

Implement any of these on a view model (or a view):

```csharp
public sealed class OrderViewModel : IRouteable, IInitializeAsync, IAsyncDisposable
{
    // Return false to block leaving this page.
    public Task<bool> CanLeaveAsync(CancellationToken ct = default)
        => Task.FromResult(!HasUnsavedChanges);

    public Task OnEnteringAsync(IRouteableArgs context, CancellationToken ct = default)
        => Task.CompletedTask;

    public Task OnLeavingAsync(IRouteableArgs context, CancellationToken ct = default)
        => Task.CompletedTask;

    // Async loading after construction, with the navigation arguments.
    public Task InitializeAsync(IRouteableArgs args, CancellationToken ct = default)
        => Task.CompletedTask;

    // Called once the entry has left the history for good.
    public ValueTask DisposeAsync()
        => ValueTask.CompletedTask;
}
```

Every member of `IRouteable` has a default implementation, so implement only what you need.

Order of events for a route change from page A to page B:

1. `CanLeaveAsync` on A (view model, then view). If one returns `false`, the route change is cancelled and `result.Cancelled` is `true`.
2. `InitializeAsync` on B (view model, then view). Nothing has been torn down yet, so a failure leaves A in place.
3. `OnLeavingAsync` on A, then `OnEnteringAsync` on B.
4. The history is updated, and entries removed by this change are disposed.

#### Cleanup

There is no dedicated cleanup interface: use the standard .NET ones. When an entry leaves the history, the router disposes its view model and its view with `DisposeAsync()` if they implement `IAsyncDisposable`, otherwise with `Dispose()` if they implement `IDisposable`. An exception thrown while disposing is swallowed, so cleanup never fails a route change.

Entries leave the history on `PopAsync`, when a new route cuts off the forward entries, when the oldest entry is dropped beyond `History.MaxSize`, and on `EraseHistoryAsync`. `GoBackAsync`, `ForwardAsync` and `MoveToAsync` only move the cursor, so they dispose nothing.

A view and a view model that are the same object, or an instance shared with an entry that stays in the history, are initialized and disposed once and are never disposed while still in use.

`Singleton` and `Scoped` views and view models belong to the cache that builds them: they are disposed with the provider or the scope, never by the router when they leave the history, since the next route change to the same route shows the same instance.

A menu entry (`IRouteableEntry`) remembers the view and view model it showed last and gives them back on its next click. If the router has disposed them since (the entry left the history), the entry gets new ones instead of a disposed page.

> [!NOTE]
> The DI container also disposes the transient `IDisposable` objects it created when its scope ends, so a view model can be disposed twice. Make `Dispose` idempotent. Blazor does the same for components it renders, so a Razor component must be safe to dispose twice.

#### Services injected into a page

A service that a view or a view model receives (constructor argument or `[Inject]`) and that implements `IDisposable` or `IAsyncDisposable` must be registered `Scoped` or `Singleton`, not `Transient`.

Pages are built from the host's provider: the root provider on Avalonia and MAUI, the circuit's scope on Blazor Server. The .NET container keeps every `Transient` disposable object it creates until that provider or scope is disposed. The router disposes the page, not what the container created for it, so a transient disposable dependency stays in memory after each route change. Measured with a page that takes one: 200 route changes leave 200 instances alive, where a `Scoped` or `Singleton` registration leaves one.

```csharp
services.AddSingleton<IPriceService, PriceService>();   // disposable, shared: fine
services.AddTransient<IFormatter, Formatter>();         // not disposable: fine
// services.AddTransient<IPriceService, PriceService>(); // disposable and transient: kept until the app ends
```

If a page needs its own instance of a disposable service, create it inside the page and dispose it in the page's `Dispose`, which the router calls when the page leaves the history.

### Regions

Use regions when the UI needs several independent history stacks:

```csharp
services.AddRegion("main");
services.AddRegion("side");
```

Each region is a keyed `IRouter` with its own history, gate and lifecycle. Resolve one with the keyed-services API, or with `GetRegion`:

```csharp
var side = provider.GetRequiredKeyedService<IRouter>("side");
```

A route can target a region from its attributes: `[Routeable("/inbox", region: "side")]` or `[RouteableView(typeof(InboxViewModel), region: "side")]`. A region is registered `Scoped` by default.

#### Receiving a region: `RouteableRegion`

A region has no place on screen of its own. A view puts one down with a **region host**, a zone that receives whatever the router of that region displays. Nothing assigns the view to it: not a view model property, not code-behind.

```xml
<!-- Avalonia: xmlns:routeables="using:Routeables.Avalonia" · Maui: xmlns:routeables="clr-namespace:Routeables.Maui;assembly=Routeables.Maui" -->
<routeables:RouteableRegion Region="detail" />
```

```razor
<RouteableRegion Region="detail" />
```

A view can hold several hosts, each with its own name, and without a name a host shows the default router. Whoever routes only names the region; the host that carries that name shows the result:

```csharp
services.AddRegion("list");
services.AddRegion("detail");

// An entry says which region shows it (null: the default router)...
var entry = new RouteableEntry<InboxViewModel>("Inbox", icon, route: "/inbox") { Region = "detail" };

// ...and the provider sends it there. No view model needs to know where "detail" is on screen.
await provider.GoToAsync(entry);
```

A page shown in a region routes that region. The router that builds a view model hands itself to it: a view model whose constructor takes an `IRouter` receives the router of its region, not the default one (`IRouteableArgs.Router`; a router given with `ForViewModel(router)` wins). A list that opens its detail with `_router.GoToRouteAsync(...)` therefore opens it in its own region, and its `Back` or `Pop` returns to the list there.

`provider.RouterFor(entry)` and `provider.GetRouter(region)` give the router itself (`Back`, `Forward`, `Pop`, `History`...). The host follows every change of its history, `Back`, `Forward`, `EraseHistoryAsync` included, shows the view the router already holds when it is created, and lets go of the router when it leaves the screen. Avalonia and Maui hosts find the router in the running host (`CreateRouteableApp`, `UseRouteables`); the `Services` property points a host to another provider. `RegionBinding` is the same behavior without UI, to build a host for another framework.

#### Routing with a result

A page the user comes back from (a picker, a form, a confirmation) gives a value to whoever opened it. The caller waits; the page
implements `IReturnsResult<T>`; the router needs nothing more.

```csharp
// The caller
var picked = await router.GoToForResultAsync<Color>("/colors");     // or GoToForResultAsync<ColorsViewModel, Color>(route)
if (picked.Returned) Apply(picked.Value);                           // picked.OpenResult says whether the page opened

// The page
public sealed class ColorsViewModel(IRouter router) : IReturnsResult<Color>
{
    private Color? _picked;
    public Task PickAsync(Color color) { _picked = color; return router.PopAsync(); }
    public bool TryGetResult(out Color result) { result = _picked.GetValueOrDefault(); return _picked is not null; }
}
```

The call completes when the page is *left for good* (`PopAsync`, `GoBackAsync`, the history cleared). A page that opens another one on
top of itself is not left yet. `Returned` is `false` when the page was left without a value; the wait can be cancelled with its
token (`Cancelled`; the page stays). It works on the router of a region too.

#### Following a region: `RegionFollower`

`RegionFollower`, `RegionLayout` and `RegionBinding` are in [`Routeables.Regions`](Routeables.Regions/README.md) (namespace `Routeables.Regions`); the regions themselves (`AddRegion`, `GetRouter`) are part of routing.

The history of a router changes while the router holds its gate, so a handler of `History.Changed` must not route that
router. To make other regions follow one (a header that says what the content shows, a menu that selects its entry), create a
`RegionFollower` on the source: each change queues an update, the updates run one after the other, never on the route change
that caused them, and on the thread that created the follower (the UI thread when a view started the page).

```csharp
var follower = new RegionFollower(provider.GetRouter("content"), async () =>
    await provider.GoToAsync(Bar("header", provider.GetRouter("content").History.Current)));

await follower.SettledAsync();   // the header says what the content shows
follower.Failed += (_, ex) => log(ex);
follower.Dispose();              // stops following
```

`provider.EraseRegionsAsync(["header", "content", "footer"])` empties a group of regions at once.

#### A layout of regions: `RegionLayout`

A header, a footer, a menu on the side and a content is a common layout. Derive from `RegionLayout`, name the region the user
routes (the *primary*) and the ones that follow it, and write only what is yours: what the regions start with and what the
followers show when the primary moves.

```csharp
public sealed class Shell(IServiceProvider provider) : RegionLayout(provider, "content", "header", "menu")
{
    protected override async Task OnStartAsync(CancellationToken ct)
    {
        await GoToAsync("menu", MenuEntry, ct);
        await ShowAsync(FirstPage, ct);
    }

    // After each move of the content; FollowAsync does nothing when the header would show the same thing.
    protected override Task OnPrimaryChangedAsync()
        => FollowAsync("header", PrimaryRouter.History.Current, () => HeaderFor(PrimaryRouter.History.Current));
}

services.AddRegionLayout<Shell>("content", "header", "menu");   // the regions and the layout, scoped
```

`StartAsync`, `ClearAsync`, `ShowAsync`, `BackAsync`, `ForwardAsync`, `CanGoBack`, `CanGoForward`, `SettledAsync` and `Changed` are
there already, and the followers run one after the other on the thread that started the layout. Set `Following = false` while
the followers show something else (a credits page), back to `true` to follow again.

### Events

```csharp
router.Routing += (_, e) => { /* about to navigate */ };
router.Routed  += (_, e) => { /* navigation finished */ };
```

A handler that throws never fails or masks the route change, and a `Dispose` that throws when an entry leaves the history never fails it either. A coordination (`IRouteCoordinator`) that is rejected or fails is reported the same way, in addition to the result it returns, so an application that ignores that result still sees it. The callbacks you give to dialogs and notifications (`Opened`, `Closing`, `OnClick`...) are run through `RouteableErrors.Guard`, which reports an exception instead of letting it reach the UI; use it for your own callbacks run from an event handler. These errors are not lost: register an `IRouteableErrorSink` (`services.AddSingleton<IRouteableErrorSink, MySink>()`) to log them; without one they go to `System.Diagnostics.Trace`.

Some errors are reported where no container is at hand (the callbacks run by `Guard`, the follower of a region, a view model base). Those reach the sink set on `RouteableErrors.Default`, so one sink can receive everything, for instance to write to Serilog:

```csharp
public sealed class SerilogErrorSink : IRouteableErrorSink
{
    public void Report(string source, Exception exception)
        => Log.Warning(exception, "Routeables: {Source}", source);
}

var sink = new SerilogErrorSink();
RouteableErrors.Default = sink;                                  // errors reported without a container
services.AddSingleton<IRouteableErrorSink>(sink);                // errors reported with one
```

A sink registered in the container wins over `Default` for the errors that have a container.

### View models in Core

`Routeables.Core` provides routing-aware view-model bases so a page that hosts other pages does not rebuild the plumbing:

- `RouterViewModelBase`: back/forward routing through the router's history.
- `MainViewModelBase`: a ready-to-use main view model (see [Main view model per host](#main-view-model-per-host)): you only return the entries.
- `RouteableViewModelBase`: the abstract base under it, with a CommunityToolkit.Mvvm flavour today; manages routing between a set of `IRouteableEntry` items (menus, tab bars), implements `IMainRouteables` and guards the change of the current item.
- `CloseableViewModelBase<TResult>`: a view model that can be closed and returns a result (dialogs).

The CommunityToolkit.Mvvm flavour is opt-in. Declare the feature you want in your project file and `Routeables.Core.targets` adds the packages and the matching source files:

```xml
<PropertyGroup>
  <RouteablesFeatures>;CommunityToolkit;</RouteablesFeatures>
  <!-- ;ReactiveUI; is recognised by the targets but no ReactiveUI sources ship yet -->
</PropertyGroup>
```

### Native AOT and trimming

AOT and trimming are part of the framework's contract: no feature depends on a type that the trimmer could remove.

- Routes are discovered at build time by the source generator (`AddGeneratedRoutes()`), or declared explicitly with `RegisterRoute<TView, TViewModel>()`. Nothing scans assemblies at run time.
- Generic registration methods carry `[DynamicallyAccessedMembers]`, so the constructors and injectable properties of the types you register are kept.
- Property injection (`[Parameter]`, `[Inject]`, `[RouteableParameter]`, ...) uses a description produced by the generator, or by the annotated generic registration. A type that was neither described nor registered with its own type fails with a clear message instead of falling back on reflection.
- JSON route parameters use `JsonTypeInfo`, so they work with a `JsonSerializerContext`.

`Routeables.AotSmoke` is published with `PublishAot` and run in CI, and the Blazor WebAssembly sample is published trimmed; a trim or AOT warning raised by Routeables fails the build.

### Diagnostics reference

The typed-routing generator reports these diagnostics at the attribute or the offending member:

| Id | Severity | Meaning |
| --- | --- | --- |
| `ROUT101` | Error | Missing or invalid caller type (none given, static, or generic) |
| `ROUT102` | Error | Invalid destination (abstract, no public constructor, ambiguous constructors) or invalid `MethodName` |
| `ROUT103` | Error | No route found, or several `[Routeable]` and no explicit `Route` |
| `ROUT104` | Error | Invalid or unsupported template: placeholder without a matching constructor parameter, type mismatch, optional placeholder without a default value, optional path placeholder not last, view-bound placeholder, unsupported type, malformed placeholder |
| `ROUT105` | Error | Unsupported parameter (`ref`, pointer, ref struct) or a name that clashes with a generated one (`caller`, `router`, `cancellationToken`, ...) |
| `ROUT106` | Warning | A parameter that may not receive a value is type-compatible with another one, so constructor arguments could be matched to the wrong parameter |
| `ROUT107` | Error | Two destinations would generate the same method on the same caller |

The registration generator (`AddGeneratedRoutes`) reports:

| Id | Severity | Meaning |
| --- | --- | --- |
| `ROUT201` | Warning | A route with parameters (`/dogs/{name}`) whose view or view model is `Scoped` or `Singleton`: the instance is cached by route and built once, so the later route changes do not pass their own arguments. Use `Transient` |
| `ROUT202` | Warning | A `[Routeable]` view model with no `[RouteableView]` whose name matches several views (`OrderViewModel` and two classes called `OrderView`): none is picked. Put `[RouteableView(typeof(OrderViewModel))]` on the one you mean. |
| `ROUT301` | Warning | `IMainRouteables` registered with `AddTransient` / `TryAddTransient`: each consumer would get another instance than the one the window shows. Use `RegisterMainRouteables<T>(Scoped or Singleton)` |

## Documentation

The guides and the API reference are published at <https://cedric56.github.io/Routeables/>. They are built with [DocFX](https://dotnet.github.io/docfx/)
from the `docs` folder by the `Docs` workflow, on each push to `master` that touches the documentation or the code. To build them locally:

```powershell
dotnet tool update -g docfx
docfx docs/docfx.json --serve
```

Then open <http://localhost:8080>. The `docs/_site` and `docs/api/*.yml` folders are generated: do not commit them.

## Building and testing

```bash
dotnet build Routeables.slnx
dotnet test --project Routeables.Test/Routeables.Test.csproj
```

`dotnet test` runs xunit v3 through Microsoft.Testing.Platform, enabled by `global.json`. On CI, pass `-p:CiBuild=true` so `Routeables.Maui` builds only its plain `net10.0` target.

Performance and memory checks run outside the test suite, in `Release`:

```bash
dotnet run -c Release --project Routeables.Benchmarks -- --filter "*"
dotnet run -c Release --project Routeables.Benchmarks -- --leak
```

[docs/benchmarks.md](docs/benchmarks.md) explains the benchmarks and how to read them, and [docs/host-scenarios.md](docs/host-scenarios.md) lists the scenarios to play by hand on each host (Blazor, Avalonia, MAUI).

## Contributing

Issues and pull requests are welcome. As the project is in preview, please open an issue to discuss larger changes first.

## License

Released under the [MIT License](LICENSE.txt).
