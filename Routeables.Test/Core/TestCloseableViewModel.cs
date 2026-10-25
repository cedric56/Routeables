using Routeables.Core;
using System.Windows.Input;

namespace Routeables.Tests.Core;

/// <summary>
/// Concrete <see cref="CloseableViewModelBase{TResult}"/> used to test the base class's
/// close/command/title behavior in isolation.
/// </summary>
public class TestCloseableViewModel : CloseableViewModelBase<string>
{
    public bool CanCloseResult { get; set; } = true;

    public override ICommand CloseCommand { get; } = new NoopCommand();

    public override string Title => "Test Title";

    public override Task<bool> CanCloseAsync() => Task.FromResult(CanCloseResult);
}

/// <summary>A command that does nothing: the tests need an <see cref="ICommand"/>, not a command library.</summary>
internal sealed class NoopCommand : ICommand
{
    public event EventHandler? CanExecuteChanged { add { } remove { } }

    public bool CanExecute(object? parameter) => true;

    public void Execute(object? parameter) { }
}