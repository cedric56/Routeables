using System.Windows.Input;

namespace XAnimals.ViewModels;

/// <summary>A command that runs an asynchronous action. An exception is traced: from an <c>async void</c> it would end the application.</summary>
internal sealed class AsyncCommand(Func<Task> run) : ICommand
{
    public event EventHandler? CanExecuteChanged { add { } remove { } }

    public bool CanExecute(object? parameter) => true;

    public async void Execute(object? parameter)
    {
        try { await run(); }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[XAnimals] {ex}"); }
    }
}
