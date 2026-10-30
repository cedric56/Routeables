using Avalonia;
using Avalonia.Animation;
using Avalonia.Media;
using Routeables.Abstraction;
using Routeables.Avalonia;
using System;
using XAnimals.ViewModels;

namespace XAnimals.Avalonia;

public partial class HistoryView : RouteableFor<HistoryViewModel>
{
    // A custom transition: registered once under a name, then chosen by name like the built-in ones (Transition="Grow").
    static HistoryView() =>
        RouteTransitions.Register("Grow", (direction, duration) => new CompositePageTransition
        {
            PageTransitions = [new CrossFade(duration), new PageSlide(duration, PageSlide.SlideAxis.Vertical)]
        });

    public HistoryView()
    {
        InitializeComponent();

        // The frame shows the view at a design size and scales it to the room it has: width and height both count.
        FrameHost.SizeChanged += (_, e) =>
        {
            var inner = FrameHost.Bounds.Deflate(FrameHost.BorderThickness);
            var scale = Math.Min(inner.Width / Frame.Width, inner.Height / Frame.Height);
            if (double.IsFinite(scale) && scale > 0)
                Frame.RenderTransform = new ScaleTransform(scale, scale);
        };
    }
}
