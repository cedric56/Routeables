using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Routeables.Abstraction;
using Routeables.WinUI3.Services;
using XAnimals.ViewModels;

namespace XAnimals.WinUI3.Views;

// Shown by IServiceProvider.ShowModalAsync<NoteViewModel, string>(); the buttons come from the view model's commands.
// A WinUI XAML file cannot have a generic root, so this view is written in code.
[RouteableView(typeof(NoteViewModel))]
public partial class NoteView : RouteableModalFor<NoteViewModel, string>
{
    public NoteView()
    {
        var note = new TextBox { PlaceholderText = "Write a note", MinWidth = 280, AcceptsReturn = false };
        note.SetBinding(TextBox.TextProperty, new Binding
        {
            Path = new PropertyPath(nameof(NoteViewModel.Note)),
            Mode = BindingMode.TwoWay,
            UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
        });
        Content = note;
    }
}
