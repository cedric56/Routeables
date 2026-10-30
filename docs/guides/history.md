# History

## History

Each router keeps a history of entries (view model, view and arguments):

| Operation | Effect |
| --- | --- |
| `GoToAsync` (or `GoToRouteAsync`) | Pushes a new entry. Forward entries are cut off. The history has no limit by default: it grows until entries are popped or erased (see below to set one) |
| `GoBackAsync` / `ForwardAsync` | Moves the cursor. Nothing is removed, so a page you went back from stays alive for `Forward` |
| `MoveToAsync(index)` | Moves the cursor to an entry |
| `PopAsync` | Goes back and removes the entry you left |
| `GoBackToRootAsync` | Returns to the first entry |
| `EraseHistoryAsync` | Clears the history |

### Limiting the history

By default a history keeps every entry. In an application that stays open for a long time, set a limit at startup:

```csharp
services.LimitHistory(50);                    // the default router
services.LimitHistory(20, region: "detail");  // the history of one region
```

When a push goes beyond the limit, the oldest entry is dropped and destroyed like a popped one (`OnUnsubscribe`, `Dispose`). The limit is at least 1, can be set before or after `RegisterRouting` / `AddRegion`, and is visible as `router.History.MaxSize` (`null` when there is no limit). A page you left with `GoBackAsync` is not affected until a push needs the room.

Which one to call: `GoBackAsync` for history you can come back to (a back button next to a forward one), `PopAsync` to leave a page for good (the detail opened from a list, a wizard step), which is what the samples do. Both answer `CanGoBack`.

`router.CanGoBack`, `router.CanGoForward`, `router.CurrentViewModel`, `router.CurrentView` and `router.History` describe the current state. `History.Entries` are read-only `IHistoryEntry` values: the `ViewModel`, the `View` and the `Args` of each row. Operations are serialized by a gate, so two route changes never interleave.
