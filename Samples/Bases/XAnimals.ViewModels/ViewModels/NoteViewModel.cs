using Routeables.Abstraction;
using System.Collections.ObjectModel;
using System.Windows.Input;
using XAminals.Common;
using XAnimals.Common;

namespace XAnimals.ViewModels;

/// <summary>
/// A modal that asks for a short note and returns it, or <see langword="null"/> when cancelled.
/// Each platform's view shows it in its own modal; the buttons come from <see cref="Commands"/>.
/// </summary>
public class NoteViewModel : ViewModelBase, ICloseableViewModel<string?>
{
    private string _note = string.Empty;

    public NoteViewModel()
    {
        Commands =
        [
            new RouteableCommand { Name = "Save", Command = new AsyncCommand(async () => await CloseAsync(_note.Trim()), () => _note.Trim().Length > 0) },
            new RouteableCommand { Name = "Cancel", Command = new AsyncCommand(async () => await CloseAsync(null)) }
        ];
        _save = Commands[0].Command;
    }

    private readonly ICommand _save;

    public string Title => "Add a note";

    /// <summary>The text typed by the user.</summary>
    public string Note
    {
        get => _note;
        set
        {
            if (_note == value) return;
            _note = value;
            OnPropertyChanged();
            (_save as AsyncCommand)?.RaiseCanExecuteChanged();
        }
    }

    public ObservableCollection<RouteableCommand> Commands { get; }

    public ICommand? CloseCommand => null;

    public event EventHandler<string?>? CloseRequested;

    public Task CloseAsync(string? value)
    {
        CloseRequested?.Invoke(this, value);
        return Task.CompletedTask;
    }
}
