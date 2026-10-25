# Passing arguments

## Passing arguments

Routing arguments travel in an `IRouteableArgs`. Each entry goes to the view model, to the view, or to neither (a shared bag of values):

```csharp
// DogsViewModel.cs of the XAnimals sample
var arguments = RouteableArgs.Create()
    .ForViewModel(back)                                        // matched to a constructor parameter by type
    .WithRoute($"/dogs/{Uri.EscapeDataString(animal.Name)}");  // the address asked for

// CatsViewModel.cs: named values, available to the lifecycle hooks, not injected
await router.GoToAsync($"/cats/{Uri.EscapeDataString(animal.Name)}",
    RouteableArgs.Create().WithValue(CatDetailViewModel.AnimalKey, animal).WithValue(CatDetailViewModel.BackKey, back));
```

The route is found from the address. When the address alone does not say which route you mean, give its template: `router.GoToAsync("/dogs/Eurohound", args, template: "/dogs/{name}")`. `DogsViewModel` does it with a `RouteableEntry<DogDetailViewModel>` whose `route` and `template` are `/dogs/{name}`.

To name the view model yourself, give it as the type argument: `router.GoToRouteAsync<BearDetailViewModel>("/bears/Brown%20Bear")` goes to the view registered under that route and builds a `BearDetailViewModel` for it. It takes the same `args` and `template`.

`ForViewModel`, `ForView` and `ForValues` take plain values (several at once: `ForViewModel(animal, back)`); `WithViewModel`, `WithView` and `WithValue` take a name and a value. In a hook, read the values by name: `args.Get<Action>("back")` (or `args.Get<Animal>()` when only one value has that type; two of the same type throw instead of silently taking the first).

`new RouteableArgument(value, name, attribute)` is still there for the rare argument that needs the `attribute` flag in a list.

### Reading a template

| Written | Meaning |
|---|---|
| `/dogs/{name}` | `name` is given to a constructor parameter named `name` (`DogDetailViewModel`) |
| `/orders/{id:int}` | `id` is given to a constructor parameter named `id`, and the route only matches an integer (syntax: no sample route is typed) |
| `/pandas/{panda::}` | `panda` is **not** given to a constructor: it is set on a property that has a platform attribute (below) |

### Parameters by attribute

A template parameter written `{name::}` (two colons) is not given to a constructor: it is set on a property that has a platform attribute. The property can be on the view model or on the view; a value is given to the view only when it has a property of that name.

| Platform | Attribute | Where |
|---|---|---|
| Blazor | `[Parameter]` | a property of the component: `[Parameter] public string? Panda { get; set; }` |
| MAUI | `[QueryProperty(nameof(Panda), "panda")]` | on the class, naming the property (or `[RouteableParameter]` on the property) |
| Avalonia | `[RouteableParameter]` | on the property |

```csharp
// PandaDetailViewModel.cs: "/pandas/{panda::}" opens PandaDetailView, and "Red Panda" ends up in its Panda property.
[Routeable(PandaDetailViewModel.Route)]   // Route = "/pandas/{panda::}"
public class PandaDetailViewModel(IRouter router, Action? back = null) : AnimalDetailViewModel(router, null, back) { /* Name */ }

// PandaDetailView.axaml.cs (Avalonia)
[RouteableParameter]
public string? Panda { get; set; }
```

The view is created before its view model is set, so a view hands the value over when both exist (`PandaDetailView` in the samples).

View-model entries are matched to constructor parameters **by type**, in order. Registered services fill the remaining parameters. Entries with a `null` value are dropped.

For a single typed value delivered after construction, implement `IInitializeAsync<TData>` on the view model and use the typed overload, which fails to compile if the type does not match:

```csharp
// BearDetailViewModel.cs
[Routeable("/bears/{name}")]
public class BearDetailViewModel(IRouter router, string? name = null)
    : AnimalDetailViewModel(router, BearData.Bears.FirstOrDefault(b => b.Name == name)), IInitializeAsync<AnimalSelection>
{
    public Task InitializeAsync(AnimalSelection data, CancellationToken ct = default)
    {
        Animal = data.Animal;
        Back = data.Back;
        return Task.CompletedTask;
    }
}

// BearsViewModel.cs
await _router.GoToRouteAsync<BearDetailViewModel, AnimalSelection>($"/bears/{Uri.EscapeDataString(animal.Name)}", (animal, back));
```

Prefer constructor parameters for required data (the object is valid as soon as it exists, and the generator checks them at compile time). Use `IInitializeAsync` for work that must be asynchronous.
