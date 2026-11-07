# Routeables for Blazor developers

What changes when the pages of a Blazor application are routed by Routeables instead of by `@page`, and what stays as it was. Everything here is in the repository: the samples `samples/XAnimals.Razor` (Blazor Server), `XAnimals.BlazorWebAssembly` and `XAnimals.MauiHybrid`.

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
@* samples/XAnimals.Razor/Components/Routes.razor *@
<Router AppAssembly="typeof(AboutView).Assembly"
        AdditionalAssemblies="new[] { typeof(MainLayoutBase).Assembly }"
        NotFoundPage="typeof(NotFound)">
    <Found Context="routeData">
        <RouteView RouteData="routeData" DefaultLayout="typeof(MainLayout)" />
    </Found>
</Router>
```

- `Routeables.Blazor` ships `Main.razor`, whose two `@page` routes (`/` and `/{*path}`) give the Blazor router a page for every address. It draws nothing.
- What the user sees is drawn by the layout (`MainLayout`, or your own inheriting `MainLayoutBase`) from the **current view of the Routeables router**.
- `RouteableUrlSync` keeps the address and the router in step: a route change updates the address (a push adds a browser history entry, the other moves replace it); a typed address, a link or the browser's Back and Forward route the router; if a guard or a page refuses, the address stays on the current page. The router is asked before the address changes (a location changing handler), so a refused Back of the browser is cancelled and the browser history keeps its entries. One limit remains: `GoBackAsync` called from a view model replaces the address instead of going back in the browser history, so the browser's Back does not follow it.
- `RouteableLink` (`Routeables.Blazor.Components`) is the anchor that goes to a route: `<RouteableLink Route="/dogs/12">Rex</RouteableLink>`. A plain click is routed by the router (guards, history); a click with Ctrl, Shift, Alt or Meta, a `target`, or a `download` is left to the browser. It renders a real `<a href>`, so it works before the circuit starts, and it adds the `active` class on the page that is shown.
- **GoBack from a view model:** when the router goes back (or forward) by itself, the browser does the same move in its history (`history.go`), as long as it holds that entry; otherwise the address is replaced. The Back button of the browser and the one of the application stay in step.
- **History across prerender and reconnection:** with `RouteablePrerenderOptions` registered (`PersistHistory`, on by default), the history and the state of the pages (`IRouteableState`, `WithValue`) are saved with `PersistentComponentState` and restored by the circuit or WebAssembly runtime that takes over. Without prerendering, register `services.AddSingleton(new RouteableHistoryOptions())` for the same hand-over (a paused circuit, a persisted state).
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

## One instance of the view

The router creates the view, runs `InitializeAsync` and `OnEnteringAsync` on it and keeps it in its history. `RouteableComponentActivator`, which `UseRouteablesServer`, `UseRouteablesWebAssembly` and `UseRouteablesHybrid` register as the `IComponentActivator` of the application, gives that same instance to Blazor the first time it is rendered. The hooks written in the view (`CanLeaveAsync` that reads a field of a form, say) therefore run on the component that is on screen, and only one component is built.

A component can be attached to a renderer only once. When a page comes back from the history (Back, Forward), Blazor builds another instance of the component and the router keeps calling the hooks of the first one. For that reason:

- keep the state that a hook reads in the **view model**, which is the same whichever instance is displayed;
- do not rely on a field of the view in `CanLeaveAsync` for a page the user can reach again with Back.

If the application registers its own `IComponentActivator` before Routeables, Routeables keeps it and the views are built by Blazor as before: the router's instance is then never the displayed one. Call it from your activator, or register it after `UseRouteables...` with a `RemoveAll`, if you need both.

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

**Authorization.** `services.AddRouteableAuthorization(o => o.SignInRoute = "/sign-in")` makes `[Authorize]` and `[AllowAnonymous]` count on the view models (and views) of Routeables, with the `Roles` and `Policy` of `[Authorize]`. The guard it registers (`RouteableAuthorizationGuard`) reads the `AuthenticationStateProvider` of the application and the authorization policies, and asks before any other guard (`Order` is `-100`).

```csharp
services.AddRouteableAuthorization(o =>
{
    o.SignInRoute = "/sign-in";        // a visitor who is not signed in; without it the route change is cancelled
    o.ForbiddenRoute = "/forbidden";   // a signed-in visitor who is refused (role or policy); without it the route change is cancelled
});

[Routeable("/account"), Authorize]
public sealed class AccountViewModel : IRouteable { }

[Routeable("/sign-in"), AllowAnonymous]
public sealed class SignInViewModel : IRouteable { }
```

The application registers its own authentication (an `AuthenticationStateProvider`, logging) as any Blazor application with sign-in does; Routeables does not choose it. Only a new page is redirected; Back and Forward are cancelled. A sign-in page that forgot `[AllowAnonymous]` is cancelled instead of being redirected to itself. For a rule that is not about the signed-in user, write an `IRouteGuard` as above. A guard is resolved once per router, so a guard that needs a scoped service must be scoped itself.

## What to know before choosing it

- **Routes are on view models,** not on components. A team that likes seeing the address next to the markup will miss `@page`.
- **Catch-all:** a template may end with `{**path}` (`/files/{**path}` matches `/files/a/b/c`, `path` = `a/b/c`; the segments keep their slashes). It is the least specific of the templates that match. `Main.razor` has its own for Blazor.
- **A component must be safe to dispose twice:** the router disposes the pages that leave its history, and Blazor disposes what it renders (see [One instance of the view](#one-instance-of-the-view)).
- **A `Transient` `IDisposable` service** injected into a page lives until the circuit ends: register it `Scoped` or `Singleton` ([lifecycle](lifecycle.md)).
- **Prerendering:** with a server that prerenders (`InteractiveServerRenderMode(prerender: true)`), register `services.AddSingleton(new RouteablePrerenderOptions())` and the layout routes to the address while the page is prerendered, so the HTML already holds the page. Without that registration nothing changes: the layout waits for the circuit. The circuit that takes over starts again from the address, and the views are built a second time (keep the loading in `InitializeAsync`, not in a static). The samples run with `prerender: false`, and this path is covered by unit tests only, not by a browser test. Static server rendering of a page without a circuit, streaming rendering and the other render modes of .NET 8 and later are not covered.
- **404:** with prerendering on, `new RouteablePrerenderOptions { UnknownAddressIsNotFound = true }` makes an address that matches no route (other than `/`) answer with the status 404 and the not-found page of the application's `Router` (`NavigationManager.NotFound()`), instead of the default page. Without prerendering the response is always 200: the server does not know the routes before the circuit runs.
- **Targets:** `Routeables.Blazor` is `net10.0`.

## When to keep the Blazor router

Keep `@page` for an application with a few pages and no need for a history, guards or regions of its own. Routeables is worth its setup when navigation has rules (pages that refuse to be left, sign-in, a history), when several zones navigate independently, or when the same view models are shown by another host (MAUI, Avalonia, Uno, WPF).
