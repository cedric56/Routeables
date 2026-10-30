# Transitions

A **transition** is how a region brings in the view of a route change. It is chosen for the region, or for one navigation, and the navigation wins. The router does not wait for the animation: the route change ends when the history has changed, and only what is on screen is animated.

## The built-in set

| Name | The new view |
| --- | --- |
| `RouteTransition.None` | appears at once |
| `RouteTransition.Fade` | fades in |
| `RouteTransition.Slide` | comes in from the right when going forward, from the left when going back, fading in |
| `RouteTransition.SlideUp` | comes in from below when going forward, from above when going back, fading in |
| `RouteTransition.SlideFade` | like `Slide` over a short distance: a light movement, fading in |
| `RouteTransition.Swipe` | crosses the whole region, from the right when going forward, from the left when going back |
| `RouteTransition.Pop` | grows from a smaller size with a small overshoot, fading in |
| `RouteTransition.Scale` | grows from a smaller size, fading in |
| `RouteTransition.Lift` | rises a little, from slightly smaller, fading in: a card that is put down |
| `RouteTransition.Blur` | goes from blurred to sharp, fading in |
| `RouteTransition.Spotlight` | goes from dim and blurred to clear, settling from slightly larger |
| `RouteTransition.Flip` | turns around its vertical axis, like a card being flipped |
| `RouteTransition.Rotate` | turns in around its centre, growing, fading in |
| `RouteTransition.Reveal` | is uncovered from the bottom to the top |
| `RouteTransition.Curtain` | is uncovered from the top to the bottom |

`RouteTransition.BuiltIn` lists them. The names and the idea of the effects follow the set of the Blazor routers (Blazouter); the animations are Routeables' own, written for each host. They all start from the same description, `RouteTransition.Start(direction)` (opacity, size, offset, angle, the point it turns around), so they look alike on every host; what a host cannot draw, it leaves out:

| | Avalonia, WPF, MAUI | Uno | Blazor |
| --- | --- | --- | --- |
| `Blur`, `Spotlight` | fade in and settle (no blur) | fade in | blur and brightness (CSS `filter`) |
| `Flip` | the width grows from a sliver (no 3D) | the platform's pop | 3D rotation (`perspective`) |
| `Reveal`, `Curtain` | the height grows from the bottom / the top | the platform's pop | `clip-path` |
| `Pop`, `Scale`, `Rotate` | as described | the platform's pop | as described |
| `Slide`, `SlideUp`, `SlideFade`, `Swipe`, `Lift` | as described | an entrance transition of the platform (offset in the right direction) | as described |

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

Animation is native to each host, so a custom transition is registered per host under a name, then used like a built-in one (`WithTransition("Spin")`, `Transition="Spin"`). A name that is a built-in one replaces it (register `Pop` and your animation is played instead of the built-in one). A name nobody registered shows the view at once and says so in the debug output.

| Host | Register |
| --- | --- |
| Avalonia | `RouteTransitions.Register("Spin", (direction, duration) => new Rotate3DTransition(duration))` returns an `IPageTransition` |
| WPF | `RouteTransitions.Register("Grow", (region, direction, duration) => ...)` starts a storyboard on the region |
| MAUI | `RouteTransitions.Register("Grow", async (region, direction, duration) => await region.ScaleToAsync(...))` |
| Uno | `RouteTransitions.Register("Pop", direction => new TransitionCollection { new EntranceThemeTransition { FromVerticalOffset = 30 } })` returns the transitions the region plays |
| Blazor | a CSS animation of yours: `.rt-enter.rt-spin { animation-name: spin; }` |

## What each host does

- **Avalonia** animates the opacity and the `RenderTransform` of the new view (the built-in ones), so that view should not rely on a `RenderTransform` of its own. A custom transition is an `IPageTransition` given to the `PageTransition` of the control (going back reverses it with `IsTransitionReversed`).
- **WPF, MAUI** animate the new view (opacity, scale, rotation, translation). WPF plays nothing when Windows has animations turned off, and leaves the view alone when the region has a `RenderTransform` of its own (it fades in).
- **Uno** uses the theme transitions of the platform (`ContentThemeTransition`, `EntranceThemeTransition`), which decide how long they last: the effects it cannot reproduce become a short rise from below (Uno does not implement `PopupThemeTransition`). Register a name to play your own animation.
- **Blazor** gives the element the classes `rt-enter rt-{name} rt-{forward|back|none}` and the variable `--rt-duration`; the animations of the built-in names are in `theme.css`, and `prefers-reduced-motion` turns them off.

`RouteableRegionItems` and the controls that do not show a router's current view (menus, tabs) play no transition.

## Sample

The **History** page of every sample (Avalonia, WPF, Uno, MAUI, Blazor) has a *Transition* list that sets the transition of its frame (`Transition="{Binding Transition}"` on the `RouteableRegion`; on MAUI and Uno the property is a string, so bind the name: `{Binding Transition.Name}`), a *Push (SlideUp)* button that asks for its own transition for that one navigation (`WithTransition(RouteTransition.SlideUp)`), and a custom transition, *Pop*, registered in the code-behind of the page (a CSS animation in the Blazor sample).
