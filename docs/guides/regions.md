# Regions

## Regions

Use regions when the UI needs several independent routing stacks:

```csharp
services.AddRegion("main");
services.AddRegion("side");
```

Each region is a keyed `IRouter` with its own history, gate and lifecycle. Resolve one with the keyed-services API, or with `GetRegion`:

```csharp
var side = provider.GetRequiredKeyedService<IRouter>("side");
```

A route can target a region from its attributes: `[Routeable("/inbox", region: "side")]` or `[RouteableView(typeof(InboxViewModel), region: "side")]`. A region is registered `Scoped` by default.

### Receiving a region: `RouteableRegion`

A region has no place on screen of its own. A view puts one down with a **region host**, a zone that receives whatever the router of that region displays. Nothing assigns the view to it: not a view model property, not code-behind.

```xml
<!-- Avalonia: xmlns:routeables="using:Routeables.Avalonia" · Maui: xmlns:routeables="clr-namespace:Routeables.Maui;assembly=Routeables.Maui" -->
<routeables:RouteableRegion Region="detail" />
```

```razor
<RouteableRegion Region="detail" />
```

A view can hold several hosts, each with its own name, and without a name a host shows the default router. Whoever routes only names the region; the host that carries that name shows the result:

```csharp
services.AddRegion("list");
services.AddRegion("detail");

// An entry says which region shows it (null: the default router)...
var entry = new RouteableEntry<InboxViewModel>("Inbox", icon, route: "/inbox") { Region = "detail" };

// ...and the provider sends it there. No view model needs to know where "detail" is on screen.
await provider.GoToAsync(entry);
```

A page shown in a region routes that region. The router that builds a view model hands itself to it: a view model whose constructor takes an `IRouter` receives the router of its region, not the default one (`IRouteableArgs.Router`; a router given with `ForViewModel(router)` wins). A list that opens its detail with `_router.GoToRouteAsync(...)` therefore opens it in its own region, and its `Back` or `Pop` returns to the list there.

`provider.RouterFor(entry)` and `provider.GetRouter(region)` give the router itself (`Back`, `Forward`, `Pop`, `History`...). The host follows every change of its history, `Back`, `Forward`, `EraseHistoryAsync` included, shows the view the router already holds when it is created, and lets go of the router when it leaves the screen. Avalonia and Maui hosts find the router in the running host (`CreateRouteableApp`, `UseRouteables`); the `Services` property points a host to another provider. `RegionBinding` is the same behavior without UI, to build a host for another framework.

### Routing with a result

A page the user comes back from (a picker, a form, a confirmation) gives a value to whoever opened it. The caller waits; the page
implements `IReturnsResult<T>`; the router needs nothing more.

```csharp
// The caller
var picked = await router.GoToForResultAsync<Color>("/colors");     // or GoToForResultAsync<ColorsViewModel, Color>(route)
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
var follower = new RegionFollower(provider.GetRouter("content"), async () =>
    await provider.GoToAsync(Bar("header", provider.GetRouter("content").History.Current)));

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
public sealed class Shell(IServiceProvider provider) : RegionLayout(provider, "content", "header", "menu")
{
    protected override async Task OnStartAsync(CancellationToken ct)
    {
        await GoToAsync("menu", MenuEntry, ct);
        await ShowAsync(FirstPage, ct);
    }

    // After each move of the content; FollowAsync does nothing when the header would show the same thing.
    protected override Task OnPrimaryChangedAsync()
        => FollowAsync("header", PrimaryRouter.History.Current, () => HeaderFor(PrimaryRouter.History.Current));
}

services.AddRegionLayout<Shell>("content", "header", "menu");   // the regions and the layout, scoped
```

`StartAsync`, `ClearAsync`, `ShowAsync`, `BackAsync`, `ForwardAsync`, `CanGoBack`, `CanGoForward`, `SettledAsync` and `Changed` are
there already, and the followers run one after the other on the thread that started the layout. Set `Following = false` while
the followers show something else (a credits page), back to `true` to follow again.
