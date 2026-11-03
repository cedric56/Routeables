using System.Collections.ObjectModel;
using System.Windows.Input;
using Routeables.Abstraction;
using Routeables.Services;

namespace Routeables.Tests.Services;

public class ModalSessionTests
{
    private sealed class Command(bool canExecute = true) : ICommand
    {
        public event EventHandler? CanExecuteChanged { add { } remove { } }

        public bool CanExecute(object? parameter) => canExecute;

        public void Execute(object? parameter) { }
    }

    private sealed class ViewModel(params RouteableCommand[] commands) : ICloseableViewModel<string?>
    {
        public string Title => "Edit";

        public ObservableCollection<RouteableCommand> Commands { get; } = new(commands);

        public ICommand? CloseCommand => null;

        public event EventHandler<string?>? CloseRequested;

        public bool HasListener => CloseRequested is not null;

        public Task CloseAsync(string? value)
        {
            CloseRequested?.Invoke(this, value);
            return Task.CompletedTask;
        }
    }

    private static RouteableCommand Named(string name, bool canExecute = true) => new() { Name = name, Command = new Command(canExecute) };

    [Fact]
    public void The_commands_become_primary_secondary_and_close()
    {
        var vm = new ViewModel(Named("Save"), Named("Reset"), Named("Cancel"));
        using var session = new RouteableModalSession<string>(vm, "view");

        var dialog = session.Dialog;
        Assert.Equal(("Edit", "view"), (dialog.Title, (string?)dialog.Content));
        Assert.Equal(("Save", "Reset", "Cancel"), (dialog.PrimaryButtonText, dialog.SecondaryButtonText, dialog.CloseButtonText));
    }

    [Fact]
    public void With_more_than_two_commands_the_last_one_is_the_close_button()
    {
        var vm = new ViewModel(Named("A"), Named("B"), Named("C"), Named("D"));
        using var session = new RouteableModalSession<string>(vm, null);

        Assert.Equal(("A", "B", "D"), (session.Dialog.PrimaryButtonText, session.Dialog.SecondaryButtonText, session.Dialog.CloseButtonText));
    }

    [Fact]
    public void A_view_model_without_commands_gets_a_Close_button()
    {
        using var session = new RouteableModalSession<string>(new ViewModel(), null);

        Assert.Equal("Close", session.Dialog.CloseButtonText);
        Assert.Null(session.Dialog.PrimaryButtonText);
    }

    [Fact]
    public void Primary_and_secondary_stay_open_until_the_view_model_asks_to_close()
    {
        var vm = new ViewModel(Named("Save"), Named("Reset"));
        using var session = new RouteableModalSession<string>(vm, null);
        var closing = session.Dialog.Closing!;

        Assert.True(closing("Primary"));
        Assert.True(closing("Secondary"));

        vm.CloseAsync("done");

        Assert.False(closing("Primary"));
        Assert.False(closing("Secondary"));
        Assert.Equal(("done", true), (session.Result, session.CloseRequested));
    }

    [Fact]
    public void The_close_button_closes_unless_its_command_cannot_execute()
    {
        var open = new ViewModel(Named("A"), Named("B"), Named("C"));
        using var allowed = new RouteableModalSession<string>(open, null);
        Assert.False(allowed.Dialog.Closing!("None"));

        var blocked = new ViewModel(Named("A"), Named("B"), Named("C", canExecute: false));
        using var vetoed = new RouteableModalSession<string>(blocked, null);
        Assert.True(vetoed.Dialog.Closing!("None"));
    }

    [Fact]
    public void A_close_request_closes_the_dialog_from_code_and_dispose_stops_listening()
    {
        var vm = new ViewModel(Named("Save"));
        var closedFromCode = 0;
        var session = new RouteableModalSession<string>(vm, null);
        session.Dialog.Close = () => closedFromCode++;

        vm.CloseAsync("x");
        Assert.Equal(1, closedFromCode);

        session.Dispose();
        Assert.False(vm.HasListener);
    }
}
