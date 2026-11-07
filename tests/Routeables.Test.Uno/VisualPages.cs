using Routeables.Uno;

namespace Routeables.Test.Uno;

/// <summary>View model of <see cref="VisualFormView"/>.</summary>
public sealed class VisualFormViewModel;

/// <summary>View model of <see cref="VisualAboutView"/>.</summary>
public sealed class VisualAboutViewModel;

/// <summary>A page with the controls an application puts on one, for the screenshots.</summary>
public sealed class VisualFormView : RouteableFor<VisualFormViewModel>
{
    /// <summary>Builds the page: a title, a text, a form and buttons.</summary>
    public VisualFormView() => Content = new StackPanel
    {
        Padding = new Thickness(24),
        Spacing = 12,
        Children =
        {
            new TextBlock { Text = "Settings", FontSize = 24, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold },
            new TextBlock { Text = "Choose how the application behaves.", TextWrapping = TextWrapping.Wrap },
            new TextBox { Text = "Routeables", PlaceholderText = "Name" },
            new CheckBox { Content = "Keep me signed in", IsChecked = true },
            new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                Children = { new Button { Content = "Save" }, new Button { Content = "Cancel" } },
            },
        },
    };
}

/// <summary>A second page for the screenshots.</summary>
public sealed class VisualAboutView : RouteableFor<VisualAboutViewModel>
{
    /// <summary>Builds the page: a title, a text and a progress bar.</summary>
    public VisualAboutView() => Content = new StackPanel
    {
        Padding = new Thickness(24),
        Spacing = 12,
        Children =
        {
            new TextBlock { Text = "About", FontSize = 24, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold },
            new TextBlock { Text = "Routeables routes between pages on every platform.", TextWrapping = TextWrapping.Wrap },
            new ProgressBar { Value = 60, Minimum = 0, Maximum = 100 },
        },
    };
}
