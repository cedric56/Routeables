# Back, deep links, guards and diagnostics

What an application needs around the route changes themselves: the Back button of the system, an address opened from outside, pages that come back after the system killed the process, a rule that applies to every route, and numbers about what the router does.

## The Back button of the system

Every host answers it for you: one page back in the history when there is one, otherwise the system decides (the application closes, the previous app comes forward).

| Host | What is wired |
| --- | --- |
| Avalonia | `TopLevel.BackRequested` (Android Back), the Back button of the mouse, `Alt+Left` (not in the browser) |
| MAUI | `Shell.OnBackButtonPressed` of `RouteablesShell` |
| WPF | the `NavigationCommands.BrowseBack` command of the window (the Back button of the mouse and the keyboard) |
| Uno | `SystemNavigationManager.BackRequested`, subscribed only while the history has a page to go back to, because from Android 16 a subscribed handler consumes the request |

A host of your own calls `RouteableBack.TryGoBack(provider)`: it returns `false` when there is nothing to go back to, and then the request is yours to leave to the system.

## Route guards

`CanLeaveAsync` lets a page refuse to be left. A guard is the other side: a rule about the page that is **entered**, written once for every route.

```csharp
public sealed class SignedInGuard(ISession session) : IRouteGuard
{
    public Task<RouteGuardDecision> CanEnterAsync(RouteableRoutingEventArgs change, CancellationToken ct)
        => Task.FromResult(change.Route is "/account" && !session.IsSignedIn
            ? RouteGuardDecision.Redirect("/sign-in")
            : RouteGuardDecision.Allow);
}

services.AddSingleton<IRouteGuard, SignedInGuard>();
```

- Every registered guard is asked, in registration order, before the route change starts. The first one that does not allow decides.
- `Cancel` leaves the user where they are (`result.Cancelled` is `true`). `Redirect(route)` goes to another route instead; it is followed only for a `Push` (for a back, a forward or a pop it is a cancel), and at most 8 redirects are chained, so two guards that send the user to each other end instead of looping.
- A guard that throws makes the route change fail with that exception.
- The redirect is a normal route change: the guards apply to it too.

### Taking the user back after a sign-in

A guard that sends the user to `/sign-in` loses the page they wanted. `RouteableReturn` keeps it:

```csharp
services.AddRouteableReturn();

// in the guard: remembers the route of the change and redirects (only a Push is remembered)
return returns.RedirectFrom(change, "/sign-in");

// in the sign-in view model, once the user is signed in
await router.GoToAsync(returns.Take() ?? "/home");
```

It works for an address opened from outside too, since that goes through the router.

## Addresses opened from outside

`RouteableDeepLinks` turns an address into a route and goes to it through the router, so guards apply.

```csharp
await RouteableDeepLinks.GoToAsync(provider, new Uri("xanimals://dogs/Eurohound"), schemes: ["xanimals", "https"]);
```

- `https://example.com/dogs/Eurohound?x=1` and `xanimals://dogs/Eurohound?x=1` both give `/dogs/Eurohound?x=1`. The fragment is dropped.
- An address with another scheme, no path (`xanimals://`) or no registered route is a failed result, not an exception: the address was typed by someone else.
- The host receives the address its own way and passes it on to a `RouteableLinkInbox`. The inbox is a field of the application, because the platform often delivers the link before the container exists. It keeps what it receives, and once you give it the container (`Attach`) and the first page is shown, it follows the addresses on the thread of the interface, one after the other.

```csharp
public static class LinkInbox { public static RouteableLinkInbox Instance { get; } = new(["xanimals"]); }

LinkInbox.Instance.Attach(provider);                     // once the container is built
LinkInbox.Instance.Push(new Uri("xanimals://dogs/Eurohound")); // from the platform code
```

| Sample | Where the address comes from |
| --- | --- |
| Avalonia on Android | an `[IntentFilter]` for the `xanimals` scheme; `OnCreate` and `OnNewIntent` of `MainActivity` |
| MAUI on Android | the same `[IntentFilter]` and the same two methods |
| MAUI on iOS | `CFBundleURLTypes` in `Info.plist`; `OpenUrl` of `AppDelegate` and `WillConnect` / `OpenUrlContexts` of `SceneDelegate` |
| WPF | the first command-line argument |
| Uno on Android | the same `[IntentFilter]` and methods in `MainActivity` |
| Uno on desktop | the first command-line argument, pushed in `Main` |

Uno on iOS, Windows packaged apps and the web are not wired in the samples; they end the same way: the platform gives the `Uri`, the application pushes it. An App Link or a Universal Link for `https` addresses needs a file on your server (`assetlinks.json`, `apple-app-site-association`) that the framework cannot provide.

## Pages that come back

The system may kill the process of an application in the background; the user expects to find the pages they had.

```csharp
// when the system asks to save the state
bundle.PutString("history", RouteableState.Serialize(RouteableState.Capture(router)));

// at the next start, once the first page is shown
await RouteableState.RestoreAsync(router, RouteableState.Deserialize(bundle.GetString("history")));
```

`Capture` returns the route of each page up to the current one (the pages that can be reached with Forward are left out). `RestoreAsync` goes to them in order and stops at the first one that cannot be reached; the pages before it stay.

What is saved is the **address** of the pages: the view model is built again from its route, so what was typed in a form or loaded in memory is not brought back, and a page reached with values that are not in its address (`RouteableArgs.WithValue`) is reached without them. Pages that have no address are not saved.

