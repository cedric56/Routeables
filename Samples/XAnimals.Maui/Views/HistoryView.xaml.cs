using Routeables.Abstraction;
using Routeables.Maui;
using XAnimals.ViewModels;

namespace XAnimals.Maui.Views;

public partial class HistoryView : RouteableFor
{
    // A custom transition: registered once under a name, then chosen by name like the built-in ones (Transition="Grow").
    static HistoryView() =>
        RouteTransitions.Register("Grow", async (region, direction, duration) =>
        {
            var ms = (uint)duration.TotalMilliseconds;
            region.Scale = 0.85;
            region.Opacity = 0;
            await Task.WhenAll(region.ScaleToAsync(1, ms, Easing.SpringOut), region.FadeToAsync(1, ms));
        });

    public HistoryView()
    {
        InitializeComponent();
    }
}
