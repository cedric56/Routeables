# Recipes

Each recipe is one task, short, with the code that does it. The guide it links to explains why. The examples use Avalonia; the view models are the same on every host. A word you do not know is in the [glossary](glossary.md).

- [Protect a page behind a sign-in](#protect-a-page-behind-a-sign-in)
- [Master and detail with a region](#master-and-detail-with-a-region)
- [Pass an object to the next page](#pass-an-object-to-the-next-page)
- [Ask before leaving a page with unsaved changes](#ask-before-leaving-a-page-with-unsaved-changes)
- [Tabs that each keep their own history](#tabs-that-each-keep-their-own-history)

## Protect a page behind a sign-in

A guard is asked before every route change. This one sends a visitor who is not signed in to `/sign-in`:

```csharp
public sealed class SignedInGuard(ISession session) : IRouteGuard
{
    public Task<RouteGuardDecision> CanEnterAsync(RouteableRoutingEventArgs change, CancellationToken ct = default)
        => Task.FromResult(change.Route is "/account" && !session.IsSignedIn
            ? RouteGuardDecision.Redirect("/sign-in")
            : RouteGuardDecision.Allow);
}

services.AddSingleton<IRouteGuard, SignedInGuard>();
```

- `ISession` is your own service. The guard is built by the container, so it can ask for any service.
- The guard runs after the page is built: keep constructors free of anything that needs the right to see the page, and load data in `InitializeAsync`.
- To bring the user back to the page they wanted after the sign-in, use `AddRouteableReturn()`.

More: [Back, deep links, guards and diagnostics](system-integration.md).

## Master and detail with a region

The list is the page; the detail appears in a region next to it. Register the region once:

```csharp
services.AddRegion("detail");
```

Put the zone in the view of the list, next to the list:

```xml
<routeables:RouteableRegion Region="detail" />
```

Open an item in that zone from the list's view model, which asks for an `IServiceProvider`:

```csharp
await provider.GoToAsync($"/dogs/{Uri.EscapeDataString(dog.Name)}", region: "detail");
```

The list stays where it is, and Back inside the zone returns to the previous detail, not to the list. A `[Routeable("/dogs/{name}", region: "detail")]` on the detail view model makes the region part of its route.

More: [Regions](regions.md).

## Pass an object to the next page

A value in the address is text. To give the next page an object, put it in the arguments:

```csharp
await router.GoToAsync($"/dogs/{Uri.EscapeDataString(dog.Name)}",
    RouteableArgs.Create().ForViewModel(dog));
```

`ForViewModel` matches the object to the constructor parameter of the same type, so the view model receives it:

```csharp
[Routeable("/dogs/{name}")]
public sealed class DogDetailViewModel(string name, Dog dog)
{
    public string Name { get; } = name;
    public Dog Dog { get; } = dog;
}
```

The page is not rebuilt from the object alone: if the user comes back through the history or a deep link, the address is all there is. Keep the address enough to find the object again (here, the name), and treat the object as a shortcut.

More: [Passing arguments](arguments.md).

## Ask before leaving a page with unsaved changes

`CanLeaveAsync` runs on the page that is being left, before anything changes. Return `false` to stay:

```csharp
[Routeable("/edit/{id:int}")]
public sealed class EditViewModel(int id, IRouteableDialogService dialogs) : IRouteable
{
    public int Id { get; } = id;
    public bool HasUnsavedChanges { get; private set; }

    public async Task<bool> CanLeaveAsync(CancellationToken ct = default)
        => !HasUnsavedChanges
           || await dialogs.ShowYesNoAsync("Leave without saving?", "Unsaved changes");
}
```

`IRouteable` is the interface of the lifecycle; a view that derives from `RouteableFor<T>` overrides `CanLeaveAsync` the same way. The check applies to Back, to a new page and to a link from outside.

More: [Lifecycle, guards and events](lifecycle.md).

## Tabs that each keep their own history

Give each tab its own region and each tab keeps its pages when the user goes to another tab and comes back:

```csharp
services.AddRegion("cats");
services.AddRegion("dogs");
```

```xml
<TabControl x:Name="Tabs">
    <TabItem Header="Cats" routeables:RouteableItem.Route="/cats" routeables:RouteableItem.Region="cats" />
    <TabItem Header="Dogs" routeables:RouteableItem.Route="/dogs" routeables:RouteableItem.Region="dogs" />
</TabControl>
```

The `TabControl` needs `AttachRouteables(provider)` (WPF, Avalonia). On MAUI a `Tab` whose route is the name of a registered region does it with nothing to write. Pages opened inside a tab go to its region: `provider.GoToAsync("/cats/Rex", region: "cats")`.

More: [Regions](regions.md#a-pile-of-pages-for-each-tab).
