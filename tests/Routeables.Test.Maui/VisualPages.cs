using Routeables.Maui;

namespace Routeables.Test.Maui;

/// <summary>View model of <see cref="VisualFormView"/>.</summary>
public sealed class VisualFormViewModel;

/// <summary>View model of <see cref="VisualAboutView"/>.</summary>
public sealed class VisualAboutViewModel;

/// <summary>A page with the controls an application puts on one, for the screenshots.</summary>
public sealed class VisualFormView : RouteableFor<VisualFormViewModel>
{
    /// <summary>Builds the page: a title, a text, a form and buttons.</summary>
    public VisualFormView() => Content = new VerticalStackLayout
    {
        Padding = new Thickness(24),
        Spacing = 12,
        Children =
        {
            new Label { Text = "Settings", FontSize = 24, FontAttributes = FontAttributes.Bold },
            new Label { Text = "Choose how the application behaves." },
            new Entry { Text = "Routeables", Placeholder = "Name" },
            new HorizontalStackLayout { Spacing = 8, Children = { new CheckBox { IsChecked = true }, new Label { Text = "Keep me signed in", VerticalOptions = LayoutOptions.Center } } },
            new HorizontalStackLayout { Spacing = 8, Children = { new Button { Text = "Save" }, new Button { Text = "Cancel" } } },
        },
    };
}

/// <summary>A second page for the screenshots.</summary>
public sealed class VisualAboutView : RouteableFor<VisualAboutViewModel>
{
    /// <summary>Builds the page: a title, a text and a progress bar.</summary>
    public VisualAboutView() => Content = new VerticalStackLayout
    {
        Padding = new Thickness(24),
        Spacing = 12,
        Children =
        {
            new Label { Text = "About", FontSize = 24, FontAttributes = FontAttributes.Bold },
            new Label { Text = "Routeables routes between pages on every platform." },
            new ProgressBar { Progress = 0.6 },
        },
    };
}
