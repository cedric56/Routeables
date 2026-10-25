using Routeables.Abstraction;
using Routeables.Wpf;
using XAnimals.Birds;

namespace XAnimals.Wpf.Views;

public partial class BirdDetailView : RouteableFor
{
    private string? _bird;

    public BirdDetailView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => Apply();
    }

    /// <summary>The bird, from <c>{bird::}</c> in the address: an attribute parameter lands in this property.</summary>
    [RouteableParameter]
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
    private void Apply()
    {
        if (_bird is not null && DataContext is BirdDetailViewModel viewModel)
            viewModel.Name = _bird;
    }
}
