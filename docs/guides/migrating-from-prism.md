# Migrating from Prism

Routeables does the route change part of Prism: views and view models, routes with parameters, a history, a lifecycle with guards, regions and dialogs. It stays out of the rest. It ships **no** `DelegateCommand`, **no** view-model locator, **no** event aggregator and **no** module catalog: the MVVM library is your choice (CommunityToolkit.Mvvm, ReactiveUI, plain C#). A feature assembly is one extension method (see [Modules](routes.md#modules)).

## Concepts side by side

| Prism | Routeables |
| --- | --- |
| `containerRegistry.RegisterForNavigation<View, ViewModel>("Name")` | `[Routeable("/name")]` on the view model. The view called `NameView` is found by name; otherwise `[RouteableView(typeof(NameViewModel))]` on the view. Then `services.AddGeneratedRoutes()`. |
| `INavigationService.NavigateAsync("Name")` | `router.GoToAsync("/name")` |
| `NavigateAsync("Name?id=42")` or `NavigationParameters` | A template parameter, `"/name/{id}"`, matched to a constructor parameter of the view model. Other values go in `RouteableArgs.Create().ForViewModel(...)`. See [Passing arguments](arguments.md). |
| `GoBackAsync()` | `router.GoBackAsync()` (keeps the entry for `ForwardAsync`), or `router.PopAsync()` to leave a page for good |
| `INavigationAware.OnNavigatedTo` / `OnNavigatedFrom` | `IRouteable.OnEnteredAsync` / `OnLeftAsync`; `OnEnteringAsync` / `OnLeavingAsync` run just before |
| `IConfirmNavigation.CanNavigate` | `IRouteable.CanLeaveAsync` |
| `IInitializeAsync` | `IInitializeAsync` (same idea, with the arguments) |
| `IDestructible` | `IAsyncDisposable`, called when the entry leaves the history for good |
| `IRegionManager`, `RegisterViewWithRegion` | `services.AddRegion("name")` and a `RouteableRegion` in the view. See [Regions](regions.md). |
| `IDialogService` | `IRouteableDialogs` (`MessageBox`, `ContentDialog`, `TaskDialog`) and `IRouteableNotificationService` from `Routeables.Services`, implemented by the optional `Routeables.<Host>.Services` package |
| Shell and tab routing | A main view model derived from `MainViewModelBase`, which returns the entries of the menu |
| `ViewModelLocator.AutoWireViewModel` | Not provided. The router builds the view model and gives it to the view. |
| `DelegateCommand`, `BindableBase` | Not provided. Use your MVVM library. |
| `IEventAggregator` | Not provided. Use `WeakReferenceMessenger` from CommunityToolkit.Mvvm, or whatever you already use. |
| `IModule`, `IModuleCatalog` | Not provided. A feature assembly exposes an `Add...Module()` extension method that calls `services.AddGeneratedRoutes()` and `services.AddMenuEntries(...)`; no loading at run time (it would break trimming and Native AOT). See [Modules](routes.md#modules). |

## A view model, before and after

Before, with Prism:

```csharp
public class OrderViewModel : BindableBase, INavigationAware, IConfirmNavigation
{
    private readonly INavigationService _navigation;
    public OrderViewModel(INavigationService navigation) => _navigation = navigation;

    public int Id { get; private set; }
    public DelegateCommand CloseCommand => new(async () => await _navigation.GoBackAsync());

    public void OnNavigatedTo(INavigationParameters parameters) => Id = parameters.GetValue<int>("id");
    public void OnNavigatedFrom(INavigationParameters parameters) { }
    public bool CanNavigate(INavigationParameters parameters) => !HasUnsavedChanges;
}

containerRegistry.RegisterForNavigation<OrderView, OrderViewModel>("Order");
await navigation.NavigateAsync("Order?id=42");
```

After, with Routeables and CommunityToolkit.Mvvm:

```csharp
[Routeable("/orders/{id:int}")]
public sealed partial class OrderViewModel(IRouter router, int id) : ObservableObject, IRouteable
{
    public int Id { get; } = id;

    [RelayCommand]
    private Task CloseAsync() => router.GoBackAsync();

    public Task<bool> CanLeaveAsync(CancellationToken ct = default) => Task.FromResult(!HasUnsavedChanges);
}

await router.GoToAsync("/orders/42");
```

The route, its parameter and its type are written once. The id is a constructor parameter, so the view model is valid as soon as it exists, and the generator checks the route at compile time.

## What changes in practice

1. **Register nothing by hand.** Remove the `RegisterForNavigation` calls, put `[Routeable]` on the view models, and call `AddGeneratedRoutes()` once.
2. **Replace names with routes.** `"Order"` becomes `"/orders/{id:int}"`. Typed route methods can be generated so a call site does not contain a string: see [Typed route methods](typed-routes.md).
3. **Move the parameters.** Read them from constructor parameters instead of from `INavigationParameters` in `OnNavigatedTo`.
4. **Move the lifecycle.** `OnNavigatedTo` is usually `InitializeAsync` (loading) or `OnEnteredAsync` (reacting to the arrival); `CanNavigate` is `CanLeaveAsync`.
5. **Keep your MVVM library.** Commands, observable properties and messaging do not change.
6. **Replace modules by extension methods.** Each feature assembly exposes one method that calls `AddGeneratedRoutes()`, registers its services and adds its entries to the menu with `AddMenuEntries`.

## Pick the packages

See "What do I install?" in the [README](../../README.md). A Prism application usually needs the host package (`Routeables.Razor`, `Routeables.Maui` or `Routeables.Avalonia`), the matching `.Services` package if it used `IDialogService`, and `Routeables.Regions` if it used regions.
