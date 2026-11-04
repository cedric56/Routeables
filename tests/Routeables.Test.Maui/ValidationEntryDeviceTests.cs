using Routeables.Maui;
using System.Collections;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Xunit;

namespace Routeables.Test.Maui;

/// <summary>
/// <see cref="RouteableMauiEntry"/> with a native handler: it shows the errors its view model reports for its column (a red frame, an icon and its
/// tooltip), keeps its text in step with the entry inside it, and listens to the view model only while it is on screen.
/// </summary>
public class ValidationEntryDeviceTests
{
    /// <summary>A test view model that implements <see cref="INotifyDataErrorInfo"/> and records subscriber count.</summary>
    private sealed class Errors : INotifyDataErrorInfo
    {
        private readonly Dictionary<string, List<object>> _errors = [];
        private EventHandler<DataErrorsChangedEventArgs>? _changed;

        /// <inheritdoc />
        public bool HasErrors => _errors.Count > 0;

        /// <summary>How many handlers are currently subscribed to <see cref="ErrorsChanged"/>.</summary>
        public int Subscribers => _changed?.GetInvocationList().Length ?? 0;

        /// <inheritdoc />
        public event EventHandler<DataErrorsChangedEventArgs>? ErrorsChanged
        {
            add => _changed += value;
            remove => _changed -= value;
        }

        /// <inheritdoc />
        public IEnumerable GetErrors(string? propertyName)
            => propertyName is not null && _errors.TryGetValue(propertyName, out var list) ? list : Array.Empty<object>();

        /// <summary>Sets the errors for a property and raises <see cref="ErrorsChanged"/> for that property.</summary>
        public void Set(string property, params object[] errors)
        {
            if (errors.Length == 0) _errors.Remove(property); else _errors[property] = [.. errors];
            _changed?.Invoke(this, new DataErrorsChangedEventArgs(property));
        }

        /// <summary>Changes the errors without saying which property: the source signals a broad change.</summary>
        public void SetQuietly(string property, params object[] errors)
        {
            if (errors.Length == 0) _errors.Remove(property); else _errors[property] = [.. errors];
        }

        /// <summary>Raises <see cref="ErrorsChanged"/> with a null property name (broad change).</summary>
        public void RaiseBroadChange() => _changed?.Invoke(this, new DataErrorsChangedEventArgs(null));
    }

    /// <summary>Builds an entry bound to the given errors source and column name.</summary>
    private static RouteableMauiEntry Bound(Errors? errors, string? column = "Name")
        => new() { BindingContext = errors, ColumnName = column! };

    /// <summary>Returns the inner <see cref="Entry"/> hosted by the control.</summary>
    private static Entry Inner(RouteableMauiEntry entry)
    {
        var grid = (Grid)entry.Content;
        return (Entry)((Border)grid.Children[0]).Content!;
    }

    /// <summary>Returns the frame border that surrounds the inner entry.</summary>
    private static Border Frame(RouteableMauiEntry entry) => (Border)((Grid)entry.Content).Children[0];

    /// <summary>Returns the marker border that shows the error icon.</summary>
    private static Border Marker(RouteableMauiEntry entry) => (Border)((Grid)entry.Content).Children[1];

    // ------------------------------------------------------------------------------------------------ errors

    /// <summary>An entry without errors shows no error indicator.</summary>
    [UIFact]
    public void An_entry_without_errors_shows_no_error()
    {
        using var hosted = new Hosted<RouteableMauiEntry>(Bound(new Errors()));

        Assert.False(hosted.View.HasError);
        Assert.Null(hosted.View.ErrorMessage);
        Assert.Null(Marker(hosted.View).Content);
        Assert.Equal(0, Frame(hosted.View).StrokeThickness);
    }

    /// <summary>An error of its column shows the message, a red frame and an icon with its tooltip.</summary>
    [UIFact]
    public void An_error_of_its_column_shows_the_message_a_red_frame_and_an_icon_with_its_tooltip()
    {
        var errors = new Errors();
        using var hosted = new Hosted<RouteableMauiEntry>(Bound(errors));

        errors.Set("Name", "The name is required");

        var entry = hosted.View;
        Assert.True(entry.HasError);
        Assert.Equal("The name is required", entry.ErrorMessage);
        Assert.NotNull(Marker(entry).Content);
        Assert.True(Frame(entry).StrokeThickness > 0);
        Assert.Equal("The name is required", ToolTipProperties.GetText(Marker(entry)));
    }

    /// <summary>Fixing the error clears the indicator.</summary>
    [UIFact]
    public void Fixing_the_error_clears_the_indicator()
    {
        var errors = new Errors();
        using var hosted = new Hosted<RouteableMauiEntry>(Bound(errors));
        errors.Set("Name", "Required");

        errors.Set("Name");

        Assert.False(hosted.View.HasError);
        Assert.Null(hosted.View.ErrorMessage);
        Assert.Null(Marker(hosted.View).Content);
        Assert.Equal(0, Frame(hosted.View).StrokeThickness);
        Assert.Equal(string.Empty, ToolTipProperties.GetText(Marker(hosted.View)));
    }

    /// <summary>The message of a <see cref="ValidationResult"/> is its error message.</summary>
    [UIFact]
    public void The_message_of_a_validation_result_is_its_error_message()
    {
        var errors = new Errors();
        using var hosted = new Hosted<RouteableMauiEntry>(Bound(errors));

        errors.Set("Name", new ValidationResult("Too short"));

        Assert.Equal("Too short", hosted.View.ErrorMessage);
    }

