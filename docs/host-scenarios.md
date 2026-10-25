# Host scenarios

The router is shared; what draws the page, handles the platform's back button and keeps the window alive is not. These scenarios are played on
each host to check that the common contract behaves the same. A scenario passes when every column marked "yes" does what **Expected** says.

Hosts: **Blazor** (Server, WebAssembly, and Blazor Hybrid in MAUI), **Avalonia** (Desktop, Android, iOS, Browser), **MAUI** (Android, iOS, Mac Catalyst, Windows).
Use the XAnimals sample of each host. Scenarios marked *auto* are covered by `Routeables.Test` on the shared core only; the host part is still played by hand.

Record each run as: host, platform, version, scenario id, pass or fail, and what was seen.

## A. Routing

| Id | Scenario | Expected | Blazor | Avalonia | MAUI |
|---|---|---|---|---|---|
| A1 | Start the app. | The first route is shown once, no flash of another page. | yes | yes | yes |
| A2 | Open a page by its address (`GoToAsync("/animals")`), then by a generated method. | The same page appears; an unknown address fails without a crash and without leaving the current page. | yes | yes | yes |
| A3 | Open the same page twice in a row. | No second entry in the history; no flicker. | yes | yes | yes |
| A4 | Open 25 pages in a row. | The history holds 20; the oldest pages are disposed; memory is steady. | yes | yes | yes |
| A5 | Tap two links quickly (double click). | One route change runs after the other; the final page is the last one asked; no exception. | yes | yes | yes |

## B. History

| Id | Scenario | Expected | Blazor | Avalonia | MAUI |
|---|---|---|---|---|---|
| B1 | Back, then Forward. | The same page objects come back (state kept); the buttons' enabled state follows `CanGoBack` and `CanGoForward`. | yes | yes | yes |
| B2 | Open a detail, then `PopAsync`. | The detail is gone for good: Forward is disabled and the detail is disposed. | yes | yes | yes |
| B3 | Back twice, then open a new page. | The forward entries are dropped. | yes | yes | yes |
| B4 | `MoveToAsync(0)`, then `GoBackToRootAsync`. | The cursor moves; the entries after the root are discarded by `GoBackToRootAsync` only. | yes | yes | yes |
| B5 | The platform's own back (Android back button, iOS swipe, browser Back, Windows back). | It does what `GoBackAsync` does, and never leaves the history out of step with what is on screen. | browser Back | Android, Browser | Android, iOS, Windows |
| B6 | Reload or restart on a deep page. | Blazor: the address reopens the page (`RouteableUrlSync`). Avalonia and MAUI: the app starts at its first route; no crash. | yes | yes | yes |

## C. Lifecycle

| Id | Scenario | Expected | Blazor | Avalonia | MAUI |
|---|---|---|---|---|---|
| C1 | Leave a form with unsaved changes (`CanLeaveAsync` returns false). | The route change is refused, the page stays, the entry state is intact. *auto* (core) | yes | yes | yes |
| C2 | A page throws in `OnEnteringAsync`. | The previous page is restored and shown again; the error is reported once. *auto* (core) | yes | yes | yes |
| C3 | A page is slow to initialise (2 s). | The previous page stays usable or a wait state is shown; a second route change made meanwhile does not corrupt the history. | yes | yes | yes |
| C4 | Close the window or the app on a page. | Pages are disposed once; no exception on shutdown. | yes | yes | yes |
| C5 | A `Transient` disposable service in a page. | It is released with the page, not only when the container is disposed (the router disposes the page, but a container keeps `Transient` disposables it created until it is disposed itself: check what the host does). | yes | yes | yes |

## D. Parameters and results

| Id | Scenario | Expected | Blazor | Avalonia | MAUI |
|---|---|---|---|---|---|
| D1 | Open `/bears/Brown%20Bear` and a typed `GoToRouteAsync<TViewModel, TData>`. | The view model receives the bear (`AnimalSelection`), typed, before it is shown. *auto* (core) | yes | yes | yes |
| D2 | Open with a query string (`?name=Tom`). | The property or constructor argument is filled; a missing one keeps its default. | yes | yes | yes |
| D3 | Open a picker with `GoToForResultAsync`, choose a value, `PopAsync`. | The caller resumes with the value; leaving without choosing returns `Returned = false`. *auto* (core) | yes | yes | yes |
| D4 | Open a picker and cancel the awaiting call. | The call ends with `Cancelled` and the application stays usable. | yes | yes | yes |
| D5 | Dialogs and message boxes from a view model. | The result comes back to the awaiting code; a second dialog waits for the first. | yes | yes | yes |

## E. Regions and composition

| Id | Scenario | Expected | Blazor | Avalonia | MAUI |
|---|---|---|---|---|---|
| E1 | Two regions (menu and content): route in each. | Each region has its own history; back in one does not move the other. | yes | yes | yes |
| E2 | Route in all regions at once. | All show their page; no region waits for another. | yes | yes | yes |
| E3 | A region is removed from the screen (tab closed, component disposed), then routed. | No exception; no page rendered into a view that is gone; it can be shown again. | yes | yes | yes |
| E4 | A main entry changes while a detail region shows a page. | The detail follows the declared layout (followers) or stays, as configured. | yes | yes | yes |
| E5 | Open a window or a second shell (where the platform has one). | Each window has its own region routers; closing one does not dispose the other's pages. | n/a | Desktop | Windows, Mac Catalyst |

## F. Lock

| Id | Scenario | Expected | Blazor | Avalonia | MAUI |
|---|---|---|---|---|---|
| F1 | A page blocks leaving (lock). | Every other main entry is disabled and cannot be chosen, by tap, key or the platform's back. | yes | yes | yes |
| F2 | The page allows leaving again. | The entries are enabled again at once. | yes | yes | yes |
| F3 | Lock while another main entry is being shown, then try to select the others. | The selection returns to the locked entry; its page is not recreated. | yes | yes | yes (Shell, all platforms) |

## G. Platform behaviour

| Id | Scenario | Expected | Blazor | Avalonia | MAUI |
|---|---|---|---|---|---|
| G1 | Rotate the device or resize the window. | The current page, its state and the history are kept. | yes | yes | yes |
| G2 | Send the app to the background and bring it back (mobile). | Same page, same history. | Hybrid | Android, iOS | Android, iOS |
| G3 | Let the OS kill the app in the background, then reopen it (mobile). | The app starts at its first route without a crash; nothing is claimed about restoring the history. | Hybrid | Android, iOS | Android, iOS |
| G4 | Theme or language change at run time. | Pages already open follow it without being recreated. | yes | yes | yes |
| G5 | Screen reader and keyboard only. | Every route change entry is reachable and announced; focus lands on the new page. | yes | yes | yes |

## H. Measure on the host

The router benchmarks do not include drawing. On each host, with the XAnimals sample, record:

- time from the call to the new page's first frame (stopwatch around `GoToAsync` plus the first layout pass);
- memory after 100 route changes between two pages, and after a forced garbage collection (it must return near the start);
- peak memory with 16 regions on screen (Blazor and Avalonia) or 5 tabs (MAUI).

Use `dotnet-counters` or `dotnet-gcdump` on Avalonia desktop and Blazor Server, Android Studio's profiler on Android, Instruments on iOS and Mac Catalyst,
and the browser's performance panel for WebAssembly.
