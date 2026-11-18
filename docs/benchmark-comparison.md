# Comparing navigation libraries on the same application

[Benchmarks](benchmarks.md) measures the router of Routeables alone. It does not say how Routeables compares with Prism, ReactiveUI, MAUI Shell, MvvmCross or a navigation service of your own: that needs the **same application** written once for each of them, and the same measures taken on each. This page is the protocol for that comparison. **Phase 1 is written (see below); phases 2 and 3 are not, and there are no results below**: a table of numbers that nobody measured on a machine that is named would be worse than none.

## The application

A small business application, large enough to use what a navigation library is for:

| Part | What it has |
|------|-------------|
| Pages | 50 view models, each with a view; 5 of them have a constructor dependency (a service of the container) and 10 take a route parameter (`/customers/{id}`) |
| Menu | 8 entries; selecting one shows its page, and the entry of the page that is shown is the one selected |
| Tabs | 3 tabs that keep their own history (a region, a tab host, or whatever the library has) |
| Chain | list, detail, edit: three pages deep, with Back from each |
| Guard | 5 pages that need a signed-in user; without one the user is sent to the sign-in page, then to the page he asked for |
| Leaving | the edit page refuses to be left while it has unsaved changes |
| Dialogs | a message box, a confirmation, and a modal page that returns a value |
| Deep link | an address given at start opens a page two levels deep, with Back leading to its parent |
| History | 20 pages deep, with Back and Forward |

The view models hold the same logic in every implementation. They only differ where the library obliges them to (a base class, an interface, an attribute). Pages have no content to speak of: the point is the navigation, not the drawing.

## The scenario that is measured

One script of 200 steps, the same for every implementation: open each of the 50 pages from the menu, go down the chain and back, switch tabs, answer the dialogs, hit the guard with and without a user, follow the deep link, go back 20 pages, forward 20. Each step has an expected page; an implementation that ends on another page has failed the run, whatever its times.

## What is measured

| Measure | How | Where |
|---------|-----|-------|
| Time of one route change, and bytes allocated | BenchmarkDotNet, `MemoryDiagnoser`, the script replayed | Headless where the library allows it (Routeables, ReactiveUI `RoutingState`, a navigation service of your own); on the device or emulator for the others, and said so |
| Start: process start to the first page shown | the timestamp of the first frame, 20 runs, median | On the target UI framework |
| Memory after 1 000 route changes | managed heap after two forced collections, and pages still alive (weak references) | Headless and on the target |
| Published size | the same application, trimmed, and with Native AOT when the library supports it | Blazor WebAssembly, MAUI Android, a console or Avalonia desktop head |
| Trimming and AOT warnings | the number of IL2xxx and IL3xxx warnings of the publish | Same as above |
| Code to write | lines of the files that hold navigation (routes, view-to-view-model mapping, registration, guards), counted with the same tool | The repository |
| Concepts to learn | types and attributes the 50 pages need, counted by a script, not by opinion | The repository |
| A wrong route | does a mistyped route or parameter fail at build time, at start, or only when the user gets there? Four mistakes tried on each | By hand, written down |

## The rules that keep it fair

- **Same machine, same SDK, same container.** `Microsoft.Extensions.DependencyInjection` in every implementation where the library accepts it; the one it requires otherwise, said so.
- **Idiomatic use, by the book.** Each implementation follows the documentation of its library, and is read by someone who uses that library every day before it is measured. A library measured the way nobody uses it proves nothing.
- **Versions pinned** in the repository (`Directory.Packages.props`), and printed with the results.
- **Both ends reported.** Where Routeables is slower or larger, the table says so, and why.
- **Raw data published**, with the code, the commands and the machine, so anyone can repeat the run and say that it is wrong.
- **No total score.** The measures are different things; a single mark would hide which one a reader cares about.

## Phases

