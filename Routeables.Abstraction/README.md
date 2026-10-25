# Routeables.Abstraction

The contracts of Routeables. View models and views depend on these types instead of an API of Blazor, .NET MAUI, Avalonia, Uno Platform or WPF, so the same routing code runs on all five hosts.

The router itself is in [`Routeables.Routing`](../Routeables.Routing/README.md), the platform services are in the host packages. Targets **.NET 10**; its only package is `Microsoft.Extensions.DependencyInjection.Abstractions`.

```xml
<ItemGroup>
  <ProjectReference Include="..\Routeables\Routeables.Abstraction\Routeables.Abstraction.csproj" />
</ItemGroup>
```

## Route from a view model

A view model depends only on `IRouter`:

```csharp
using Routeables.Abstraction;

public class DogsViewModel(IRouter router)
{
    public async Task OpenDogAsync(string name)
    {
        RouteableResult result = await router.GoToAsync($"/dogs/{Uri.EscapeDataString(name)}");
        if (!result.Success && !result.Cancelled)
            throw new InvalidOperationException(result.Exception?.Message);
    }
}
```

`IRouter` also offers `GoBackAsync`, `ForwardAsync`, `PopAsync`, `GoBackToRootAsync`, `MoveToAsync` and `EraseHistoryAsync`. A route change that cannot happen (an unknown address, a page that vetoes leaving) returns a failed or cancelled `RouteableResult` and does not throw.

## What is in it

| Area | Main types |
| --- | --- |
| Router | `IRouter`, `IRouterHistory`, `RouteableResult`, `RouteableRoutingEventArgs`, `RouteableRoutedEventArgs` |
| Routes | `[Routeable]`, `[RouteableView]`, `[RouteableViewModel]`, `RegisterRoute<TView, TViewModel>` |
| Lifecycle | `IRouteable` (every member has a default), `IInitializeAsync`, `IReturnsResult<T>` |
| Arguments | `IRouteableArgs` (values for the view model, for the view, neutral ones), `RouteableParameterAttribute` |
| Menu | `IRouteableEntry`, `IMainViewModel`, `AddMenuEntries`, `IRouteableModules` (modules loaded while running) |
| Errors and locks | `IRouteableErrorSink`, `RouteableErrors`, `ILockCoordinator` |

The dialogs, notifications and theme are in [`Routeables.Services`](../Routeables.Services/README.md), regions in [`Routeables.Regions`](../Routeables.Regions/README.md), segments in [`Routeables.Segments`](../Routeables.Segments/README.md): a project that only routes carries none of them.

## Go further

[Routes and templates](../docs/guides/routes.md), [typed routing methods](../docs/guides/typed-routes.md), [arguments](../docs/guides/arguments.md), [lifecycle and guards](../docs/guides/lifecycle.md), [history](../docs/guides/history.md).