### Keeping the values of a page

A view model that implements `IRouteableState` writes the small values it wants back and reads them after the page is built:

```csharp
// FeaturesViewModel.cs of the XAnimals sample (the Features page keeps its draft)
public class FeaturesViewModel : ViewModelBase, IRouteableState, IDisposable
{
    public string Draft { get; set; } = "";

    public void SaveState(IDictionary<string, string> state) => state["draft"] = Draft;

    public void LoadState(IReadOnlyDictionary<string, string> state)
    {
        if (state.TryGetValue("draft", out var draft))
            Draft = draft;
    }
}

var text = RouteableState.Serialize(RouteableState.CaptureSnapshots(router));
await RouteableState.RestoreSnapshotsAsync(router, RouteableState.DeserializeSnapshots(text));
```

`LoadState` runs after the view model is initialized from its address, so it wins over the initialization. Keep to text that is small (a search, a draft, a selected tab): the Android bundle is limited to about 500 KB for the whole application. `RouteableLinkInbox.Save` and `Restore` do this for you, and a text made by the routes-only `Serialize` is read by `DeserializeSnapshots` (pages with no values).

## Diagnostics: traces and metrics

Every route change is an activity and a measure, with no setup in the framework. Listen to the source named `Routeables` (`RouteableDiagnostics.Name`) with OpenTelemetry or `ActivityListener` / `MeterListener`.

| Name | Kind | Content |
| --- | --- | --- |
| `Routeables.Route` | Activity | tags `routeables.kind` (Push, Back, Forward, Pop, Root), `routeables.route`, `routeables.view_model`, `routeables.outcome` (Success, Cancelled, Failed). A change that a guard redirects ends as Cancelled; the redirect is a change of its own |
| `routeables.route.duration` | Histogram (ms) | the time of a route change, tagged with `routeables.kind` and `routeables.outcome` only (the route is left out to keep the number of series small; read it from the activity) |
| `routeables.route.redirects` | Counter | the redirects made by guards, tagged with `routeables.route` (the route they lead to) |

The duration is the time **in the router** (guards, creation, initialization). The time to draw the new page is the host's; on a slow phone it is usually most of what the user waits for. The Avalonia sample's `RouteTimings` shows both, and the *Features* page of every sample lists the last measures with a `MeterListener` (`RouteMetrics`).

## Samples

Every sample (Avalonia, MAUI, MAUI Hybrid, Uno, WPF, Blazor Server, Blazor WebAssembly) has a **Features** page, backed by `FeaturesViewModel` in `XAnimals.ViewModels`:

| Feature | On the page |
| --- | --- |
| Route guard and return | the Members page is for members (`MembersGuard`); signed out, opening it brings you to Features, *Sign in* only signs in, and *Open Members* goes there |
| Address from outside | *Open xanimals://dogs/Eurohound* follows it through `RouteableDeepLinks`; on Android, iOS and the WPF and Uno desktop heads the platform code gives the same kind of address to `RouteableLinkInbox` |
| Saved values (phone and tablet only) | the note is kept by `IRouteableState` and comes back with the page after the process was killed (Android) |
| Measures | the last route changes, read with a `MeterListener` (`RouteMetrics`) |
| Back of the system | the Back button of Android, the mouse button and Alt+Left on a desktop, the browser Back in Blazor |
| Transitions | the frame of the *History* page fades its pages in (`PageTransition` on Avalonia, `TransitionDuration` on MAUI, Uno and WPF) |

## Tabs with their own history, and transitions

These two are patterns, not features of the router, and the framework does not add code for them.

- **A history per tab**: give each tab its own region (`services.AddRegion("orders")`, a `RouteableRegion` in the tab, see [Regions](regions.md)) and set `Region = "orders"` on its menu entry. A region has its own history, so Back inside a tab stays in the tab, and the tab keeps its page when the user leaves it and comes back. The Back request of the system (see above) goes through the router of the selected tab first, and through the default router when the tab is on its first page: `RouteableBack.Target` tells which one. A host of your own uses `RouteableBack.CanGoBack` to enable its Back button.

  The menu of the router (`MainViewModelBase`) keeps one history for the pages it shows itself; it does not hold a stack per entry, because Back, Forward, Pop, deep links and the saved pages all rely on one ordered history. A tab that needs a stack of its own is a region.
- **Transitions**: how a page appears is the job of the control that shows it, so the option is on `RouteableRegion`.

| Host | Option |
| --- | --- |
| Avalonia | `RouteableRegion` is a `TransitioningContentControl`: set `PageTransition` (`CrossFade`, `PageSlide`, ...) |
| WPF | `TransitionDuration="0:0:0.2"`: the new view fades in |
| MAUI | `TransitionDuration="0:0:0.2"`: the new view fades in |
| Uno | `TransitionDuration="0:0:0.2"` turns on the theme transition of the platform (any value above zero; the platform chooses the length) |
| Blazor | CSS on the element that holds the view |

See [Transitions](transitions.md) for the built-in set (`None`, `Fade`, `Slide`, `SlideUp`), the choice per navigation (`RouteableArgs.WithTransition`) and custom transitions.

Zero, the default, shows the view at once. The region of a module's page and the main zone of the shell use the same control, so a transition set there applies to every page.
