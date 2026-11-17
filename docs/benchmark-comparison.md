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

**The ReactiveUI version was written without being able to restore ReactiveUI**, so it has never been compiled or run: its CI step cannot fail the job until one run has passed, and a person who uses ReactiveUI every day must read it before its numbers are quoted (the rule above applies to it first). The runs of the other two were made on a 2-core Linux sandbox and checked (200 of 200 steps on the expected page); nothing was measured on a machine fit to be named, so no number is published here. The CI job writes the three tables in the summary of each run; copy them below with the date, the versions and the runner when a run is clean.

Each phase ends with its results published in this page, with the date, the versions and the machine. Phases 2 and 3 need the packages of each library, the SDKs of each platform, and a person who knows each library to review its implementation: they are work for several people, not for one afternoon.

## Results

None yet. Phase 1 can produce them now (the summary of the CI job "Same application, three libraries"); they are published here once someone who uses ReactiveUI has read its implementation.
