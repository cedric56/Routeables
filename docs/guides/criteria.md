# The 17 criteria of a routing system

[The MAUI routing gist](https://gist.github.com/mattleibow/c140ad06bf1be77b38bf1f7e175b9e9d) ends with a matrix of 17 capabilities. This page says, for each one, what Routeables gives and which test proves it. Every test runs without a user interface.

| # | Criterion | In Routeables | Proof (`tests/Routeables.Test`) |
| --- | --- | --- | --- |
| 1 | Unified registration | `services.AddRoutes(nav => nav.Tab<…>().Page<…>().Modal<…>())` declares the pages, the tabs of the menu and the modals in one place | `RouteCriteriaTests`: `Pages_tabs_and_modals_are_declared_in_one_place`, `A_tab_is_a_page_and_an_entry_of_the_menu` |
| 2 | Type-safe routes | `router.GoToAsync<OrderViewModel>(values)` from anywhere, no route string; `[RouteableViewModel]` generates `GoToXAsync` on the callers it names ([Typed routing methods](typed-routes.md)) | `RouteCriteriaTests`: `A_page_is_reached_by_its_view_model_without_writing_a_route`; `RouteRegistrationGeneratorTests` |
| 3 | Type-safe parameters | The generated methods mirror the constructor of the destination; `GoToRouteAsync<TViewModel, TData>` checks the data type with `IInitializeAsync<TData>`; `RouteableKey<T>` types a value | `RouterTypedExtensionsTests`, `RouteableKeyTests` |
| 4 | Route templates | `/orders/{id}/{tab?}?{note?}`, a literal beats a typed parameter, which beats an untyped one | `RouteParserTests`, `RouterGenericRouteTests` |
| 5 | Route constraints | `{id:int}`, `long`, `bool`, `guid`, `decimal`, `double`, a registered type (`{person:Person}`); a value that breaks its constraint is refused, going to a route or building one | `RouteParserTests`; `RouteCriteriaTests.A_value_that_breaks_its_constraint_is_refused` |
| 6 | Programmatic tabs | `IMainViewModel.Entries` and `SelectedEntry`, `MenuEntry<T>`, `router.GoToRouteAsync(entry)`, `AddMenuEntries` | `MainViewModelTests`; `RouteCriteriaTests.A_tab_is_a_page_and_an_entry_of_the_menu` |
| 7 | Programmatic modals | `provider.ShowModalAsync<TViewModel, TResult>()`, declared with `Modal<TView, TViewModel>()`; the dialogs and notifications are services (`IRouteableDialogService`) | `ModalSessionTests`, `DialogSessionTests`; `RouteCriteriaTests.Pages_tabs_and_modals_are_declared_in_one_place` |
| 8 | Route result | Every move returns a `RouteableResult` (success, exception, redirection); a modal returns its typed result; `RouteableReturn` brings the user back after a guard | `RouterResultTests`, `RouteableReturnTests` |
| 9 | ViewModel lifecycle | `IInitializeAsync`, entering, leaving, `CanLeaveAsync`, `OnSubscribe`, destroy ([Lifecycle](lifecycle.md)) | `RouterLifecycleTests` |
| 10 | Route guards | `IRouteGuard`: let in, cancel or redirect, with the route to come back to | `RouteGuardTests`, `RouteGuardDecisionAndLifetimeTests` |
| 11 | State as data | The history is a list of routes: `RouteableState.Capture`, `Serialize`, `Deserialize`, `RestoreAsync`. With `CaptureSnapshots` each page also keeps what its view model saves (`IRouteableState`) and the values it was reached with | `RouteableDeepLinksTests`; `RouteCriteriaTests`: `The_values_a_page_was_reached_with_come_back_with_the_page`, `A_page_shown_with_its_own_view_is_saved_when_it_is_given_a_route` |
| 12 | Deep linking | A route is a link: `RouteableDeepLinks`, `RouteableLinkInbox` (links that arrive before the first page), the address bar of Blazor | `RouteableDeepLinksTests` |
| 13 | DI-first | Pages and view models are built by the container (`ActivatorUtilities`, `[FromContainer]`, keyed services); lifetimes per route | `RegisterRouteLifetimeTests`, `ConstructorArgumentsTests` |
| 14 | Scoped parameters | `RouteableArgs.WithValue` belongs to one history entry (two entries never share them) and is saved and restored with that entry | `RouteableArgsIsolationTests`; `RouteCriteriaTests.The_values_a_page_was_reached_with_come_back_with_the_page` |
| 15 | Source generation | Registration (`AddGeneratedRoutes`), typed methods, coordination of segments; no reflection at run time (Native AOT in CI) | `RouteRegistrationGeneratorTests`, `RouteCoordinationGeneratorTests`, the `aot` job |
| 16 | Back | `GoBackAsync`, `ForwardAsync`, `PopAsync`, `GoBackToRootAsync`, `MoveToAsync`, `CanGoBack`, the system back button (`RouteableBack`) | `RouterLifecycleTests`, `RouteableBackTests` |
| 17 | Testable without UI | The router, the history, the guards and the view models run in a plain test; the hosts only draw | all the tests of `Routeables.Test` |

## What is kept in the saved state

- The address of each page, and what its view model writes with `IRouteableState`.
- The values given with `WithValue` that are text, numbers, `bool`, `Guid` or dates. They come back as the same types, and the values that are already in the address are not kept twice. An object of another type is not kept: put what identifies it in the route.
- A page shown with `GoAsync(view, viewModel)` is kept when its arguments carry a route (`RouteableArgs.Create().WithRoute("/orders/42")`): it is rebuilt from that route. Without a route there is nothing to rebuild it from.

## Values for a route without writing it

```csharp
[Routeable("/orders/{id:int}/{tab?}?{note?}")]
public class OrderViewModel(int id, string? tab, string? note) { … }

await router.GoToAsync<OrderViewModel>(new Dictionary<string, object?> { ["id"] = 42, ["note"] = "gift" });
await router.GoToAsync<OrderViewModel>(new { id = 42 });   // reads the properties by reflection: not for a trimmed application
```

The template registered for the view model is filled: numbers are written with the invariant culture, each value is escaped, an optional value that is missing leaves its segment out, and a required one that is missing gives a failed result that names it.
