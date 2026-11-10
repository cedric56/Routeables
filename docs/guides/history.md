# History

## History

Each router keeps a history of entries (view model, view and arguments):

| Operation | Effect |
| --- | --- |
| `GoToAsync` (or `GoToRouteAsync`) | Pushes a new entry. Forward entries are cut off. The history has no limit by default (a scoped router keeps 50): it grows until entries are popped or erased (see below to set one) |
| `GoBackAsync` / `ForwardAsync` | Moves the cursor. Nothing is removed, so a page you went back from stays alive for `Forward` |
| `MoveToAsync(index)` | Moves the cursor to an entry |
| `GoBackToAsync("/orders")`, `GoBackToAsync<TViewModel>()`, `GoBackToAsync(entry => ...)` | Moves the cursor to the nearest earlier entry that matches (route compared without the leading `/`, the query and the case); cancelled when none does. Later entries stay, as with `GoBackAsync` |
| `GoToAsync("-")`, `GoToAsync("-/orders")` | The qualifier `-` goes back one page; `-/orders` goes back, then to `/orders`. It makes a back button declarative on the XAML hosts: `<Button rt:RouteableLink.Route="-" />` |
| `GoBackAsync(steps)` | Moves the cursor back several entries in one move (one lifecycle change); cancelled when the history is shorter |
| `GoToStackAsync(["/orders", "/orders/42"])` | Goes to each route in order, so the history gets one entry per route (deep link, wizard). Each page runs its guards and lifecycle, the intermediate pages included; stops at the first route that fails or is cancelled and returns that result |
| `PopAsync` | Goes back and removes the entry you left |
| `GoBackToRootAsync` | Returns to the first entry |
| `EraseHistoryAsync` | Clears the history |

### Limiting the history

By default a history keeps every entry, except the one of a scoped router (Blazor Server, ASP.NET Core: one history per user session), which keeps the last 50. In an application that stays open for a long time, set a limit at startup (it also replaces the 50 of a scoped router):

```csharp
services.LimitHistory(50);                    // the default router
services.LimitHistory(20, region: "detail");  // the history of one region
```

When a push goes beyond the limit, the oldest entry is dropped and destroyed like a popped one (`OnUnsubscribe`, `Dispose`). The limit is at least 1, can be set before or after `RegisterRouting` / `AddRegion`, and is visible as `router.History.MaxSize` (`null` when there is no limit). A page you left with `GoBackAsync` is not affected until a push needs the room.

Which one to call: `GoBackAsync` for history you can come back to (a back button next to a forward one), `PopAsync` to leave a page for good (the detail opened from a list, a wizard step), which is what the samples do. Both answer `CanGoBack`.

`router.CanGoBack`, `router.CanGoForward`, `router.CurrentViewModel`, `router.CurrentView` and `router.History` describe the current state. `History.Entries` are read-only `IHistoryEntry` values: the `ViewModel`, the `View` and the `Args` of each row. Operations are serialized by a gate, so two route changes never interleave.
