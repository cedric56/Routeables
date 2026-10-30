# Lifecycle, guards and events

## Lifecycle and guards

Implement any of these on a view model (or a view). `AnimalsViewModel` of the XAnimals sample (`Samples/Bases/XAnimals.Common/ViewModels/AnimalsViewModel.cs`) implements `CanLeaveAsync` and `OnEnteredAsync`; the full list of members:

```csharp
public class AnimalsViewModel : IRouteable, IInitializeAsync, IAsyncDisposable
{
    // Return false to block leaving this page.
    public Task<bool> CanLeaveAsync(CancellationToken ct = default)
        => Task.FromResult(!HasUnsavedChanges);

    public Task OnEnteringAsync(IRouteableArgs context, CancellationToken ct = default)
        => Task.CompletedTask;

    public Task OnLeavingAsync(IRouteableArgs context, CancellationToken ct = default)
        => Task.CompletedTask;

    // Async loading after construction, with the navigation arguments.
    public Task InitializeAsync(IRouteableArgs args, CancellationToken ct = default)
        => Task.CompletedTask;

    // Called once the entry has left the history for good.
    public ValueTask DisposeAsync()
        => ValueTask.CompletedTask;
}
```

Every member of `IRouteable` has a default implementation, so implement only what you need.

Order of events for a route change from page A to page B:

1. `CanLeaveAsync` on A (view model, then view). If one returns `false`, the route change is cancelled and `result.Cancelled` is `true`.
2. `InitializeAsync` on B (view model, then view). Nothing has been torn down yet, so a failure leaves A in place.
3. `OnLeavingAsync` on A, then `OnEnteringAsync` on B.
4. The history is updated, and entries removed by this change are disposed.

### Routing from a hook

A router makes one route change at a time. A route change started **from a hook of that same router** (`CanLeaveAsync`, `InitializeAsync`, `OnLeavingAsync`, `OnEnteringAsync`, or a `Routing` or `Routed` handler) does not wait for the first one: the router throws an `InvalidOperationException` ("Reentrant route changes are not supported"). The history would otherwise change under a route change that has not finished.

Route after the hook has returned: from the command that started the first route change, once it has its result, or from a handler that queues the work (a `RegionFollower` does exactly that for regions). The rule is per router, so a hook can route another router, a region for instance.

If you come from Prism and started another request from `OnNavigatedTo`, move that call out of the hook.

### Cleanup

There is no dedicated cleanup interface: use the standard .NET ones. When an entry leaves the history, the router disposes its view model and its view with `DisposeAsync()` if they implement `IAsyncDisposable`, otherwise with `Dispose()` if they implement `IDisposable`. An exception thrown while disposing is swallowed, so cleanup never fails a route change.

Entries leave the history on `PopAsync`, when a new route cuts off the forward entries, and on `EraseHistoryAsync` (the history has no limit). `GoBackAsync`, `ForwardAsync` and `MoveToAsync` only move the cursor, so they dispose nothing.

A view and a view model that are the same object, or an instance shared with an entry that stays in the history, are initialized and disposed once and are never disposed while still in use.

`Singleton` and `Scoped` views and view models belong to the cache that builds them: they are disposed with the provider or the scope, never by the router when they leave the history, since the next route change to the same route shows the same instance.

A menu entry (`IRouteableEntry`) remembers the view and view model it showed last and gives them back on its next click. If the router has disposed them since (the entry left the history), the entry gets new ones instead of a disposed page.

> [!NOTE]
> When a scope or provider is disposed **synchronously** (`Dispose`, not `DisposeAsync`), a view or view model that only implements `IAsyncDisposable` has its `DisposeAsync()` started and not awaited: a synchronous call cannot wait for it, on a UI thread it would risk a deadlock, and on Blazor WebAssembly (one thread) it is impossible. The disposal finishes afterwards and an error goes to the error sink. Dispose the scope with `DisposeAsync` when you can await.
>
> The DI container also disposes the transient `IDisposable` objects it created when its scope ends, so a view model can be disposed twice. Make `Dispose` idempotent. Blazor does the same for components it renders, so a Razor component must be safe to dispose twice.

### Services injected into a page

A service that a view or a view model receives (constructor argument or `[Inject]`) and that implements `IDisposable` or `IAsyncDisposable` must be registered `Scoped` or `Singleton`, not `Transient`.

Pages are built from the host's provider: the root provider on Avalonia and MAUI, the circuit's scope on Blazor Server. The .NET container keeps every `Transient` disposable object it creates until that provider or scope is disposed. The router disposes the page, not what the container created for it, so a transient disposable dependency stays in memory after each route change. Measured with a page that takes one: 200 route changes leave 200 instances alive, where a `Scoped` or `Singleton` registration leaves one.

```csharp
services.AddSingleton<IPriceService, PriceService>();   // disposable, shared: fine
services.AddTransient<IFormatter, Formatter>();         // not disposable: fine
// services.AddTransient<IPriceService, PriceService>(); // disposable and transient: kept until the app ends
```

