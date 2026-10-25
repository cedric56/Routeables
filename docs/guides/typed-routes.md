# Typed routing methods

## Typed routing methods (source generator)

Writing route strings and argument lists by hand is easy to get wrong. `[RouteableViewModel]` generates a strongly typed method instead.

From the XAnimals sample (`Samples/Bases/XAnimals.ViewModels/ViewModels/Animals/MonkeyDetailViewModel.cs`):

```csharp
[RouteableViewModel(typeof(MonkeysViewModel), Route = "/monkeys/{name}")]
[Routeable("/monkeys/{name}")]
public class MonkeyDetailViewModel(IRouter router, string name, Action? back = null)
    : AnimalDetailViewModel(router, MonkeyData.Monkeys.FirstOrDefault(d => d.Name == name), back);
```

generates, **on `MonkeysViewModel` only**:

```csharp
Task<RouteableResult> GoToMonkeyDetailAsync(
    this MonkeysViewModel caller,
    IRouter router,
    string name,
    Action? back = null,
    CancellationToken cancellationToken = default);
```

which `MonkeysViewModel` (`Animals/MonkeysViewModel.cs`) calls like this:

```csharp
[Routeable("/monkeys")]
public class MonkeysViewModel(IRouter router) : AnimalsViewModel(router, MonkeyData.Monkeys)
{
    protected override Task<RouteableResult> GoToDetailAsync(Animal animal, Action back)
        => this.GoToMonkeyDetailAsync(_router, animal.Name, back);
}
```

How it works:

- **Scoped to the caller.** The types given to the attribute are the only ones that see the method. Another type that calls it gets a compiler error. This keeps `IRouter` free of one method per destination.
- **`IRouter` stays an explicit parameter**, because it is shared with the caller (same instance, same history).
- **Parameters mirror the destination's constructor.** The constructor is chosen like `ActivatorUtilities` does: the one marked `[ActivatorUtilitiesConstructor]`, otherwise the public one with the most parameters.
- **Placeholders are filled from matching parameters.** `name` above is written into `/monkeys/{name}` (invariant culture, URL-escaped). Every other parameter is passed to the view model as a constructor argument.
- **Required parameters are checked.** Required reference-type parameters get a `ThrowIfNull`, and `?` is dropped from their type in the signature. Parameters with a default value keep it and are skipped when `null` or default.
- **Services come from the container.** Mark a constructor parameter with `[FromContainer]` (or `[FromServices]` / `[FromKeyedServices]`) and it is resolved by DI instead of being asked from the caller.
- **Several callers.** `[RouteableViewModel(typeof(A), typeof(B))]` generates the method on both, and the attribute can be repeated.

The attribute accepts three optional named arguments:

| Argument | Default | Purpose |
| --- | --- | --- |
| `Route` | The path of the single `[Routeable]` on the class | The route string routed to. Required when the class has several `[Routeable]` |
| `Template` | `Route` | The template handed to the router to match `Route` against |
| `MethodName` | `GoTo{Name}Async`, where `{Name}` is the class name without its `ViewModel` suffix | Name of the generated method |

```csharp
// ElephantDetailViewModel.cs generates GoToElephantDetailAsync; MethodName would name it otherwise:
[RouteableViewModel(typeof(ElephantsViewModel), Route = "/elephants/{name}", MethodName = "OpenElephant")]
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
