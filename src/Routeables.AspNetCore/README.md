# Routeables.AspNetCore

The [ASP.NET Core](https://learn.microsoft.com/aspnet/core) host of Routeables, for **Razor Pages and MVC** (pages rendered on the server, one request at a time; for interactive components use [`Routeables.Blazor`](../Routeables.Blazor/README.md)). It gives the view models their router, history, dialogs and notifications on the server. Targets **.NET 10**.

```bash
dotnet add package Routeables.AspNetCore
```

The package brings `Routeables` (routing, abstractions and the source generator): there is nothing else to add.

## Set up

```csharp
builder.Services.AddRazorPages();
builder.Services.AddRouteablesAspNetCore();   // or AddRouteablesAspNetCore<MainViewModel>() for an application with a menu
```

The routes the generator finds are registered for you at startup. The optional block is for your own services: `AddRouteablesAspNetCore(r => r.AddSingleton<IClock, Clock>())`.

Everything is **scoped**. A request that has no scope of its own to share would start from an empty history every time, so an application keeps one service scope per visitor (a cookie holds its identifier) and runs each request in it, one at a time. The Razor sample does it with `VisitorSessions` and `UseVisitorSessions()` ([`samples/Razor`](../../samples/Razor)); copy it as a starting point, or use your own session store, as long as the routers and the view models of a visitor live in the same scope from one request to the next. **`VisitorSessions` is a sample, not a production store**: it keeps at most 1 000 sessions in memory and drops the oldest when a new one comes, so a client that sends requests without its cookie can push the others out; a real application creates the session after a first interaction (or a sign-in), limits the rate per client and keeps the sessions in a store of its own.

## What the host registers

| Service | What it is for |
| --- | --- |
| `IRouteableDialogService`, `IRouteableNotificationService` | A view model awaits a dialog or a notification as on a desktop; the answer comes with a later request. `RouteableInteractions` holds the dialogs and the toasts that wait to be drawn (`Pending`, `Toasts`) and runs an action until it ends or asks something (`StartAsync`, then `AnswerAsync`) **System notifications** (`CreateNotificationAsync`) are those of the browser: the server queues them (`ShowAsync`), the page rendered next hands them to the browser with `TakeSystemNotificationsJson()` (the sample draws them in `_Interactions.cshtml`), and the visitor must allow them. The page tells the server what he does with one: it sends a request when the notification is clicked or closed, the application passes it to `RouteableInteractions.CompleteSystemNotification(id, activated)`, and `NotificationCompleted` is raised (`IsActivated` for a click, `IsCancelled` for a close). The report comes from the page that showed the notification: once the visitor goes to another page the browser no longer tells it, and a notification can only be closed by the page that showed it. |
| `IRouteableRequestedThemeService` | The theme the visitor asked for (`RouteableRequestThemeService`) |
| `RouteablePageState` | The address that was last served: a reload shows the state as it is, another address navigates |
| `RouteableTransitions` | The CSS classes and the duration for the view of a region (see below) |

## A page

A view is a Razor view under `Views/` that names its view model and its route, as in the sample (`Views/DogDetailView.cshtml`):

```cshtml
@attribute [RouteableView(typeof(DogDetailViewModel), "/dogs/{name}")]
@model DogDetailViewModel
```

Its compiled class is named after its path (`Views_DogDetailView`). The page model of the sample (`MainView.cshtml.cs`) takes the address of the request to the router, and the `_Region.cshtml` partial draws `router.CurrentView` with `Html.PartialAsync`, giving it the view model as its model. Copy those two files as a starting point.

## The menu

`<routeable-menu />` (`@addTagHelper *, Routeables.AspNetCore` once, in `_ViewImports.cshtml`) draws a `nav` with a link per entry of the menu view model, marks the entry of the current address and shows a locked entry without a link. `entries` gives another list, `current` another address, and a `class` goes to the `nav`; the classes of the links are `rt-menu-link`, `rt-menu-active`, `rt-menu-locked`, `rt-menu-icon`, `rt-menu-text`, `rt-menu-name` and `rt-menu-description`.

## Transitions

A page is a whole new document for each request, so the browser plays a CSS animation when the view of a region arrives. `RouteableTransitions.Next(region, regionTransition)` gives the classes (`rt-enter rt-{name} rt-{forward|back|none}`) and `--rt-duration`; the animations of the built-in names are in `transitions.css`, a static web asset of the package: `<link rel="stylesheet" href="~/_content/Routeables.AspNetCore/transitions.css" />` (it needs `app.UseStaticFiles()`; in production run the published application, or call `builder.WebHost.UseStaticWebAssets()`), and `prefers-reduced-motion` turns them off. The first page has none and a reload plays none. See [transitions](../../docs/guides/transitions.md).

## Good to know

- Nothing is shared between visitors, and a request is not run at the same time as another one of the same visitor: the routers and the view models are not made for it.
- There is no push: a view model that changes by itself (a timer, a `Notify` of another request) shows when the visitor asks for the page again.
- Routes found by the generator are in each project that references it; a library with views wraps `AddGeneratedRoutes()` in a public method.

## Next

- [Setting up a host](../../docs/getting-started.md#2-set-up-your-host)
- [Razor Pages sample](../../samples/Razor)
- [Transitions](../../docs/guides/transitions.md)
