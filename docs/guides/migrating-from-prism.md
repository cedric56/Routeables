# Migrating from Prism

Routeables does the route change part of Prism: views and view models, routes with parameters, a history, a lifecycle with guards, regions and dialogs. It stays out of the rest. It ships **no** `DelegateCommand`, **no** view-model locator, **no** event aggregator and **no** module catalog: the MVVM library is your choice (CommunityToolkit.Mvvm, ReactiveUI, plain C#). A feature assembly is one extension method (see [Modules](routes.md#modules)).

## Concepts side by side

| Prism | Routeables |
| --- | --- |
| `containerRegistry.RegisterForNavigation<DogsView, DogsViewModel>("Dogs")` | `[Routeable("/dogs")]` on the view model. The view called `DogsView` is found by name; otherwise `[RouteableView(typeof(DogsViewModel))]` on the view. Then `services.AddGeneratedRoutes()`. |
| `INavigationService.NavigateAsync("Dogs")` | `router.GoToAsync("/dogs")` |
| `NavigateAsync("DogDetail?name=Eurohound")` or `NavigationParameters` | A template parameter, `"/dogs/{name}"`, matched to a constructor parameter of the view model. Other values go in `RouteableArgs.Create().ForViewModel(...)`. See [Passing arguments](arguments.md). |
| `GoBackAsync()` | `router.GoBackAsync()` (keeps the entry for `ForwardAsync`), or `router.PopAsync()` to leave a page for good |
| `INavigationAware.OnNavigatedTo` / `OnNavigatedFrom` | `IRouteable.OnEnteredAsync` / `OnLeftAsync`; `OnEnteringAsync` / `OnLeavingAsync` run just before |
| `IConfirmNavigation.CanNavigate` | `IRouteable.CanLeaveAsync` for the page that is left; `IRouteGuard` for a rule on the page that is entered, with redirect. See [Back, deep links, guards and diagnostics](system-integration.md). |
| `IInitializeAsync` | `IInitializeAsync` (same idea, with the arguments) |
| `IDestructible` | `IAsyncDisposable`, called when the entry leaves the history for good |
| `IRegionManager`, `RegisterViewWithRegion` | `services.AddRegion("adopt")` and a `RouteableRegion` (or `RouteableRegionItems`) in the view. See [Regions](regions.md). |
| `IDialogService` | `IRouteableDialogService` (message boxes, content dialogs and task dialogs) and `IRouteableNotificationService` from `Routeables.Services`, implemented by each host and registered by `UseRouteables` |
| Shell and tab routing | A main view model derived from `MainViewModelBase`, which returns the entries of the menu |
| `ViewModelLocator.AutoWireViewModel` | Not provided. The router builds the view model and gives it to the view. |
| `DelegateCommand`, `BindableBase` | Not provided. Use your MVVM library. |
| `IEventAggregator` | Not provided. Use `WeakReferenceMessenger` from CommunityToolkit.Mvvm, or whatever you already use. |
| `IModule`, `IModuleCatalog`, `ModuleInitializationMode` | No catalog. A feature assembly exposes an `Add...Module()` extension method that calls `services.AddGeneratedRoutes()` and `services.AddMenuEntries(...)`; the application calls it at start (`WhenAvailable`), or `IRouteableModules.Load(...)` calls it when the user asks (`OnDemand`), from code or from a DLL. See [Modules](routes.md#modules). |

## A view model, before and after

The page is the dog detail of the XAnimals sample (`DogDetailViewModel`, opened from the list of dogs).

Before, with Prism:

```csharp
public class DogDetailViewModel : BindableBase, INavigationAware, IConfirmNavigation
{
    private readonly INavigationService _navigation;
    public DogDetailViewModel(INavigationService navigation) => _navigation = navigation;

    public Animal? Animal { get; private set; }
    public DelegateCommand BackCommand => new(async () => await _navigation.GoBackAsync());

    public void OnNavigatedTo(INavigationParameters parameters)
        => Animal = DogData.Dogs.FirstOrDefault(d => d.Name == parameters.GetValue<string>("name"));
    public void OnNavigatedFrom(INavigationParameters parameters) { }
    public bool CanNavigate(INavigationParameters parameters) => !BlockLeaving;
}

containerRegistry.RegisterForNavigation<DogDetailView, DogDetailViewModel>("DogDetail");
await navigation.NavigateAsync("DogDetail?name=Eurohound");
```

After, with Routeables (the sample's `DogDetailViewModel` derives from `AnimalDetailViewModel`, which holds the animal and the back command; written out here with CommunityToolkit.Mvvm):

```csharp
[Routeable("/dogs/{name}")]
public sealed partial class DogDetailViewModel(IRouter router, string name) : ObservableObject, IRouteable
{
    public Animal? Animal { get; } = DogData.Dogs.FirstOrDefault(d => d.Name == name);

    [RelayCommand]
    private Task BackAsync() => router.GoBackAsync();

    public Task<bool> CanLeaveAsync(CancellationToken ct = default) => Task.FromResult(!BlockLeaving);
}

await router.GoToAsync("/dogs/Eurohound");
```

The route, its parameter and its type are written once. The name is a constructor parameter, so the view model is valid as soon as it exists, and the generator checks the route at compile time.

## What changes in practice

1. **Register nothing by hand.** Remove the `RegisterForNavigation` calls, put `[Routeable]` on the view models, and call `AddGeneratedRoutes()` once.
2. **Replace names with routes.** `"DogDetail"` becomes `"/dogs/{name}"`. Typed route methods can be generated so a call site does not contain a string: see [Typed route methods](typed-routes.md).
3. **Move the parameters.** Read them from constructor parameters instead of from `INavigationParameters` in `OnNavigatedTo`.
4. **Move the lifecycle.** `OnNavigatedTo` is usually `InitializeAsync` (loading) or `OnEnteredAsync` (reacting to the arrival); `CanNavigate` is `CanLeaveAsync`.
5. **Keep your MVVM library.** Commands, observable properties and messaging do not change.
6. **Route after the hook, not inside it.** A route change started from `CanLeaveAsync`, `OnLeavingAsync`, `OnEnteringAsync` or a `Routing` handler of the same router throws. See [Routing from a hook](lifecycle.md#routing-from-a-hook).
7. **Replace modules by extension methods.** Each feature assembly exposes one method that calls `AddGeneratedRoutes()`, registers its services and adds its entries to the menu with `AddMenuEntries`.

## Pick the packages

See "Add what you need" in the [README](https://github.com/cedric56/Routeables/blob/master/README.md). A Prism application usually needs the host package (`Routeables.Razor`, `Routeables.Maui`, `Routeables.Avalonia`, `Routeables.Uno` or `Routeables.Wpf`), the matching `.Services` package if it used `IDialogService`, and `Routeables.Regions` if it used regions.
