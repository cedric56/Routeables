using System.Windows.Input;

namespace XAnimals.Common;

/// <summary>A command that runs an asynchronous action. An exception is traced: from an <c>async void</c> it would end the application.</summary>
public sealed class AsyncCommand(Func<Task> execute, Func<bool>? canExecute = null) : ICommand
{
    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter) => canExecute?.Invoke() ?? true;

    /// <summary>Runs the command and returns when it is done, for a host that must wait for it (a web request answers once the page has changed). Unlike <see cref="Execute"/>, an exception reaches the caller.</summary>
    public Task ExecuteAsync() => execute();

    public async void Execute(object? parameter)
    {
        try { await execute(); }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[XAnimals] {ex}"); }
    }

    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
