# Changelog

All notable changes to the Routeables packages. The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and the versions follow [VERSIONING.md](VERSIONING.md). Every package of the repository has the same version.

## Unreleased

### Changed

- **The three-button dialog is in `Routeables.Native`** (`NativeDialogs.DisplayThreeButtonsAsync`): MAUI, WinUI 3 and Uno use it (Android alert, Windows App SDK `ContentDialog`, iOS alert or sheet) and draw their own where it has nothing to show.

## 2.1.0 - 2026-11-11

### Added

- **Your own container** (DryIoc, Autofac, Lamar...): `services.UseRouteablesContainer(factory)` on WPF, Avalonia, Uno and WinUI 3; `ConfigureContainer` on MAUI and `UseServiceProviderFactory` on ASP.NET Core / Blazor Server are the existing hooks (see the guide on hosts).
- **`[Persist]`** on a view-model property: the value is saved with the history snapshots (`RouteableState.CaptureSnapshots`, the Blazor `PersistentComponentState`, `RouteableLinkInbox`) and read back after the process was killed. A source generator writes the code (no reflection, trim and Native AOT safe). Built-in formats for text, numbers, `bool`, `char`, `Guid`, enums, dates and their `Nullable` forms.
- **Your choice of format** for the other types: `IPersistConverter<T>`, given to one property (`[Persist(Converter = typeof(X))]`) or to every property of a type (`RouteablePersistence.RegisterConverter<T>`). `RouteablePersistence.RegisterJson` is one ready-made converter (System.Text.Json source generation). `RouteablePersistence.MaxValueLength` bounds one value (64 KB by default).
- Diagnostic **`ROUT401`**: a `[Persist]` property that cannot be saved is skipped and explained.
- **Several pages at once** (extensions of `IRouter`): `GoToStackAsync(routes)` rebuilds a stack (deep link, wizard), `GoBackToAsync(route | TViewModel | predicate)` goes back to the nearest earlier page, `GoBackAsync(steps)` goes back several pages in one move.
- **Route qualifiers**: `GoToAsync("-")` goes back one page and `GoToAsync("-/orders")` goes back, then to `/orders`; with `RouteableLink.Route="-"` a back button needs no code.
- **Application lifecycle**: a view model that implements `IRouteableAppLifecycle` is told `OnSleepAsync` / `OnResumeAsync`; `RouteableAppLifecycle` raises them for the shown page of the main router and of each region, and saves the history after a sleep (so `[Persist]` values written at sleep are kept). Raised by the host when a window closes (WPF, Avalonia) and by `Window.UseRouteablesLifecycle(services)` on MAUI; call `SleepAsync()` / `ResumeAsync()` yourself on the other hosts.
- **A page for routes that do not exist**: `services.UseNotFoundRoute("/not-found")`; the address asked for is in the value `notFoundUrl`.
- **Guards run again for the page shown**: `router.RefreshGuardsAsync()` and `router.RefreshGuardsOn(source, provider, properties)` (a sign-out redirects to the login page).
- **A pile of pages for each tab** (WPF, Avalonia, WinUI 3, Uno): `RouteableItem.Region` gives a tab (or a `NavigationViewItem`) a region of its own, which keeps its pages when the user goes to another tab and comes back. On .NET MAUI a `Shell` tab whose route is the name of a registered region already works that way.
- **Closing the tab with unsaved changes** (Blazor): a page that implements `IRouteableUnsavedChanges` makes the browser confirm before the tab is closed or reloaded while `HasUnsavedChanges` is true.
- Diagnostic **`ROUT501`** (information): a `GoToAsync("/route")` with a literal that matches no known route, caught at build time instead of when the line runs.
- ASP.NET Core: a click or a close on a browser notification is reported to the server (`NotificationCompleted`).
- Metric `routeables.guard.duration`, and an option to include the address in the route diagnostics.
- `dotnet new` templates for Blazor, .NET MAUI, Uno Platform (desktop head) and WinUI 3 (`routeables-blazor`, `routeables-maui`, `routeables-uno`, `routeables-winui3`).

- .NET MAUI: a `Shell` tab with a region of its own pushed its route again each time it was selected, on top of the pages opened in it. It now goes to its route only while its region has no page.

