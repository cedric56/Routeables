# Regions

## Regions

Use regions when the UI needs several independent routing stacks:

```csharp
// Samples/Bases/XAnimals.ViewModels/Extensions.cs
services.AddRegion(AdoptViewModel.Region);      // "adopt": the adoption zone of the Adopt a pet page
services.AddRegion(TabsViewModel.Region);       // the tabs of the Tabs page
```

Each region is a keyed `IRouter` with its own history, gate and lifecycle. Resolve one with `GetRouter`, or with the keyed-services API:

```csharp
var adopt = provider.GetRouter(AdoptViewModel.Region);          // or provider.GetRequiredKeyedService<IRouter>("adopt")
```

A route can target a region from its attributes: `[Routeable("/pet", region: "adopt")]` or `[RouteableView(typeof(PetViewModel), region: "adopt")]`. A region is registered `Scoped` by default.

### Receiving a region: `RouteableRegion`

A region has no place on screen of its own. A view puts one down with a **region host**, a zone that receives whatever the router of that region displays. Nothing assigns the view to it: not a view model property, not code-behind.

```xml
<!-- Avalonia: xmlns:routeables="using:Routeables.Avalonia" · Maui: xmlns:routeables="clr-namespace:Routeables.Maui;assembly=Routeables.Maui" -->
<!-- Samples/XAnimals.Avalonia/Views/RegionsView.axaml -->
<routeables:RouteableRegion Region="shell-content" />
```

```razor
<RouteableRegion Region="shell-content" />
```

A view can hold several hosts, each with its own name, and without a name a host shows the default router. Whoever routes only names the region; the host that carries that name shows the result:

A view has one parent, so a view of a region is shown by one host at a time. A host that has left the screen lets go of its view (on Avalonia, the next host that shows the region takes it back); two hosts that are on screen at once for the same region, or a host and a `ContentControl` bound to the same view, are refused with a message that names who holds the view.

A region is one router for as long as its scope lives, which is the whole application on a desktop. View models that follow one another in the same region share its history: to start from an empty one, call `provider.EraseRegionsAsync(["detail"])` before opening the next.

```csharp
// AdoptViewModel.cs: an entry says which region shows it (null: the default router)...
var entry = new RouteableEntry<PetViewModel>("Pet", null, route: "pet", template: "pet")
{
    Region = "adopt",
    Arguments = RouteableArgs.Create().ForViewModel(pet)
};

// ...and the provider sends it there. No view model needs to know where "adopt" is on screen.
await provider.GoToAsync(entry);
```

A page shown in a region routes that region. The router that builds a view model hands itself to it: a view model whose constructor takes an `IRouter` receives the router of its region, not the default one (`IRouteableArgs.Router`; a router given with `ForViewModel(router)` wins). A list that opens its detail with `_router.GoToRouteAsync(...)` therefore opens it in its own region, and its `Back` or `Pop` returns to the list there.

`provider.RouterFor(entry)` and `provider.GetRouter(region)` give the router itself (`Back`, `Forward`, `Pop`, `History`...). The host follows every change of its history, `Back`, `Forward`, `EraseHistoryAsync` included, shows the view the router already holds when it is created, and lets go of the router when it leaves the screen. Avalonia and Maui hosts find the router in the running host (`UseRouteables`); the `Services` property points a host to another provider. `RegionBinding` is the same behavior without UI, to build a host for another framework.

### Showing every view of a region: `RouteableRegionItems`

`RouteableRegion` shows the current view of a region. When a zone must show **all** the views the region holds (as cards, as a
strip, as a grid), use `RouteableRegionItems` (Avalonia, MAUI, Uno, WPF and Blazor): it lays out the history of the router, oldest first, up to the
current view. Routing the region adds a view, `PopAsync` and `BackAsync` remove the last one, and the views a `ForwardAsync` could reach are
not shown.

```xml
<!-- Samples/XAnimals.Avalonia/Views/AdoptView.axaml -->
<routeables:RouteableRegionItems Region="adopt">
    <routeables:RouteableRegionItems.ItemsPanel>
        <ItemsPanelTemplate><WrapPanel /></ItemsPanelTemplate>
    </routeables:RouteableRegionItems.ItemsPanel>
</routeables:RouteableRegionItems>
```

