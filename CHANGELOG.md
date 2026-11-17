# Changelog

All notable changes to the Routeables packages. The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and the versions follow [VERSIONING.md](VERSIONING.md). Every package of the repository has the same version.

## Unreleased

### Added

- **`RouteableTexts`**: the buttons of the message boxes (`Yes`, `No`, `Cancel`, `Abort`, `Ok`) and the default title `Application` can be translated (`RouteableTexts.ButtonTranslator`, `RouteableTexts.ApplicationTitle`); every host (WPF, Avalonia, MAUI, WinUI 3, Uno, Blazor, Razor) and the task dialogs use them. What a message box answers stays English, so the code that reads it does not change. See the hosts guide.

- **System notifications on the Linux and Windows desktop** from the plain `net10.0` build of `Routeables.Native` (Avalonia, WPF, a console): Linux through `org.freedesktop.Notifications` on the D-Bus session bus (a small client inside the library, no package: a tap on the notification is a tap, closing it a dismissal), Windows through the toasts of Windows 10 and 11 (`WindowsContracts.Net.Foundation.UniversalApiContract`, `PrivateAssets="all"`; no Windows SDK target), each in its own partial file (`SystemNotificationCenter.linux.cs`, `.winrt.cs`, `.browser.cs`) like the other platforms; `Routeables.Native` now uses the Razor SDK and its JavaScript lives in `wwwroot`; an Avalonia head that targets `net10.0-windows10.0.19041.0` gets the notification center of the Windows App SDK instead (`Routeables.Avalonia` is built for that target too). **In the browser**, WebAssembly applications that are not Blazor (Avalonia, Uno) use the notification center of the browser too: the module and the service worker of `Routeables.Blazor` moved to `Routeables.Native` (Blazor serves the same files), and `Routeables.Native` talks to it with `JSImport`. The `AppNotificationOptions` of `Routeables.Blazor` is gone: every host uses the one of `Routeables.Native` (namespace `Routeables.Native`), which gained `ServiceWorkerUrl`, `DefaultIcon` and `RequestPermissionOnInitialize`. **macOS** (the plain `net10.0` build, no `net10.0-macos` target) has them too: `UNUserNotificationCenter` through the Objective-C runtime (`Desktop/ObjC.cs`, `SystemNotificationCenter.macos.cs`), for an application in a `.app` bundle; outside a bundle nothing is created. `CreateNotificationAsync` still returns `null` where there is no session bus.

### Changed

- **One rule for "the same route?"** (`RouteAddress`, internal): the router (menu entry lookup, type guards, not-found page, `GoBackToAsync`), `WentTo`, the menus (`RouteableMenuViewModelBase`), the table of routes, the controls of navigation (`RouteTarget`, the Shell tabs of MAUI), the sign-in guard of Blazor and the address bar (`RouteableUrlSync`) used eight different comparisons. They now agree: a leading or closing slash does not count, the words of a path ignore the case, a query ignores the order of its pairs and the case of its names but not of its values, and an escaped address is the plain one. Visible effects: a menu entry is found for the page whatever the case or slashes of its route (`/Orders/` is `/orders`), two entries that differ by their query stay two, `RouteableTable` drops `orders/` as a duplicate of `/orders`, and the sign-in guard no longer redirects a visit of the sign-in page with a `returnUrl` to itself.

- **The address bar of Blazor asks any menu to select an entry**: `IRouteableMenuViewModel.SelectEntryAsync` (a default that sets `SelectedEntry`, which the base classes of `Routeables.Core` already implement). A menu written by hand can answer `false` when a guard keeps the user where they were, and the address is put back; before, only the base classes could.

- **Avalonia uses `Routeables.Native`** for its system notifications instead of Avalonia.Labs.Notifications (removed: on Linux its D-Bus dependency crashed the application at start). Turn them on with `AppBuilder.WithAppNotifications()` (`Routeables.Avalonia`). `Routeables.Avalonia` now also builds for `net10.0-android` and `net10.0-ios`: an Android or iOS head must reference it directly to get them. The desktop has them too (Linux through D-Bus, Windows through the notification area, see above); macOS and the browser do not: `CreateNotificationAsync` returns `null` there. On Android pass the intents to `SystemNotificationCenter.HandleIntent` from the main activity.

- **`Routeables.Native` no longer depends on MAUI Essentials or AndroidX** on Android: it follows the activity in front itself, asks for the notification permission with it, and uses `Notification.Builder`. No `Platform.Init` is needed outside MAUI. Applications build the services (`UseRouteablesServices`) before the first activity, as they do; `NativeDialogs.Initialize()` starts it for a host that does not.
- **The three-button dialog is in `Routeables.Native`** (`NativeDialogs.DisplayThreeButtonsAsync`): MAUI, WinUI 3 and Uno use it (Android alert, Windows App SDK `ContentDialog`, iOS alert or sheet) and draw their own where it has nothing to show.

### Fixed

- **A view that fails to show on the UI thread is shown again** (`RegionBinding.ShowFailed`, used by `RouteableRegion`): when the route change came from another thread, the failure happened after the binding had taken the view for shown, and the same view was never tried again. `RouteableHistoryKeeper` also disposes its cancellation source.

- **Starting with a link and a saved history** (`AddRouteableHistory` and `RouteableLinkInbox`): the history is put back first and the addresses are followed after it. Before, the restoration, which starts by erasing the history, could remove the page that the link had just opened.
- **An absolute coordination whose page throws** (an exception in `OnEnteringAsync`, a cancellation) puts back the page that was shown, as a failed result already did; the regions were left empty.
- **`GoBackAsync(steps)` and `GoBackToAsync`** work out the target once the route change may start, so a route change that is already running cannot make them land on another page (`IRouter.MoveBackAsync`).
- **System notifications**: Windows (plain `net10.0`) no longer reports a notification as closed when its banner leaves the screen, and a notification that is shown again replaces the first one on Linux and Windows instead of adding a second one; the expiration of an updated notification runs from its last display; the identifiers of the notifications no longer start again at 1 at each launch (an old notification of the system could be mistaken for a new one); `NotificationCompleted` is raised on the context of the interface (the one in which `Configure` ran), and never on the thread that reads the D-Bus replies.
- **Menu**: it follows the page by its **route** first, so two entries of the same view model type with other routes (`/products/shoes`, `/products/hats`) are told apart after a Back; two overlapping `GoBackAsync` / `GoForwardAsync` of the menu no longer switch off each other's follow; `SelectEntryAsync` and `SelectAsync` take a `CancellationToken`, which the Blazor address sync passes so that a navigation the browser gave up stops its selection.
- **`IRouteTypeGuard`** compares the template like `GoToAsync` does (case and leading `/` ignored): a menu entry that writes its template another way no longer skips the guard.
- **`MoveBackAsync(steps)`** refuses a number below 1.
- **Regions**: an exception in a view that is shown from another thread (queued on the dispatcher) goes to the error channel instead of ending the application (Avalonia, MAUI, WinUI) or being lost (WPF).
- **Linux notifications**: a connection to the session bus that failed is tried again after 5 seconds (it was kept for the life of the process); showing a notification and asking whether the service is there no longer block the calling thread (`SystemNotificationCenter` waits asynchronously), and a call that is not answered in 3 seconds is forgotten instead of kept; a D-Bus header field that the client does not know is read past (the specification asks for it) instead of dropping the message; the action text is `AppNotificationOptions.DefaultActionText`.
- **Telemetry**: a route that no template names is tagged `(unmatched)` in `routeables.route`, not with its address.

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
