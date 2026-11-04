using Avalonia;
using Avalonia.Controls;
using Avalonia.Styling;
using Routeables.Avalonia;
using Xunit;

namespace Routeables.Test.Avalonia;

/// <summary>The style that shows data validation errors as a tooltip.</summary>
[Collection("Avalonia headless")]
public sealed class HeadlessErrorStyleTests(HeadlessApp app)
{
    /// <summary>UseRouteables registers the error style on the application exactly once.</summary>
    [Fact]
    public Task UseRouteables_adds_the_error_style_to_the_application_once() => app.RunAsync(() =>
    {
        Assert.Single(Application.Current!.Styles.OfType<ErrorStyle>());
        return Task.CompletedTask;
    });

    /// <summary>The error style applies a theme setter for <see cref="DataValidationErrors"/>.</summary>
    [Fact]
    public Task The_error_style_styles_the_validation_errors_of_a_control() => app.RunAsync(() =>
    {
        var style = new ErrorStyle();

        var inner = Assert.IsType<Style>(Assert.Single(style));
        Assert.Contains(inner.Setters.OfType<Setter>(), s => s.Property == DataValidationErrors.ThemeProperty);
        return Task.CompletedTask;
    });
}