1. **Headless core (written).** The 200-step script on the navigation of Routeables, ReactiveUI routing, and a navigation service of your own, all on the .NET console, with the memory checks. This part needs no UI framework and is the cheapest to make correct.
2. **One UI framework.** The same application on Avalonia or WPF, for Routeables, Prism and ReactiveUI: start, size, trimming, code to write.
3. **Mobile and Blazor.** MAUI (Routeables, Shell, Prism) and Blazor (Routeables, the Blazor router): published size and start on the device.

### Phase 1: what is in the repository

`benchmarks/Routeables.Comparison` (Routeables and a navigation service of your own) and `benchmarks/Routeables.Comparison.ReactiveUI` (the same, with ReactiveUI routing) run **the same script of 200 steps** through one interface (`INavigationDriver`: go, back, forward, the state of the application). The expected page of each step comes from a small model of history written once (`Scenario.cs`), not from any of the libraries.

```
dotnet run --project benchmarks/Routeables.Comparison -c Release                       # Routeables and the service of your own
dotnet run --project benchmarks/Routeables.Comparison.ReactiveUI -c Release            # the three; options: --check, --measure, --memory
```

- **`--check`**: every step must end on the page the model expects; the exit code says so. This is what the CI job "Same application, three libraries" gates on.
- **`--measure`**: 5 warm-up runs, then 30 runs of the script, each on a new driver; the median time and the bytes allocated (`GC.GetTotalAllocatedBytes`) of a run. A plain harness, not BenchmarkDotNet: the three libraries run in one process on a script that lasts milliseconds, and the harness is the same for all of them. BenchmarkDotNet comes back in the phases that measure on a device.
- **`--memory`**: 1 000 route changes, the managed heap after two forced collections, and how many of the first 100 pages are still alive, before and after the history is cleared.

What the script covers: 40 menu pages (5 with a constructor dependency, 5 behind a guard), 10 pages with a route parameter, the chain list / detail / edit with Back, the edit page that refuses to be left, the guard (turned away, signs in, goes on to the page asked for), a history 20 pages deep with Back and Forward. **What it does not cover yet**: the menu as a control, tabs, dialogs and the deep link at start: they need a UI framework, so they wait for phase 2.

How each library is written, and where it differs, is part of the result:

| | Routeables | ReactiveUI routing | Service of your own |
|---|---|---|---|
| Pages | one view and one view model per route, registered with a route (`/p07`, `/c3/{id:int}`); the parameter reaches the constructor | the application creates the view model and gives it to `Navigate` | a dictionary from a route to a factory |
| Guard | an `IRouteGuard` that redirects | written in the application, before `Navigate` | written in the service |
| Refuse to leave | `CanLeaveAsync` of the page | written in the application, before `Navigate` | written in the service |
| Back / Forward | `GoBackAsync`, `ForwardAsync` | `NavigateBack`; **no Forward** (a stack, not a history): those 20 steps are skipped and the run says so | a list and an index |
| Container | `Microsoft.Extensions.DependencyInjection` | the application's own (a plain object here, the dependency is a field) | `Microsoft.Extensions.DependencyInjection` |

**The ReactiveUI version was written without being able to restore ReactiveUI.** It has since compiled and run in the CI. A reviewer who had only that file read it (not a daily user of the library) and found it honest on the functions: no Forward (`SupportsForward` is `false`), the guard and the refusal to leave written in front of `Navigate`. What the review changed, and what it did not:

- **The dependency** of the five pages that have one was read from a field, while Routeables resolves it through the container: the ReactiveUI driver now asks the same container (`Microsoft.Extensions.DependencyInjection`, a singleton) each time. The second run below was made after this change: the allocations did not move.
- **The pattern of the pages with a parameter** (`['C', ..]`) took any name that starts with C: it is now restricted to `C` and a digit, with a parameter.
- **The pages** are now built the way Routeables builds them: a view (transient) and a view model (the activator of the container, with the name and the screen as arguments) for every page, the view kept as long as the page is on the stack. Before, ReactiveUI built a view model with `new` and no view. The activator of the container is not the compiled factory Routeables may use: a difference that stays, and that favours Routeables a little. The numbers of the first two runs were measured **before** this change and are to be replaced by the next run.
- **Not changed, and stated:** Routeables calls the hooks of the page and disposes it when it leaves the history, ReactiveUI has no lifecycle (the pages are still collected: none is alive after the history is cleared). The router of Routeables runs without a scheduler or a `Task.Yield`, as the immediate scheduler does here.
- **Checked and already the same:** Back does not run the guards again in either (the guard of Routeables looks at pushes only); the return to the page that was asked for after signing in is done by the scenario for every driver (`Continue`), not by a library; the name of the page shown is compared with the model at every step.
- **Not exercised by the scenario:** the order of the checks when a dirty edit page is left for a guarded page (leaving first, in the model). Both drivers do it in that order, but no step proves it.