On Avalonia and WPF it is an `ItemsControl`: its panel decides the layout. On MAUI, Uno and Blazor a view is `MinItemWidth` wide (200 by default): a row holds as many as fit and the others go to the row
below, and the zone scrolls when the rows do not fit. A region has one history, so there is no type of region to choose and no adapter:
the host decides how many views it shows. `RegionItemsBinding` is the same behavior without UI, to build a host for another
framework. The *Adopt a pet* page of the samples shows it: click an animal and its card appears in the adoption zone, next to the pets already adopted.

### Routing with a result

A page the user comes back from (a picker, a form, a confirmation) gives a value to whoever opened it. The caller waits; the page
implements `IReturnsResult<T>`; the router needs nothing more.

```csharp
// No sample page returns a result: this one is the shape. (The modals of the samples, NoteViewModel, use ICloseableViewModel.)
// The caller
var picked = await router.GoToForResultAsync<Color>("/colors");
if (picked.Returned) Apply(picked.Value);                           // picked.OpenResult says whether the page opened

// The page
public sealed class ColorsViewModel(IRouter router) : IReturnsResult<Color>
{
    private Color? _picked;
    public Task PickAsync(Color color) { _picked = color; return router.PopAsync(); }
    public bool TryGetResult(out Color result) { result = _picked.GetValueOrDefault(); return _picked is not null; }
}
```

The call completes when the page is *left for good* (`PopAsync`, `BackAsync`, the history cleared). A page that opens another one on
top of itself is not left yet. `Returned` is `false` when the page was left without a value; the wait can be cancelled with its
token (`Cancelled`; the page stays). It works on the router of a region too.

### Following a region: `RegionFollower`

`RegionFollower`, `RegionLayout` and `RegionBinding` are in `Routeables.Regions` (namespace `Routeables.Regions`); the regions themselves (`AddRegion`, `GetRouter`) are part of routing.

The history of a router changes while the router holds its gate, so a handler of `History.Changed` must not route that
router. To make other regions follow one (a header that says what the content shows, a menu that selects its entry), create a
`RegionFollower` on the source: each change queues an update, the updates run one after the other, never on the route change
that caused them, and on the thread that created the follower (the UI thread when a view started the page).

```csharp
// What RegionLayout does for you in LayoutController.cs of the sample, written by hand:
var follower = new RegionFollower(provider.GetRouter(LayoutController.Content), async () =>
    await provider.GoToAsync(Bar(LayoutController.Header, provider.GetRouter(LayoutController.Content).History.Current)));

await follower.SettledAsync();   // the header says what the content shows
follower.Failed += (_, ex) => log(ex);
follower.Dispose();              // stops following
```

`provider.EraseRegionsAsync(["header", "content", "footer"])` empties a group of regions at once.

### A layout of regions: `RegionLayout`

A header, a footer, a menu on the side and a content is a common layout. Derive from `RegionLayout`, name the region the user
routes (the *primary*) and the ones that follow it, and write only what is yours: what the regions start with and what the
followers show when the primary moves.

```csharp
// LayoutController.cs of the sample (the Regions page)
public sealed class LayoutController(IServiceProvider provider) : RegionLayout(provider, Content, Header, Footer, LeftPanel)
{
    protected override async Task OnStartAsync(CancellationToken ct)
    {
        await GoToAsync(LeftPanel, RegionEntries.Menu(LeftPanel), ct);
        await ShowAsync(RegionEntries.Page(Pages[0], Content), ct);
    }
    // ...
}

// Extensions.cs: the regions and the layout, scoped
services.AddRegionLayout<LayoutController>(LayoutController.Content, LayoutController.Header, LayoutController.Footer, LayoutController.LeftPanel);
```

`StartAsync`, `ClearAsync`, `ShowAsync`, `BackAsync`, `ForwardAsync`, `CanGoBack`, `CanGoForward`, `SettledAsync` and `Changed` are
there already, and the followers run one after the other on the thread that started the layout. Set `Following = false` while
the followers show something else (a credits page), back to `true` to follow again.


### Choosing the region in the arguments

Give the region to the arguments, and let the provider pick the router of that region:

```csharp
await provider.GoToAsync("/form", RouteableArgs.Create().WithRegion("Form").WithViewModel("id", 42));
```

`provider.GoToAsync(url, region: "Form")` does the same. A router obtained by hand (`router.GoToAsync`) is already one region's router: it ignores `Region`.
