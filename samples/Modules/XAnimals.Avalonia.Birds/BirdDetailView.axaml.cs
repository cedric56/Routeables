using Avalonia.Controls;
using Routeables.Abstraction;
using Routeables.Avalonia;
using System;
using XAnimals.Birds;

namespace XAnimals.Avalonia;

public partial class BirdDetailView : RouteableFor<BirdDetailViewModel>
{
    private string? _bird;

    public BirdDetailView()
    {
        InitializeComponent();
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
    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        Apply();
    }

    private void Apply()
    {
        if (_bird is not null && DataContext is BirdDetailViewModel viewModel)
            viewModel.Name = _bird;
    }
}