If a page needs its own instance of a disposable service, create it inside the page and dispose it in the page's `Dispose`, which the router calls when the page leaves the history.

## Events

```csharp
router.Routing += (_, e) => { /* about to navigate */ };
router.Routed  += (_, e) => { /* navigation finished */ };
```

A handler that throws never fails or masks the route change, and a `Dispose` that throws when an entry leaves the history never fails it either. A coordination (`IRouteCoordinator`) that is rejected or fails is reported the same way, in addition to the result it returns, so an application that ignores that result still sees it. The callbacks you give to dialogs and notifications (`Opened`, `Closing`, `OnClick`...) are run through `RouteableErrors.Guard`, which reports an exception instead of letting it reach the UI; use it for your own callbacks run from an event handler. These errors are not lost: register an `IRouteableErrorSink` (`services.AddSingleton<IRouteableErrorSink, MySink>()`) to log them; without one they go to `System.Diagnostics.Trace`.

Some errors are reported where no container is at hand (the callbacks run by `Guard`, the follower of a region, a view model base). Those reach the sink set on `RouteableErrors.Default`, so one sink can receive everything, for instance to write to Serilog:

```csharp
public sealed class SerilogErrorSink : IRouteableErrorSink
{
    public void Report(string source, Exception exception)
        => Log.Warning(exception, "Routeables: {Source}", source);
}

var sink = new SerilogErrorSink();
RouteableErrors.Default = sink;                                  // errors reported without a container
services.AddSingleton<IRouteableErrorSink>(sink);                // errors reported with one
```

A sink registered in the container wins over `Default` for the errors that have a container.

## Subscribing once in a view

`InitializeAsync` of a view runs again each time its section is shown. Do not register a messenger or an event there: override `OnSubscribe` and `OnUnsubscribe` of `RouteableFor` (Avalonia, WPF, Uno, MAUI), as in Razor. `OnSubscribe` is called once for each view model the view shows (on the first `InitializeAsync`), and `OnUnsubscribe` when the view is disposed or gets another view model.

```csharp
public partial class HomeView : RouteableFor<HomeViewModel>
{
    protected override void OnSubscribe()
        => WeakReferenceMessenger.Default.Register<HomeView, MyMessage>(this, (view, message) => view.Handle(message));

    protected override void OnUnsubscribe()
        => WeakReferenceMessenger.Default.UnregisterAll(this);
}
```

If the view overrides `InitializeAsync`, call `base.InitializeAsync(args, ct)` so the hook runs.

### Subscribing once in a view model

`IRouteable` has the same pair for a view model (or any page that implements it): `OnSubscribe()` is called by the router once for each instance, the first time it enters it, before `OnEnteringAsync` (which runs again on every back and forward); `OnUnsubscribe()` is called when the router disposes the page because it left the history. A page the container owns (a singleton, a scoped page) is not disposed by the router, so `OnUnsubscribe` is not called for it. An exception in `OnSubscribe` fails the route change; one in `OnUnsubscribe` is reported to the `IRouteableErrorSink` and the page is still disposed.

## Work that is not awaited

`_ = Task.Run(...)` in `OnEnteringAsync` loses the exception of the task. `AsyncHelper.FireAndForget` starts the work without waiting for it and reports its exception to the `IRouteableErrorSink` (a cancellation is not an error); `onError` shows it in the view:

```csharp
public override Task OnEnteringAsync(IRouteableArgs args, CancellationToken ct = default)
{
    AsyncHelper.FireAndForget(async () =>
    {
        await cache.LoadAsync();                 // what follows the await is back on the UI context
        Persons = new(cache.Entities);
    }, provider, "persons", onError: ex => Error = ex.Message);
    return Task.CompletedTask;
}
```

`task.Forget(provider)` does the same for a task you already have. From synchronous code that must wait (a constructor, a property), `AsyncHelper.RunSync(() => LoadAsync())` runs the method on the thread pool, so it cannot deadlock on the UI context, keeps the culture of the caller and throws the original exception.

## Finding out why a route did not show

`GoToRouteAsync` returns a `RouteableResult` (`Success`, `Cancelled`, `Exception`) instead of throwing. `MainViewModelBase` writes the reason to the debug output when an entry is not shown, and calls the virtual `OnRoutingFailed(entry, result)`: override it to log, or put a breakpoint in it.

```csharp
protected override void OnRoutingFailed(IRouteableEntry entry, RouteableResult result)
{
    base.OnRoutingFailed(entry, result);
    logger.LogWarning(result.Exception, "Route {Route} not shown (cancelled: {Cancelled})", entry.Route, result.Cancelled);
}
```

The `.pdb` files are inside the NuGet packages, with Source Link: in Visual Studio enable *Debug > Options > Enable Source Link support* and disable *Just My Code* to step into Routeables.