- `RouteableReturn` took one instance for the whole application, which every visitor of an ASP.NET Core or Blazor host shared. It now has the lifetime of the routers of the host (one for each visitor there). Call `AddRouteableReturn()` after the registration of the host.
- Modules loaded while running: a service that asks for `IServiceProvider` gets the provider of the module (it found nothing of the module before), `ActivatorUtilities` sees the services of the module, and a service that fails to dispose no longer keeps the others alive.
- `ScopedViewCache`, `RegionLayout.Changed`, `RouteableFormLock`: a view that fails to dispose, a listener that throws, or a subscriber that throws on lock no longer stops the rest (a form could stay locked for ever).
- `RouteableHistoryKeeper` could start saving again after it was disposed; `RouteableLinkInbox` polled again every 30 seconds when no page ever came.
- `RegionTabs` ran its posted actions after `Dispose()`; the dialog queue of Blazor left a dialog asked at the moment of the disposal waiting for ever; the dialog callbacks of ASP.NET Core are guarded like the other hosts'.
- Blazor: an address that is an entry of the menu is accepted only once its guards have answered; the cancellation of a navigation reaches the router; the values of a query string are compared exactly (`?q=ABC` is not `?q=abc`); the script of the unload warning is let go of when the circuit ends while it loads.
- `RouteParser.Build`: the value of a `{**catch-all:type}` is checked against its constraint, like the other placeholders.
- iOS: the delegate of the notification center is kept in a field (the center holds it weakly).
- Tests: the four `xUnit1051` warnings (a cancellation token was not passed).

- .NET MAUI (Windows): a content dialog closed without a button (Escape) returned `default` instead of the close result, as Android does.

- .NET MAUI (iOS, Mac Catalyst): a three-button dialog whose content is neither text nor a `UIView` (a MAUI view) is shown by the content dialog service instead of throwing `NotImplementedException`.

- A value a view was given (`{tab::}` style attribute entry) is no longer overwritten by a view model value whose name differs only by case. Blazor: `?x=1&y=2` and `?y=2&x=1` are the same address.

- .NET MAUI: an iOS alert with no button text could not be closed (the caller waited for ever): it gets an OK button; on Android a missing current activity says so instead of failing with a null reference.

- Tests: the screenshot tests (Avalonia, WPF, Uno, MAUI, Blazor/Playwright) are removed with their CI job and artifacts: no reference image was ever kept, so they were all skipped.

- .NET MAUI: the default button of a modal dialog is the first one shown even when the first command has no name; a modal page refused as it opens frees its caller. Blazor: a browser notification that shares its id with a newer one no longer removes the newer one.

- Restoring the history (`RouteableState.RestoreAsync`, `RestoreSnapshotsAsync`, `RouteableHistoryKeeper`, `RouteableLinkInbox`): when the first saved route cannot be shown (a page that no longer exists, a refusal of a guard) the page that was shown before comes back instead of an empty window, and the keeper replaces the saved file by the page that is there, so the failure does not return at every start.
- An absolute coordination (`/Orders/...`) asks the page shown by each router if it can be left (`CanLeaveAsync`) before it erases the histories, and stops without touching anything when one says no; when a segment then fails, a router that is still empty gets its previous page back.
- ASP.NET Core: `RouteableInteractions.AnswerAsync` ignores an index that is not a button of the dialog (it was posted by the page, anyone can change it) and leaves the dialog waiting; the dialogs, toasts and system notifications of one visitor are changed under a lock (two requests at the same time lost updates), and `Toasts` is a copy.
- Blazor: `RouteableUrlSync` compares the case of the values of an address (`/items/aB` is not `/items/Ab`); the words of the route still ignore it.
- `AddRoutes` called after `AddRegion` registers the default router (the keyed router of the region was taken for it); the history of a scoped region is limited to 50 pages like the one of a scoped router.
- A transient page the router disposed when it left the history is no longer disposed a second time when the scope ends.
- `RefreshGuardsOn` follows a `PropertyChanged` that names no property (everything changed).
- `RouteParser` leaves the fragment out of a route (`/orders/42#top` is `/orders/42`).
- `RouteableLinkInbox` keeps at most 32 addresses while the application is not ready (the oldest are dropped) and drops at once the ones longer than `RouteableDeepLinks.MaxLength`.
- `RouteableHistoryKeeper`: a save that read the history before another one no longer replaces it (the last read is the last written).
- Modules: `RouteableModules.Dispose()` waits for the asynchronous disposal on the thread pool, so a service whose `DisposeAsync` resumes on the interface thread cannot block it.
- `BuildRouteableProvider` validates the registrations while a debugger is attached in the package (the validation was only compiled into a debug build); the route generator no longer pairs a view model with the controls of WPF, Uno or WinUI by name.

### Changed

- ASP.NET Core: `AddRouteablesAspNetCore` is renamed **`UseRouteables`** (and `UseRouteables<TMenu>`), the name of the other hosts. The old names still work for this minor version, marked `[Obsolete]`, and will be removed in the next major version.
- Deep links on the web need the allowed hosts to be configured (`RouteableLinkInbox`, `RouteableDeepLinks`); the path base is honoured and an address that leaves the application is refused.
- The route generator reads only the compiled model of its own assembly and caches its result: an edit that changes no route no longer regenerates its file.
- A guard that returns `null` fails with an explicit error.
- Blazor: a `JSDisconnectedException` raised while the main layout talks to the browser is ignored (the circuit is gone).

### Documentation

- New sections: keeping the values of a page (`[Persist]`), untrusted input in routes, the Blazor stylesheet bundle.
