# Routeables for Blazor developers

What changes when the pages of a Blazor application are routed by Routeables instead of by `@page`, and what stays as it was. Everything here is in the repository: the samples `Samples/XAnimals.Razor` (Blazor Server), `XAnimals.BlazorWebAssembly` and `XAnimals.MauiHybrid`.

## The idea in one sentence

With `@page`, the address chooses a component. With Routeables, a **view model** has a route, the router opens the view paired with it, and the browser address follows the router. Navigation is a call on `IRouter` from the view model, with a history, guards and a lifecycle that do not depend on Blazor.

## Side by side

| | Blazor router | Routeables |
| --- | --- | --- |
| Declare a page | `@page "/dogs/{Name}"` on the component | `[Routeable("/dogs/{name}")]` on the view model; the view is the component named after it (`DogDetailView`) |
| Parameters | `[Parameter]` and `[SupplyParameterFromQuery]` | The constructor of the view model (or an injected property): `{id:int}` in the path, `?{tab}` and `?{tab?}` in the query |
| Navigate from code | `NavigationManager.NavigateTo("/dogs/Rex")` in the component | `await router.GoToAsync("/dogs/Rex")` from the view model, or the typed method the generator writes |
| Navigate from markup | `<a href>`, `NavLink` | The same: the address follows the route, so `<a href="/dogs/Rex">` and `NavLink` work |
| Refuse to leave | `NavigationLock` in the component | `CanLeaveAsync` on the view model or the view |
| Rule for a route | `AuthorizeRouteView` and `[Authorize]` | An `IRouteGuard`, written once for every route |
| Back | The browser history | The router's own history (`GoBackAsync`, `ForwardAsync`, `PopAsync`) and the browser's Back button |
| Result of a navigation | none | `RouteableResult`: succeeded, cancelled (a guard or a page refused) or failed, with the exception |
| Several independent zones | none | Regions: one router, one history and one lock per zone |

## How it plugs into Blazor

Routeables does not replace `Router` in `Routes.razor`: the sample keeps it.

```razor
@* Samples/XAnimals.Razor/Components/Routes.razor *@
<Router AppAssembly="typeof(AboutView).Assembly"
        AdditionalAssemblies="new[] { typeof(MainLayoutBase).Assembly }"
        NotFoundPage="typeof(NotFound)">
    <Found Context="routeData">
        <RouteView RouteData="routeData" DefaultLayout="typeof(MainLayout)" />
    </Found>
</Router>
```

- `Routeables.Razor` ships `Main.razor`, whose two `@page` routes (`/` and `/{*path}`) give the Blazor router a page for every address. It draws nothing.
- What the user sees is drawn by the layout (`MainLayout`, or your own inheriting `MainLayoutBase`) from the **current view of the Routeables router**.
- `RouteableUrlSync` keeps the address and the router in step: a route change updates the address (a push adds a browser history entry, the other moves replace it); a typed address, a link or the browser's Back and Forward route the router; if a guard or a page refuses, the address goes back to the current page.
- A page registered without an explicit route has no address of its own: the address goes back to `/`.

Everything is scoped: with Blazor Server, each circuit has its own router and history.

## Write a page

A view model with its route, and the view paired with it:

```csharp
[Routeable("/dogs/{name}")]
public class DogDetailViewModel(IRouter router, string name, Action? back = null)
    : AnimalDetailViewModel(router, DogData.Dogs.FirstOrDefault(d => d.Name == name), back);
```

```razor
@* DogDetailView.razor *@
@inherits RouteableFor<DogDetailView, DogDetailViewModel>

<AnimalDetail ViewModel="ViewModel" />
```

`ViewModel` is typed and the lifecycle hooks come with the base class. Register the routes at startup with `services.UseRouteablesServer<MainViewModel>(r => r.AddGeneratedRoutes())` (or `UseRouteablesWebAssembly`, `UseRouteablesHybrid`).

