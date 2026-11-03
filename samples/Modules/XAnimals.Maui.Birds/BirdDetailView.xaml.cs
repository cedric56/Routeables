using Routeables.Abstraction;
using Routeables.Maui;
using XAnimals.Birds;

namespace XAnimals.Maui.Views;

// {bird::} in the route is an attribute parameter: it lands in the Bird property named here, as a Shell query would.
[QueryProperty(nameof(Bird), "bird")]
public partial class BirdDetailView : RouteableFor
{
    private string? _bird;

    public BirdDetailView()
    {
        InitializeComponent();
    }

    public string? Bird
    {
        get => _bird;
        set
        {
            _bird = value;
            Apply();
        }
    }

    // The view is injected before its view model is set: give the name over once both are there.
    protected override void OnBindingContextChanged()
    {
        base.OnBindingContextChanged();
        Apply();
    }

    private void Apply()
    {
        if (_bird is not null && BindingContext is BirdDetailViewModel viewModel)
            viewModel.Name = _bird;
    }
}