The ReactiveUI step stays tolerated until someone who uses the library daily has read the driver. The runs were made on a shared CI runner, nothing was measured on a machine fit to be named. The CI job writes the three tables in the summary of each run; copy them below with the date, the versions and the runner when a run is clean.

Each phase ends with its results published in this page, with the date, the versions and the machine. Phases 2 and 3 need the packages of each library, the SDKs of each platform, and a person who knows each library to review its implementation: they are work for several people, not for one afternoon.

## Phase 1b: the same application with views, in Avalonia.Headless

The console comparison above does not give every library the same work: Routeables builds a view and a view model for each page, a navigation service of your own builds nothing, ReactiveUI has no view to locate on a console. Phase 1b gives them the same ground: an Avalonia application with no screen (`Avalonia.Headless`), a real window, and for every page a real view (a control with a text) that the library builds and shows. After each step the queued work of the application (binding, layout) is run, the same for everyone. The service of your own is left out: it has no views.

- `benchmarks/Routeables.Comparison.Avalonia`: Routeables (a `RouteableRegion` in the window, 50 routes, a `RouteableFor<TViewModel>` view each) and ReactiveUI routing (the view host is written in the application: it shows the view of the page the router gives; `RoutedViewHost` of ReactiveUI.Avalonia does the same with its view locator and was not used because its API could not be checked here). Avalonia 12.
- `benchmarks/Routeables.Comparison.Prism`: Prism.Avalonia (region navigation by name, the journal for Back and Forward, `IConfirmNavigationRequest` to refuse leaving, DryIoc). Prism.Avalonia 9 is built for Avalonia 11 (the support of 12 is not released), so it runs in a program of its own, on Avalonia 11.3: **the two programs do not run the same Avalonia**, and their numbers are read side by side, not as one table.

What Prism does and does not give: it has Back and Forward (the journal) and a veto (`IConfirmNavigationRequest`), no guard (written in front of `RequestNavigate`), no typed route parameter (a query string, `C3?id=7`), and it keeps every view it has shown in the region until the application removes it (clearing the history removes them in the driver). Every Prism page gets its dependency injected (`IClock`, a singleton), the five pages that read it are not told apart.

Written without being able to restore the packages: both steps of the CI job "Same application, with views" are tolerated until a run is green, and the Prism namespaces (`Prism.Navigation.Regions`) are the least certain part. Nobody who uses Prism daily has read the driver yet.

## Results

### Phase 1b: with views (three runs, November 2026)

.NET 10.0.15, Ubuntu 26.04.1 LTS, 4 cores, workstation GC, 200 steps, 30 runs after 5 warm-ups, Avalonia.Headless, a real view in a real window for every page. Every library ended on the expected page at every step (ReactiveUI: 180 of 200, no Forward). Three runs on a shared runner: the allocations repeat to a few per cent, the times and the heap do not.

| Library | Avalonia | µs / step | B / step | heap after 1000 changes (KiB) | alive after the history is cleared |
|---|---|---:|---:|---:|---:|
| Routeables | 12 | 305 – 412 | 61 262 – 61 732 | 9 201 – 9 371 | 0 of 100 |
| ReactiveUI routing | 12 | 148 – 213 | 30 022 – 30 053 | 1 202 – 2 745 | 0 of 100 |
| Prism, one run | 11.3 | 40 (35 – 40 in two runs) | 4 680 | 286 | 100 of 100 |

