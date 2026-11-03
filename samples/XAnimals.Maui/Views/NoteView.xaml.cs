using Routeables.Abstraction;
using Routeables.Maui;
using Routeables.Maui.Services;
using XAnimals.ViewModels;

namespace XAnimals.Maui.Views;

// Shown by IServiceProvider.ShowModalAsync<NoteViewModel, string>(); the buttons come from the view model's commands.
[RouteableView(typeof(NoteViewModel))]
public partial class NoteView : RouteableModalFor<NoteViewModel, string?>
{
    public NoteView()
    {
        InitializeComponent();
    }
}
