using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Routeables.Abstraction;
using Routeables.Uno;
using XAnimals.ViewModels;

namespace XAnimals.Uno.Views;

public sealed partial class PandaDetailView : RouteableFor
{
    private string? _panda;

    public PandaDetailView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => Apply();
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
    private void Apply()
    {
        if (_panda is not null && DataContext is PandaDetailViewModel viewModel)
            viewModel.Name = _panda;
    }
}
