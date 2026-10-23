# Typed navigation methods

## Typed navigation methods (source generator)

Writing route strings and argument lists by hand is easy to get wrong. `[RouteableViewModel]` generates a strongly typed method instead.

```csharp
[Routeable("/orders/{id:int}")]
[RouteableViewModel(typeof(HomeViewModel))]
public sealed class OrderViewModel(IRouter router, int id, string? note = null) { }
```

generates, **on `HomeViewModel` only**:

```csharp
Task<RouteableResult> GoToOrderAsync(
    this HomeViewModel caller,
    IRouter router,
    int id,
    string? note = null,
    CancellationToken cancellationToken = default);
```

which you call like this:

```csharp
public sealed class HomeViewModel(IRouter router)
{
    public Task OpenAsync() => this.GoToOrderAsync(router, id: 42, note: "urgent");
}
```

How it works:

- **Scoped to the caller.** The types given to the attribute are the only ones that see the method. Another type that calls it gets a compiler error. This keeps `IRouter` free of one method per destination.
- **`IRouter` stays an explicit parameter**, because it is shared with the caller (same instance, same history).
- **Parameters mirror the destination's constructor.** The constructor is chosen like `ActivatorUtilities` does: the one marked `[ActivatorUtilitiesConstructor]`, otherwise the public one with the most parameters.
- **Placeholders are filled from matching parameters.** `id` above is written into `/orders/{id}` (invariant culture, URL-escaped). Every other parameter is passed to the view model as a constructor argument.
- **Required parameters are checked.** Required reference-type parameters get a `ThrowIfNull`, and `?` is dropped from their type in the signature. Parameters with a default value keep it and are skipped when `null` or default.
- **Services come from the container.** Mark a constructor parameter with `[FromContainer]` (or `[FromServices]` / `[FromKeyedServices]`) and it is resolved by DI instead of being asked from the caller.
- **Several callers.** `[RouteableViewModel(typeof(A), typeof(B))]` generates the method on both, and the attribute can be repeated.

The attribute accepts three optional named arguments:

| Argument | Default | Purpose |
| --- | --- | --- |
| `Route` | The path of the single `[Routeable]` on the class | The route string navigated to. Required when the class has several `[Routeable]` |
| `Template` | `Route` | The template handed to the router to match `Route` against |
| `MethodName` | `GoTo{Name}Async`, where `{Name}` is the class name without its `ViewModel` suffix | Name of the generated method |

```csharp
[RouteableViewModel(typeof(AnimalsViewModel), Route = "/monkeys", MethodName = "OpenMonkey")]
public class MonkeyDetailViewModel(IRouter router, Animal monkey, Action back) { }
```

Constructor arguments are matched **by type, in order** when the view model is created, so avoid two constructor parameters of the same type that can be skipped. The generator warns about this case (`ROUT106`).

### Reference the generator from the project with the view models

The generator runs per project. Add it to the project that **contains the view models** and callers, not only to the application project:

```xml
<!-- XAnimals.ViewModels.csproj -->
<ItemGroup>
  <ProjectReference Include="..\..\Routeables.Generated\Routeables.Generated.csproj"
                    OutputItemType="Analyzer" ReferenceOutputAssembly="false" />
</ItemGroup>
```

The generated methods are `internal`, which is why the caller and the destination live in the same project.

### Seeing the generated code

```xml
<PropertyGroup>
  <EmitCompilerGeneratedFiles>true</EmitCompilerGeneratedFiles>
  <CompilerGeneratedFilesOutputPath>gen</CompilerGeneratedFilesOutputPath>
</PropertyGroup>
<ItemGroup>
  <Compile Remove="gen/**" />
</ItemGroup>
```

The `Compile Remove` line matters: without it the generated files are compiled twice and you get duplicate-definition errors.