> [!IMPORTANT]
> The generator cannot read the `.razor` files of its own project. A Razor view is found when `[RouteableView(...)]` is on a C# `partial class` of the same name (`DogDetailView.razor.cs`), or when the view is in a **referenced** project and `AddGeneratedRoutes()` is called from the application project. An `@attribute [RouteableView(...)]` inside the Razor file, in the project that calls `AddGeneratedRoutes()`, registers nothing. See [getting started](../getting-started.md).

Do not also give the component an `@page`: a page of your own with an `@page` is rendered by Blazor, outside the router, and neither its history nor its guards apply to it.

## Navigate

From a view model:

```csharp
RouteableResult result = await router.GoToAsync($"/dogs/{Uri.EscapeDataString(name)}");
if (result.Cancelled) return;      // a guard or the current page refused
if (!result.Success) throw new InvalidOperationException(result.Exception?.Message);
```

The typed methods of the generator spare you the route string ([typed routing methods](typed-routes.md)). `IRouter` also has `GoBackAsync`, `ForwardAsync`, `PopAsync`, `GoBackToRootAsync`, `MoveToAsync` and `EraseHistoryAsync`; the difference between going back and popping is in [History](history.md).

From markup, `<a href="/dogs/Rex">` works, and so does a `NavLink` in a layout of your own, because the address follows the route.

## Parameters

| Template | Meaning |
| --- | --- |
| `{name}` | Required segment, given to the constructor of the view model |
| `{id:int}` | Typed segment (the built-in types and the ones you register with `RouteParser.RegisterSerializer`) |
| `{tab?}` | Optional segment, for the last segment of the path |
| `?{tab}` / `?{tab?}` | Query-string value, required / optional |

A query value that the template does not declare is kept in `IRouteableArgs.Values` for the lifecycle hooks, and is never given to a constructor. Details: [Routes and templates](routes.md).

## Guards, lifecycle and sign-in

- **Leaving a page:** `CanLeaveAsync` returns `false` and the route change is cancelled. The browser address goes back to the page that stays.
- **Entering a page:** an `IRouteGuard` is asked before every route change. It allows, cancels or redirects:

```csharp
public sealed class SignedInGuard(ISession session) : IRouteGuard
{
    public Task<RouteGuardDecision> CanEnterAsync(RouteableRoutingEventArgs change, CancellationToken ct)
        => Task.FromResult(change.Route is "/account" && !session.IsSignedIn
            ? RouteGuardDecision.Redirect("/sign-in")
            : RouteGuardDecision.Allow);
}
```

`RouteableReturn` takes the user back to the page they asked for once they are signed in. Everything about guards: [Back, deep links, guards and diagnostics](system-integration.md).

**Authorization is not integrated with Blazor.** There is no `AuthorizeRouteView` equivalent and no use of `[Authorize]`: a rule about who may open a route is an `IRouteGuard` that you write with your own service. Routeables does not read the authentication state of Blazor for you, and no sample or test of this repository covers it. A guard is resolved once per router and lives as long as it does, so a guard that needs a scoped service must be scoped itself.

## What to know before choosing it

- **Routes are on view models,** not on components. A team that likes seeing the address next to the markup will miss `@page`.
- **There is no catch-all placeholder** (`{**path}`) in a Routeables template. `Main.razor` has the one that Blazor needs.
- **A component must be safe to dispose twice:** the router disposes the pages that leave its history, and Blazor disposes what it renders.
- **A `Transient` `IDisposable` service** injected into a page lives until the circuit ends: register it `Scoped` or `Singleton` ([lifecycle](lifecycle.md)).
- **Render modes:** the samples run in interactive Server, WebAssembly and MAUI Hybrid. Prerendering and the other render modes of .NET 8 and later are not covered by the tests of this repository.
- **Targets:** `Routeables.Razor` is `net10.0`.

## When to keep the Blazor router

Keep `@page` for an application with a few pages and no need for a history, guards or regions of its own. Routeables is worth its setup when navigation has rules (pages that refuse to be left, sign-in, a history), when several zones navigate independently, or when the same view models are shown by another host (MAUI, Avalonia, Uno, WPF).
