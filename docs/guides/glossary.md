# Glossary

Four ideas are enough to start: a **route**, a **view**, a **region** and the **router** (`IRouter`). The other words below appear when a task needs them.

## The four ideas

| Word | Meaning |
| --- | --- |
| Route | The address of a page, such as `/dogs/Eurohound`. A view model declares its route with `[Routeable("/dogs/{name}")]`. |
| View | What the user sees: a `UserControl`, a `Page`, a Razor component. It is paired with the view model by name (`DogDetailView` goes with `DogDetailViewModel`). |
| Region | A zone of the screen that shows pages. It has its own router and its own history. A window with one zone uses the default router and needs no region of its own. See [Regions](regions.md). |
| Router (`IRouter`) | What a view model asks to go somewhere: `GoToAsync`, `GoBackAsync`, `PopAsync`. It owns the history. A view model never touches a frame or a navigation API of the framework. |

## The other words

| Word | Meaning |
| --- | --- |
| Template | A route with placeholders: `/orders/{id:int}`. The values go to the constructor parameters of the view model that have the same name. See [Routes](routes.md). |
| History | The pages the router has shown, in order, so that Back and Forward have somewhere to go. See [History](history.md). |
| Entry (history) | One page in the history: its view model, its view and its arguments. |
| Entry (menu) | One line of a menu: a `RouteableMenuEntry` with a title, a route and, if needed, a region. A module adds its own with `AddMenuEntries`. |
| Back and Pop | `GoBackAsync` returns to the previous page and keeps the page it left, so `ForwardAsync` can return to it. `PopAsync` returns and forgets the page it left. |
| Guard | A rule that applies to every route change (`IRouteGuard`): the user must be signed in, a feature is off. It allows the change, redirects it or cancels it. See [Back, deep links, guards and diagnostics](system-integration.md). |
| Leaving check | `CanLeaveAsync` on the page that is left. It can refuse, for example to keep unsaved changes. A guard looks at the page that is entered; the leaving check looks at the page that is left. |
| Lifecycle | The calls a page receives: initialize, entering, entered, leaving, left, dispose. See [Lifecycle](lifecycle.md). |
| Arguments | The values given to the page that opens, in an `IRouteableArgs`: the ones for the view model, the ones for the view, and named values. See [Passing arguments](arguments.md). |
| Host | The package for one UI framework (`Routeables.Avalonia`, `Routeables.Wpf`, `Routeables.Blazor`...). It shows the views and brings the dialogs and notifications of that framework. See [Hosts](hosts.md). |
| Module | A feature in its own assembly, added to the application by one extension method. See [Hosts, services and samples](hosts.md). |
| Deep link | An address from outside the application (`xanimals://dogs/Eurohound`) that opens a route. See [Back, deep links, guards and diagnostics](system-integration.md). |
| Segments and coordination | An optional way to write several steps in one string, as Prism does: `Orders?status=open/Detail?id=5`. Each step is a segment, run on its own router. `CoordinateAsync` runs the string. A plain `GoToAsync("/orders/5")` does not read segments. See the [segments README](../../src/Routeables.Segments/README.md). |
| Reshapes | Add-on packages that show the dialogs of Routeables with a UI library: `Reshapes.Blazor.MudBlazor`, `Reshapes.Maui.Community`, `Reshapes.Avalonia.Fluent`. You need one only if you want that library's look. |
| Typed route | A method generated from a constructor, such as `GoToMonkeyDetailAsync(router, name)`, so that a route is written once. See [Typed routing methods](typed-routes.md). |
