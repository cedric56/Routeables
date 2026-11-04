using Uno.UI.RuntimeTests;

namespace Routeables.Test.Uno;

/// <summary>The runner on top and, below it, the zone the tests put their controls in (they must be in the tree of the window to be loaded).</summary>
public sealed class MainPage : Page
{
    /// <summary>Builds the page with the unit-test runner in the upper half and the host grid in the lower half.</summary>
    public MainPage()
    {
        var root = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition { Height = new GridLength(1, GridUnitType.Star) },
                new RowDefinition { Height = new GridLength(1, GridUnitType.Star) },
            },
        };
        var runner = new UnitTestsControl();
        Grid.SetRow(runner, 0);
        root.Children.Add(runner);

        Host = new Grid();
        Grid.SetRow(Host, 1);
        root.Children.Add(Host);

        Content = root;
    }

    /// <summary>Where the controls under test are shown.</summary>
    public static Grid Host { get; private set; } = null!;
}