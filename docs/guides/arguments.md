# Passing arguments

## Passing arguments

Routing arguments travel in an `IRouteableArgs`. Each entry goes to the view model, to the view, or to neither (a shared bag of values):

```csharp
var args = RouteableArgs.Create()
    .ForViewModel(order)                  // matched to a constructor parameter by type
    .WithView("mode", "highlight")        // a named value, passed to the view
    .WithValue("cid", correlationId);     // available to lifecycle hooks, not injected

await router.GoToAsync("/orders/42", args);
```

`ForViewModel`, `ForView` and `ForValues` take plain values (several at once: `ForViewModel(order, back)`); `WithViewModel`, `WithView` and `WithValue` take a name and a value. In a hook, read the values by name: `args.Get<Action>("back")` (or `args.Get<Animal>()` when only one value has that type; two of the same type throw instead of silently taking the first).

`new RouteableArgument(value, name, attribute)` is still there for the rare argument that needs the `attribute` flag in a list.

### Parameters by attribute

A template parameter written `{name::}` (two colons) is not given to a constructor: it is set on a property that has a platform attribute. The property can be on the view model or on the view; a value is given to the view only when it has a property of that name.

| Platform | Attribute | Where |
|---|---|---|
| Blazor | `[Parameter]` | a property of the component: `[Parameter] public string? Panda { get; set; }` |
| MAUI | `[QueryProperty(nameof(Panda), "panda")]` | on the class, naming the property (or `[RouteableParameter]` on the property) |
| Avalonia | `[RouteableParameter]` | on the property |

```csharp
// "/pandas/{panda::}" opens PandaDetailView, and "Red Panda" ends up in its Panda property.
[RouteableView(typeof(PandaDetailViewModel), route: "/pandas/{panda::}")]
```

The view is created before its view model is set, so a view hands the value over when both exist (`PandaDetailView` in the samples).

View-model entries are matched to constructor parameters **by type**, in order. Registered services fill the remaining parameters. Entries with a `null` value are dropped.

For a single typed value delivered after construction, implement `IInitializeAsync<TData>` on the view model and use the typed overload, which fails to compile if the type does not match:

```csharp
public sealed class OrderViewModel : IInitializeAsync<Order>
{
    public Task InitializeAsync(Order data, CancellationToken ct = default) { /* ... */ return Task.CompletedTask; }
}

await router.GoToRouteAsync<OrderViewModel, Order>("/orders/42", "/orders/{id:int}", order);
```

Prefer constructor parameters for required data (the object is valid as soon as it exists, and the generator checks them at compile time). Use `IInitializeAsync` for work that must be asynchronous.
