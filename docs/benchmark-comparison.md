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

- **The dependency** of the five pages that have one was read from a field, while Routeables resolves it through the container: the ReactiveUI driver now asks the same container (`Microsoft.Extensions.DependencyInjection`, a singleton) each time. The numbers below were measured before this change, so ReactiveUI's will go up a little at the next run.
- **The pattern of the pages with a parameter** (`['C', ..]`) took any name that starts with C: it is now restricted to `C` and a digit, with a parameter.
- **Not changed, and stated:** Routeables builds a view and a view model through the container for every page, ReactiveUI only a view model (a console has no views to locate); Routeables calls the hooks of the page and disposes it when it leaves the history, ReactiveUI has no lifecycle (the pages are still collected: none is alive after the history is cleared). These are part of what the allocations pay for. The router of Routeables runs without a scheduler or a `Task.Yield`, as the immediate scheduler does here.
- **Checked and already the same:** Back does not run the guards again in either (the guard of Routeables looks at pushes only); the return to the page that was asked for after signing in is done by the scenario for every driver (`Continue`), not by a library; the name of the page shown is compared with the model at every step.
- **Not exercised by the scenario:** the order of the checks when a dirty edit page is left for a guarded page (leaving first, in the model). Both drivers do it in that order, but no step proves it.

The ReactiveUI step stays tolerated until someone who uses the library daily has read the driver. The runs were made on a shared CI runner, nothing was measured on a machine fit to be named. The CI job writes the three tables in the summary of each run; copy them below with the date, the versions and the runner when a run is clean.

Each phase ends with its results published in this page, with the date, the versions and the machine. Phases 2 and 3 need the packages of each library, the SDKs of each platform, and a person who knows each library to review its implementation: they are work for several people, not for one afternoon.

## Results

Phase 1, one run of the CI job "Same application, three libraries" (November 2026): .NET 10.0.15, Ubuntu 26.04.1 LTS, 4 cores, workstation GC, 200 steps, 30 runs after 5 warm-ups. A shared CI runner is not a quiet machine: read the orders of magnitude, not the decimals.

Every library ended 200 of 200 steps on the expected page (ReactiveUI: 180 of 200, its 20 Forward steps are skipped because it has no Forward).

| Library | median ms / run | µs / step | KiB allocated / run | B / step |
|---|---:|---:|---:|---:|
| Routeables | 11.64 – 11.84 | 58 – 59 | 4971 – 4982 | 25 453 – 25 505 |
| ReactiveUI routing (before the container for its dependency) | 5.56 | 27.8 | 786 | 4023 |
| Service of your own | 0.04 – 0.05 | 0.2 | 17 | 86 |

(Routeables and the service of your own were run twice, once in each job step; the range is the two runs.)

| Library | heap after 1000 changes (KiB) | first 100 pages alive | alive after the history is cleared |
|---|---:|---:|---:|
| Routeables | 476 | 100 of 100 | 0 |
| ReactiveUI routing | 247 | 100 of 100 | 0 |
| Service of your own | 52 | 100 of 100 | 0 |

What it says, and what it does not:

- A page change costs Routeables about 58 µs and 25 KB here, about twice ReactiveUI's time and six times its allocations, and a few hundred times a dictionary of factories. That is the price of what the two others leave to the application in this scenario: route matching and typed parameters, guards that redirect, a veto on leaving, the history with Forward, the lifetime of each view model (created and disposed with its page) and the cancellation of a change that is overtaken.
- At 58 µs a change is far below what a user can see (a frame is 16 000 µs); the figure matters for a test that runs thousands of changes, not for an application.
- Nothing is leaked: after the history is cleared no page stays alive in any of the three. The heap that remains after 1000 changes is larger with Routeables (476 KiB against 52 and 247); the measure does not say what holds it, and it is not growing: the pages are released with the history.
- It does not compare the platforms (Blazor, MAUI, WPF…), the cost of drawing a page, nor the libraries on a task they were not built for.
