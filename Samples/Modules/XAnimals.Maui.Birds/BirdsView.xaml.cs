using Routeables.Abstraction;
using Routeables.Maui;
using XAnimals.Birds;
using XAnimals.Maui.Birds;

namespace XAnimals.Maui.Views;

public partial class BirdsView : RouteableFor
{
    public BirdsView()
    {
        InitializeComponent();
    }

    // The module of this platform: its page is registered when it is loaded.
    private void OnLoadModule(object? sender, EventArgs e)
    {
        if (BindingContext is not BirdsViewModel viewModel) return;
        viewModel.LoadMoreBirds(services => services.AddMauiMoreBirdsModule());
        LoadButton.Text = "Module loaded";
    }
}
