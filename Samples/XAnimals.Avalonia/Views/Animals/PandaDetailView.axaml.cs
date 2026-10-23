using Avalonia.Controls;
using Routeables.Abstraction;
using Routeables.Avalonia;
using System;
using XAnimals.ViewModels;

namespace XAnimals.Avalonia;

[RouteableView(typeof(PandaDetailViewModel), route: PandaDetailViewModel.Route)]
public partial class PandaDetailView : RouteableFor<PandaDetailViewModel>
{
    private string? _panda;

    public PandaDetailView()
    {
        InitializeComponent();
    }

    /// <summary>The panda, from <c>{panda::}</c> in the address: an attribute parameter lands in this property.</summary>
    [RouteableParameter]
    public string? Panda
    {
        get => _panda;
        set
        {
            _panda = value;
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
        if (_panda is not null && DataContext is PandaDetailViewModel viewModel)
            viewModel.Name = _panda;
    }
}
