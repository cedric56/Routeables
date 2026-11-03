using Microsoft.Maui.Controls.Shapes;
using System.ComponentModel;

namespace Routeables.Maui;

/// <summary>
/// An Entry with a built-in validation indicator (red underline + error icon with tooltip),
/// driven by the current BindingContext's <see cref="INotifyDataErrorInfo"/> — e.g. a
/// CommunityToolkit.Mvvm <c>ObservableValidator</c>-derived view model. Set <see cref="ColumnName"/>
/// to the bound property's name (matching the name used in [ObservableProperty]/validation
/// attributes); AvaeEntry does the rest, no converter or custom interface required on the
/// view model.
/// </summary>
public class RouteableMauiEntry : ContentView
{
    PathGeometry? geometry = new PathGeometryConverter().ConvertFromInvariantString("M14,7 A7,7 0 0,0 0,7 M0,7 A7,7 0 1,0 14,7 M7,3l0,5 M7,9l0,2") as PathGeometry;

    INotifyDataErrorInfo? _subscribed;

    /// <summary>Creates the entry, with its validation indicator.</summary>
    public RouteableMauiEntry()
    {
        Content = BuildContent();
    }

    /// <summary>The <see cref="ColumnName"/> property.</summary>
    public static readonly BindableProperty ColumnNameProperty = BindableProperty.Create(
            nameof(ColumnName), typeof(string), typeof(RouteableMauiEntry),
            propertyChanged: (sender, _, _) => ((RouteableMauiEntry)sender).RefreshErrorState());
    /// <summary>The name of the bound property whose errors this entry shows.</summary>
    public string ColumnName
    {
        get => (string)GetValue(ColumnNameProperty);
        set => SetValue(ColumnNameProperty, value);
    }

    /// <summary>The <see cref="Text"/> property.</summary>
    public static readonly BindableProperty TextProperty = BindableProperty.Create(
            nameof(Text), typeof(string), typeof(RouteableMauiEntry),
            defaultBindingMode: BindingMode.TwoWay,
            propertyChanged: (sender, _, text) =>
            {
                var entry = ((RouteableMauiEntry)sender)._entry;
                if (entry is not null && !string.Equals(entry.Text, (string?)text, StringComparison.Ordinal))
                    entry.Text = (string?)text;
            });
    /// <summary>The text of the entry (two-way).</summary>
    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    /// <summary>The <see cref="UnderlineColor"/> property.</summary>
    public static readonly BindableProperty UnderlineColorProperty = BindableProperty.Create(
            nameof(UnderlineColor), typeof(Color), typeof(RouteableMauiEntry), Colors.Black);
    /// <summary>The color of the underline.</summary>
    public Color UnderlineColor
    {
        get => (Color)GetValue(UnderlineColorProperty);
        set => SetValue(UnderlineColorProperty, value);
    }

    /// <summary>The <see cref="UnderlineThickness"/> property.</summary>
    public static readonly BindableProperty UnderlineThicknessProperty = BindableProperty.Create(
            nameof(UnderlineThickness), typeof(int), typeof(RouteableMauiEntry), 0);
    /// <summary>The thickness of the underline.</summary>
    public int UnderlineThickness
    {
        get => (int)GetValue(UnderlineThicknessProperty);
        set => SetValue(UnderlineThicknessProperty, value);
    }

    // Read-only from the outside: the state of the validation indicator, set from the view model's errors.
    static readonly BindablePropertyKey HasErrorPropertyKey = BindableProperty.CreateReadOnly(
            nameof(HasError), typeof(bool), typeof(RouteableMauiEntry), false);
    /// <summary>The <see cref="HasError"/> property.</summary>
    public static readonly BindableProperty HasErrorProperty = HasErrorPropertyKey.BindableProperty;
    /// <summary>Whether the view model reports an error for <see cref="ColumnName"/>.</summary>
    public bool HasError => (bool)GetValue(HasErrorProperty);

    static readonly BindablePropertyKey ErrorMessagePropertyKey = BindableProperty.CreateReadOnly(
            nameof(ErrorMessage), typeof(string), typeof(RouteableMauiEntry), null);
    /// <summary>The <see cref="ErrorMessage"/> property.</summary>
    public static readonly BindableProperty ErrorMessageProperty = ErrorMessagePropertyKey.BindableProperty;
    /// <summary>The first error the view model reports for <see cref="ColumnName"/>, or <see langword="null"/>.</summary>
    public string? ErrorMessage => (string?)GetValue(ErrorMessageProperty);

