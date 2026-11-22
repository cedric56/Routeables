# What Routeables offers

Nothing here is needed for a first page: start with [Getting started](getting-started.md), then come back when a task needs one of these. For a task, the [recipes](guides/recipes.md) are shorter than the guides; for a word you do not know, the [glossary](guides/glossary.md).

| Area | What Routeables offers | Guide |
| --- | --- | --- |
| Routes | `[Routeable("/path/{id:int}")]` on the view model; view paired by name; constraints; most specific template wins; query strings | [Routes](guides/routes.md) |
| Declarative navigation | `routeables:RouteableLink.Route="/hello/World"` on a button, in XAML, no code in the view model (Avalonia, WPF, Uno, MAUI) | [Routes](guides/routes.md) |
| Typed routing | `GoToMonkeyDetailAsync(router, name)` generated from the constructor | [Typed routes](guides/typed-routes.md) |
| Arguments | Route values, query values and objects passed to the target | [Arguments](guides/arguments.md) |
| History | Back, forward, pop, back to root, move to an entry, erase; the last 50 entries kept by default, `LimitHistory` to change it, `KeepWholeHistory` to keep them all | [History](guides/history.md) |
| Regions | Several zones, each with its own router and history | [Regions](guides/regions.md) |
| Lifecycle | Initialize, entering, leaving, `CanLeaveAsync` veto, `OnSubscribe`/`OnUnsubscribe`, destroy | [Lifecycle](guides/lifecycle.md) |
| Guards | `IRouteGuard` with an order; allow, redirect or cancel a route change | [Routes](guides/routes.md) |
| Transitions | 15 built-in transitions, custom ones registered per host | [Transitions](guides/transitions.md) |
| Dialogs and notifications | One dialog service and one notification service on every host, replaceable at startup, system notifications (Avalonia, Blazor, MAUI) | [System integration](guides/system-integration.md) |
| Deep links | Open a route from a URL or a system activation | [System integration](guides/system-integration.md) |
| Modules | A feature is one extension method, loaded at startup or on demand | [Hosts](guides/hosts.md) |
| Locking | Pages that block leaving, a locked menu | [Locking](../src/Routeables.Locking/README.md) |
| Menu | `RouteableMenu` on every host (a `<routeable-menu />` tag helper on ASP.NET Core), items bound to routes | [Hosts](guides/hosts.md) |
| Errors | `RouteableResult`, `IRouteableErrorSink` | [Lifecycle](guides/lifecycle.md) |
| Diagnostics | Tracing and metrics for route changes | [Diagnostics](guides/diagnostics.md) |
| Native AOT | Source generated; nothing scans assemblies at run time | [AOT](guides/aot.md) |
| Hosts | Blazor, ASP.NET Core, .NET MAUI, Avalonia, Uno Platform, WinUI 3, WPF | [Hosts](guides/hosts.md) |
| Dialog libraries | MudBlazor, CommunityToolkit.Maui, FluentAvalonia | [Reshapes](../reshapes/README.md) |

Routeables is the navigation layer, dialogs and notifications included. It ships no commands, view-model locator or event aggregator: use CommunityToolkit.Mvvm, ReactiveUI or plain C#.