    /// <summary>Only the first error is shown when several are reported.</summary>
    [UIFact]
    public void Only_the_first_error_is_shown()
    {
        var errors = new Errors();
        using var hosted = new Hosted<RouteableMauiEntry>(Bound(errors));

        errors.Set("Name", "First", "Second");

        Assert.Equal("First", hosted.View.ErrorMessage);
    }

    /// <summary>The errors of another column are ignored.</summary>
    [UIFact]
    public void The_errors_of_another_column_are_ignored()
    {
        var errors = new Errors();
        using var hosted = new Hosted<RouteableMauiEntry>(Bound(errors));

        errors.Set("Email", "Not an address");

        Assert.False(hosted.View.HasError);
    }

    /// <summary>A broad change of the errors is read again.</summary>
    [UIFact]
    public void A_broad_change_of_the_errors_is_read_again()
    {
        var errors = new Errors();
        using var hosted = new Hosted<RouteableMauiEntry>(Bound(errors));
        errors.SetQuietly("Name", "Required");
        Assert.False(hosted.View.HasError);

        errors.RaiseBroadChange();

        Assert.True(hosted.View.HasError);
    }

    /// <summary>Naming the column after the errors exist shows them.</summary>
    [UIFact]
    public void Naming_the_column_after_the_errors_exist_shows_them()
    {
        var errors = new Errors();
        errors.SetQuietly("Name", "Required");
        using var hosted = new Hosted<RouteableMauiEntry>(Bound(errors, column: null));
        Assert.False(hosted.View.HasError);

        hosted.View.ColumnName = "Name";

        Assert.True(hosted.View.HasError);
    }

    /// <summary>A view model that reports no errors object shows none.</summary>
    [UIFact]
    public void A_view_model_that_reports_no_errors_object_shows_none()
    {
        using var hosted = new Hosted<RouteableMauiEntry>(Bound(errors: null));

        Assert.False(hosted.View.HasError);
    }

    // ------------------------------------------------------------------------------------------------ following the view model

    /// <summary>The entry listens to its view model only while it is on screen.</summary>
    [UIFact]
    public void The_entry_listens_to_its_view_model_only_while_it_is_on_screen()
    {
        var errors = new Errors();
        var entry = Bound(errors);
        Assert.Equal(0, errors.Subscribers);

        Device.Attach(entry);
        Assert.Equal(1, errors.Subscribers);

        Device.Detach(entry);
        Assert.Equal(0, errors.Subscribers);

        errors.Set("Name", "Required");
        Assert.False(entry.HasError);
    }

    /// <summary>An entry that comes back on screen reads the errors it missed.</summary>
    [UIFact]
    public void An_entry_that_comes_back_on_screen_reads_the_errors_it_missed()
    {
        var errors = new Errors();
        var entry = Bound(errors);
        Device.Attach(entry);
        Device.Detach(entry);
        errors.Set("Name", "Required");

        Device.Attach(entry);

        Assert.True(entry.HasError);
        Device.Detach(entry);
    }

    /// <summary>Another view model replaces the first one; the entry stops following the old one.</summary>
    [UIFact]
    public void Another_view_model_replaces_the_first_one()
    {
        var first = new Errors();
        var second = new Errors();
        using var hosted = new Hosted<RouteableMauiEntry>(Bound(first));

        hosted.View.BindingContext = second;

        Assert.Equal(0, first.Subscribers);
        Assert.Equal(1, second.Subscribers);
        first.Set("Name", "Ignored");
        Assert.False(hosted.View.HasError);
        second.Set("Name", "Required");
        Assert.True(hosted.View.HasError);
    }

    /// <summary>A view model that has no errors to report is not followed.</summary>
    [UIFact]
    public void A_view_model_that_has_no_errors_to_report_is_not_followed()
    {
        using var hosted = new Hosted<RouteableMauiEntry>(Bound(errors: null));

        hosted.View.BindingContext = new object();

        Assert.False(hosted.View.HasError);
    }

    // ------------------------------------------------------------------------------------------------ text

    /// <summary>The text goes to the entry inside and the text typed comes back.</summary>
    [UIFact]
    public void The_text_goes_to_the_entry_inside_and_the_text_typed_comes_back()
    {
        using var hosted = new Hosted<RouteableMauiEntry>(Bound(new Errors()));

        hosted.View.Text = "from the code";
        Assert.Equal("from the code", Inner(hosted.View).Text);

        Inner(hosted.View).Text = "typed";
        Assert.Equal("typed", hosted.View.Text);
    }

    /// <summary>The text property is two-way by default.</summary>
    [UIFact]
    public void The_text_is_two_way_by_default()
    {
        Assert.Equal(BindingMode.TwoWay, RouteableMauiEntry.TextProperty.DefaultBindingMode);
    }

    /// <summary>The underline has a color and a thickness that can be chosen.</summary>
    [UIFact]
    public void The_underline_has_a_color_and_a_thickness_that_can_be_chosen()
    {
        var entry = new RouteableMauiEntry();
        Assert.Equal(Colors.Black, entry.UnderlineColor);
        Assert.Equal(0, entry.UnderlineThickness);

        entry.UnderlineColor = Colors.Blue;
        entry.UnderlineThickness = 3;

        Assert.Equal(Colors.Blue, entry.UnderlineColor);
        Assert.Equal(3, entry.UnderlineThickness);
    }
}