    /// <inheritdoc />
    protected override void OnBindingContextChanged()
    {
        base.OnBindingContextChanged();

        Unsubscribe();
        _subscribed = BindingContext as INotifyDataErrorInfo;
        if (Handler is not null)
            Subscribe();

        RefreshErrorState();
    }

    // The view model outlives the entry: it listens to the view model only while the entry is on screen (has a handler),
    // or the view model would keep every entry it was ever bound to.
    /// <inheritdoc />
    protected override void OnHandlerChanging(HandlerChangingEventArgs args)
    {
        base.OnHandlerChanging(args);

        if (args.NewHandler is null)
            Unsubscribe();
        else
            Subscribe();
    }

    private void Subscribe()
    {
        if (_subscribed is null || _following)
            return;

        _subscribed.ErrorsChanged += OnErrorsChanged;
        _following = true;
        RefreshErrorState();
    }

    private void Unsubscribe()
    {
        if (_subscribed is not null && _following)
            _subscribed.ErrorsChanged -= OnErrorsChanged;

        _following = false;
    }

    private bool _following;

    void OnErrorsChanged(object? sender, DataErrorsChangedEventArgs e)
    {
        // PropertyName is null when the source signals "errors may have changed broadly"
        // (e.g. ValidateAllProperties()), so refresh unconditionally in that case too.
        if (e.PropertyName is null || e.PropertyName == ColumnName)
            RefreshErrorState();
    }

    void RefreshErrorState()
    {
        if (_subscribed is null || string.IsNullOrEmpty(ColumnName))
        {
            SetValue(HasErrorPropertyKey, false);
            SetValue(ErrorMessagePropertyKey, null);
            ShowError();
            return;
        }

        var first = _subscribed.GetErrors(ColumnName)?.Cast<object>().FirstOrDefault();
        var message = first switch
        {
            null => null,
            System.ComponentModel.DataAnnotations.ValidationResult vr => vr.ErrorMessage,
            _ => first.ToString(),
        };

        SetValue(HasErrorPropertyKey, message is not null);
        SetValue(ErrorMessagePropertyKey, message);
        ShowError();
    }

    // The visual tree is built once and updated from code: no binding of any kind (a binding by path resolves the property
    // by reflection, which the trimmer and Native AOT cannot follow).
    private Entry? _entry;
    private Border? _frame;
    private Border? _marker;

    private Grid BuildContent()
    {
        var grid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto),
            }
        };

        _frame = new Border { Stroke = null, StrokeThickness = 0 };
        _entry = new Entry { Text = Text };
        _entry.TextChanged += (_, e) =>
        {
            if (!string.Equals(Text, e.NewTextValue, StringComparison.Ordinal))
                SetValue(TextProperty, e.NewTextValue);
        };
        _frame.Content = _entry;
        grid.Children.Add(_frame);

        _marker = new Border
        {
            Margin = new Thickness(8, 0, 0, 0), // gap before the icon; icon only occupies space when shown
            Stroke = null,
            StrokeThickness = 0
        };
        Grid.SetColumn(_marker, 1);
        grid.Children.Add(_marker);
        return grid;
    }

    // Red frame, error icon and its tooltip while the view model reports an error for ColumnName.
    private void ShowError()
    {
        if (_frame is null || _marker is null)
            return;

        if (HasError)
        {
            _frame.Stroke = new SolidColorBrush(Colors.Red);
            _frame.StrokeThickness = 0.5;
            _marker.Content = new Microsoft.Maui.Controls.Shapes.Path
            {
                Data = geometry,
                Fill = new SolidColorBrush(Colors.Transparent),
                Stroke = new SolidColorBrush(Colors.Red),
                StrokeThickness = 2,
                WidthRequest = 20,
                HeightRequest = 20
            };
            ToolTipProperties.SetText(_marker, ErrorMessage ?? string.Empty);
        }
        else
        {
            _frame.Stroke = null;
            _frame.StrokeThickness = 0;
            _marker.Content = null;
            ToolTipProperties.SetText(_marker, string.Empty);
        }
    }
}
