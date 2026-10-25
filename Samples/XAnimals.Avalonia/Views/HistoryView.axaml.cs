using Avalonia;
using Avalonia.Media;
using Routeables.Abstraction;
using Routeables.Avalonia;
using System;
using XAnimals.ViewModels;

namespace XAnimals.Avalonia;

public partial class HistoryView : RouteableFor<HistoryViewModel>
{
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
