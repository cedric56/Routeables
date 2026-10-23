using Routeables.Abstraction;
using Routeables.Maui;
using XAnimals.ViewModels;

namespace XAnimals.Maui.Views;

// {panda::} in the route is an attribute parameter: it lands in the Panda property named here, as a Shell query would.
[RouteableView(typeof(PandaDetailViewModel), route: PandaDetailViewModel.Route)]
[QueryProperty(nameof(Panda), "panda")]
public partial class PandaDetailView : RouteableFor
{
    private string? _panda;

    public PandaDetailView()
    {
        InitializeComponent();
    }

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
    protected override void OnBindingContextChanged()
    {
        base.OnBindingContextChanged();
        Apply();
    }

    private void Apply()
    {
        if (_panda is not null && BindingContext is PandaDetailViewModel viewModel)
            viewModel.Name = _panda;
    }
}