- With the views, Routeables costs about 2 times the time and 2 times the allocations of ReactiveUI (1.8 to 2.3 for the time, 2.0 for the allocations), against 3.7 and 6.5 times without views: building and laying out a view is most of what a step costs, and it is the same work for both.
- The heap measured is what the application holds after 1000 changes, so it grows with the history, which keeps the pages it has shown: about 9 KB per entry with Routeables, 1 to 3 KB with ReactiveUI (its navigation stack keeps only the view models). The earlier note here that it "is not growing" had no measure behind it and is withdrawn. That the pages are released when the history is cleared (0 of 100) is measured; what makes an entry of Routeables 3 to 8 times heavier with views (the view kept for Back and Forward is the first thing to check) is not.
- **The Prism line is not yet a comparison.** Its driver now checks that the region shows a view and passes, but its 4.7 KB per step is still six times below ReactiveUI's 30 KB for the same kind of view, and it runs on another Avalonia. Either Prism is that much lighter, or Avalonia 11 lays a view out for far less than Avalonia 12, or the view is not laid out as in the other two. To tell them apart the Prism program now also runs ReactiveUI routing on the same Avalonia 11: the next run gives ReactiveUI on 11 against Prism on 11 (the library alone), and against ReactiveUI on 12 (the Avalonia version alone). The 100 pages still alive after the history is cleared are not explained and are not written as a defect of Prism.

### Phase 1, console (kept as a record; its ground is not equal)

Phase 1, two runs of the CI job "Same application, three libraries" (November 2026, the second after the ReactiveUI driver took its dependency from the container): .NET 10.0.15, Ubuntu 26.04.1 LTS, 4 cores, workstation GC, 200 steps, 30 runs after 5 warm-ups. Every library ended 200 of 200 steps on the expected page (ReactiveUI: 180 of 200, its 20 Forward steps are skipped because it has no Forward).

A shared CI runner is not a quiet machine, and the two runs show it: every time was 1.5 to 2 times longer in the second, the service of your own included (0.04 to 0.08 ms), while the allocations did not move. Read the **allocations and the ratios**; the absolute times only give an order of magnitude.

| Library | µs / step (first run, second run) | B / step | Time against ReactiveUI | Allocations against ReactiveUI |
|---|---:|---:|---:|---:|
| Routeables | 58 – 106 | about 25 400 | 2.1 times | 6.3 times |
| ReactiveUI routing | 28 – 40 | about 4 000 | 1 | 1 |
| Service of your own | 0.2 – 0.4 | 86 | | |

| Library | heap after 1000 changes (KiB) | first 100 pages alive | alive after the history is cleared |
|---|---:|---:|---:|
| Routeables | 476 – 477 | 100 of 100 | 0 |
| ReactiveUI routing | 247 – 252 | 100 of 100 | 0 |
| Service of your own | 51 – 52 | 100 of 100 | 0 |

What it says, and what it does not:

- A page change costs Routeables about twice the time of ReactiveUI and six times its allocations, and a few hundred times a dictionary of factories. That is the price of what the two others leave to the application in this scenario: route matching and typed parameters, guards that redirect, a veto on leaving, the history with Forward, the lifetime of each view model (created and disposed with its page) and the cancellation of a change that is overtaken.
- Taking ReactiveUI's dependency from the container changed nothing measurable (786 to 787 KiB per run): a singleton resolved five times in 200 steps.
- At 60 to 100 µs a change is far below what a user can see (a frame is 16 000 µs); the figure matters for a test that runs thousands of changes, not for an application.
- Nothing is leaked: after the history is cleared no page stays alive in any of the three. The heap that remains after 1000 changes is larger with Routeables (476 KiB against 52 and 250); the measure does not say what holds it, and it is not growing: the pages are released with the history.
- It does not compare the platforms (Blazor, MAUI, WPF…), the cost of drawing a page, nor the libraries on a task they were not built for.
