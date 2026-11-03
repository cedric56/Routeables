using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Routeables.Wpf;

namespace XAnimals.Wpf.Views;

public partial class HistoryView : RouteableFor
{
    // A custom transition: registered once under a name, then chosen by name like the built-in ones (Transition="Grow").
    static HistoryView() =>
        RouteTransitions.Register("Grow", (region, direction, duration) =>
        {
            var scale = new ScaleTransform(0.85, 0.85);
            region.RenderTransformOrigin = new Point(0.5, 0.5);
            region.RenderTransform = scale;

            var grow = new DoubleAnimation(0.85, 1, duration) { EasingFunction = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.4 } };
            // The transform is given back when it ends, so the built-in Slide can use the region again.
            grow.Completed += (_, _) => region.RenderTransform = Transform.Identity;
            scale.BeginAnimation(ScaleTransform.ScaleXProperty, grow);
            scale.BeginAnimation(ScaleTransform.ScaleYProperty, grow);
            region.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, 1, duration));
        });

    public HistoryView()
    {
        InitializeComponent();
    }
}
