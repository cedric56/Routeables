using Microsoft.UI.Xaml.Media.Animation;
using Routeables.Uno;

namespace XAnimals.Uno.Views;

public sealed partial class HistoryView : RouteableFor
{
    // A custom transition: registered once under a name, then chosen by name like the built-in ones (Transition="Grow").
    static HistoryView() =>
        RouteTransitions.Register("Grow", direction => new TransitionCollection { new PopupThemeTransition() });

    public HistoryView()
    {
        InitializeComponent();
    }
}
