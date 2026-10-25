using Routeables.Abstraction;
using Routeables.Avalonia;
using Routeables.Avalonia.Services;
using XAnimals.ViewModels;

namespace XAnimals.Avalonia;

// Shown by IServiceProvider.ShowModalAsync<NoteViewModel, string>(); the buttons come from the view model's commands.
[RouteableView(typeof(NoteViewModel))]
public partial class NoteView : DialogView<NoteViewModel, string>
{
    public NoteView()
    {
        InitializeComponent();
    }
}
