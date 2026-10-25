using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Routeables.Abstraction;
using Routeables.Wpf.Services;
using XAnimals.ViewModels;

namespace XAnimals.Wpf.Views;

// Shown by IServiceProvider.ShowModalAsync<NoteViewModel, string>(); the buttons come from the view model's commands.
// A XAML file cannot have a generic root, so this view is written in code.
[RouteableView(typeof(NoteViewModel))]
public class NoteView : DialogView<NoteViewModel, string>
{
    public NoteView()
    {
        var label = new TextBlock { Text = "Write a note", Margin = new Thickness(0, 0, 0, 6) };
        var note = new TextBox { MinWidth = 280 };
        note.SetBinding(TextBox.TextProperty, new Binding(nameof(NoteViewModel.Note))
        {
            Mode = BindingMode.TwoWay,
            UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
        });

        var panel = new StackPanel();
        panel.Children.Add(label);
        panel.Children.Add(note);
        Content = panel;
    }
}
