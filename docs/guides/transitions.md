# Transitions

A **transition** is how a region brings in the view of a route change. It is chosen for the region, or for one navigation, and the navigation wins. The router does not wait for the animation: the route change ends when the history has changed, and only what is on screen is animated.

## The built-in set

| Name | The new view |
| --- | --- |
| `RouteTransition.None` | appears at once |
| `RouteTransition.Fade` | fades in |
| `RouteTransition.Slide` | comes in from the right when going forward, from the left when going back, fading in |
| `RouteTransition.SlideUp` | comes in from below when going forward, from above when going back, fading in |

*Forward* is a new page or `ForwardAsync`; *back* is `GoBackAsync`, `PopAsync` or `GoBackToRootAsync`; `MoveToAsync` goes the way of the index.

## For one navigation

```csharp
var args = RouteableArgs.Create().WithTransition(RouteTransition.Slide);
await provider.GoToAsync("/orders/42", args);

// with its own length, or by name
args.WithTransition(RouteTransition.Fade.WithDuration(TimeSpan.FromMilliseconds(400)));
args.WithTransition("SlideUp");
```

A menu entry carries it in its `Arguments`: `entry.Arguments.WithTransition(RouteTransition.Fade)`.

## For a region

Set `Transition` on the `RouteableRegion` (the main area of the shell is one too):

```xml
<routeables:RouteableRegion Region="detail" Transition="Slide" TransitionDuration="0:0:0.3" />
```

Without `Transition`, a region keeps what it did before: `PageTransition` on Avalonia, the fade of `TransitionDuration` on WPF, MAUI and Uno, nothing on Blazor. A transition that gives no length uses `TransitionDuration`, else 250 ms.

## Custom transitions

Animation is native to each host, so a custom transition is registered per host under a name, then used like a built-in one (`WithTransition("Spin")`, `Transition="Spin"`). A name that is a built-in one replaces it. A name nobody registered shows the view at once and says so in the debug output.

| Host | Register |
| --- | --- |
| Avalonia | `RouteTransitions.Register("Spin", (direction, duration) => new Rotate3DTransition(duration))` returns an `IPageTransition` |
| WPF | `RouteTransitions.Register("Grow", (region, direction, duration) => ...)` starts a storyboard on the region |
| MAUI | `RouteTransitions.Register("Grow", async (region, direction, duration) => await region.ScaleToAsync(...))` |
| Uno | `RouteTransitions.Register("Pop", direction => new TransitionCollection { new PopupThemeTransition() })` returns the transitions the region plays |
| Blazor | a CSS animation of yours: `.rt-enter.rt-spin { animation-name: spin; }` |

## What each host does

- **Avalonia** animates the opacity and the `RenderTransform` of the new view (the built-in ones), so that view should not rely on a `RenderTransform` of its own. A custom transition is an `IPageTransition` given to the `PageTransition` of the control (going back reverses it with `IsTransitionReversed`).
- **WPF, MAUI** animate the new view (opacity, translation). WPF plays nothing when Windows has animations turned off.
- **Uno** uses the theme transitions of the platform (`ContentThemeTransition`, `EntranceThemeTransition`), which decide how long they last.
- **Blazor** gives the element the classes `rt-enter rt-{name} rt-{forward|back|none}` and the variable `--rt-duration`; the animations of the built-in names are in `theme.css`, and `prefers-reduced-motion` turns them off.

`RouteableRegionItems` and the controls that do not show a router's current view (menus, tabs) play no transition.

## Sample

The **History** page of every sample (Avalonia, WPF, Uno, MAUI, Blazor) has a *Transition* list that sets the transition of its frame (`Transition="{Binding Transition}"` on the `RouteableRegion`), a *Push (SlideUp)* button that asks for its own transition for that one navigation (`WithTransition(RouteTransition.SlideUp)`), and a custom transition, *Pop*, registered in the code-behind of the page (a CSS animation in the Blazor sample).
