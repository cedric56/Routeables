using System.Windows.Input;
using Routeables.Services;

namespace Routeables.Tests.Services;

public class DialogSessionTests
{
    private sealed class Command(List<string> log, bool canExecute = true) : ICommand
    {
        public event EventHandler? CanExecuteChanged { add { } remove { } }

        public bool CanExecute(object? parameter) => canExecute;

        public void Execute(object? parameter) => log.Add("command:" + parameter);
    }

    // ----- content dialog

    [Fact]
    public void Content_buttons_follow_the_order_primary_secondary_close_and_skip_the_ones_without_text()
    {
        var dialog = new RouteableContentDialog { PrimaryButtonText = "Save", CloseButtonText = "Cancel", IsPrimaryButtonEnabled = false };
        var session = new RouteableContentDialogSession(dialog, () => { });

        Assert.Equal(["Save", "Cancel"], session.Buttons.Select(b => b.Text));
        Assert.Equal([true, false], session.Buttons.Select(b => b.IsDefault));
        Assert.False(session.Buttons[0].IsEnabled);
        Assert.True(session.Buttons[1].IsEnabled);
    }

    [Fact]
    public void Pressing_a_button_runs_the_command_then_asks_Closing_then_completes()
    {
        var log = new List<string>();
        var dialog = new RouteableContentDialog
        {
            PrimaryButtonText = "Ok",
            PrimaryButtonCommand = new Command(log),
            PrimaryButtonCommandParameter = 42,
            PrimaryButtonClick = () => log.Add("click"),
            Closing = result => { log.Add("closing:" + result); return false; },
        };
        var session = new RouteableContentDialogSession(dialog, () => log.Add("complete"));

        Assert.True(session.Buttons[0].Press());

        Assert.Equal(["command:42", "closing:Primary", "complete"], log);
        Assert.Equal(RouteableContentDialogResult.Primary, session.Result);
    }

    [Fact]
    public void Without_a_command_the_click_callback_runs()
    {
        var log = new List<string>();
        var dialog = new RouteableContentDialog { SecondaryButtonText = "No", SecondaryButtonClick = () => log.Add("click") };
        var session = new RouteableContentDialogSession(dialog, () => log.Add("complete"));

        session.Buttons[0].Press();

        Assert.Equal(["click", "complete"], log);
        Assert.Equal(RouteableContentDialogResult.Secondary, session.Result);
    }

    [Fact]
    public void A_command_that_cannot_execute_makes_the_press_do_nothing()
    {
        var log = new List<string>();
        var dialog = new RouteableContentDialog { PrimaryButtonText = "Ok", PrimaryButtonCommand = new Command(log, canExecute: false) };
        var session = new RouteableContentDialogSession(dialog, () => log.Add("complete"));

        Assert.False(session.Buttons[0].Press());

        Assert.Empty(log);
        Assert.Equal(RouteableContentDialogResult.None, session.Result);
    }

    [Fact]
    public void Closing_returning_true_keeps_the_dialog_open_and_the_result_unchanged()
    {
        var completed = false;
        var dialog = new RouteableContentDialog { PrimaryButtonText = "Ok", Closing = _ => true };
        var session = new RouteableContentDialogSession(dialog, () => completed = true);

        Assert.False(session.Buttons[0].Press());

        Assert.False(completed);
        Assert.Equal(RouteableContentDialogResult.None, session.Result);
    }

    [Fact]
    public void Dismissing_asks_Closing_with_None_and_can_be_vetoed()
    {
        string? asked = null;
        var veto = true;
        var dialog = new RouteableContentDialog { Closing = r => { asked = r; return veto; } };
        var session = new RouteableContentDialogSession(dialog, () => { });

        Assert.False(session.TryDismiss());
        veto = false;
        Assert.True(session.TryDismiss());

        Assert.Equal("None", asked);
        Assert.Equal(RouteableContentDialogResult.None, session.Result);
    }

    [Fact]
    public void The_caller_can_close_the_dialog_from_code_until_it_is_released()
    {
        var closed = 0;
        var completed = 0;
        var dialog = new RouteableContentDialog { PrimaryButtonText = "Ok", Closed = () => closed++ };
        var session = new RouteableContentDialogSession(dialog, () => completed++);
        var ranOnUi = 0;
        session.AllowCloseFromCode(action => { ranOnUi++; action(); });

        Assert.NotNull(dialog.Close);
        dialog.Close!();

        Assert.Equal((1, 1), (ranOnUi, completed));
        Assert.Equal(RouteableContentDialogResult.None, session.Result);

        session.Release();
        Assert.Null(dialog.Close);
        Assert.Equal(1, closed);
    }

    private sealed class ThrowingCommand : ICommand
    {
        public event EventHandler? CanExecuteChanged { add { } remove { } }
        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter) => throw new InvalidOperationException("no");
    }

    [Fact]
    public void A_command_that_throws_is_contained_and_the_dialog_still_closes()
    {
        var completed = 0;
        var dialog = new RouteableContentDialog { PrimaryButtonText = "Ok", PrimaryButtonCommand = new ThrowingCommand() };
        var session = new RouteableContentDialogSession(dialog, () => completed++);

        Assert.True(session.Buttons[0].Press());

        Assert.Equal(1, completed);
        Assert.Equal(RouteableContentDialogResult.Primary, session.Result);
    }

    [Fact]
    public void Opening_and_Opened_are_told_and_a_throwing_handler_is_contained()
    {
        var log = new List<string>();
        var dialog = new RouteableContentDialog { Opening = () => log.Add("opening"), Opened = () => throw new InvalidOperationException("no") };
        var session = new RouteableContentDialogSession(dialog, () => { });

        session.Opening();
        session.Opened();

        Assert.Equal(["opening"], log);
    }

    // ----- task dialog

    [Fact]
    public void A_task_dialog_without_results_offers_Close_and_skips_None()
    {
        var none = new RouteableTaskDialogSession(new RouteableTaskDialog(), null, () => { });
        Assert.Equal(["Close"], none.Buttons.Select(b => b.Text));

        var many = new RouteableTaskDialogSession(
            new RouteableTaskDialog(), [RouteableTaskDialogResult.Yes, RouteableTaskDialogResult.None, RouteableTaskDialogResult.No], () => { });
        Assert.Equal(["Yes", "No"], many.Buttons.Select(b => b.Text));
        Assert.Equal([true, false], many.Buttons.Select(b => b.IsDefault));
    }

    [Fact]
    public void A_task_button_completes_with_its_result_unless_Closing_vetoes()
    {
        var veto = true;
        var completed = 0;
        var dialog = new RouteableTaskDialog { Closing = () => veto };
        var session = new RouteableTaskDialogSession(dialog, [RouteableTaskDialogResult.Yes], () => completed++);

        Assert.False(session.Buttons[0].Press());
        Assert.Equal((0, RouteableTaskDialogResult.None), (completed, session.Result));

        veto = false;
        Assert.True(session.Buttons[0].Press());
        Assert.Equal((1, RouteableTaskDialogResult.Yes), (completed, session.Result));
    }

    [Fact]
    public void A_task_dialog_dismissed_gives_None_and_Release_tells_Closed()
    {
        var closed = false;
        var dialog = new RouteableTaskDialog { Closed = () => closed = true };
        var session = new RouteableTaskDialogSession(dialog, [RouteableTaskDialogResult.Yes], () => { });

        Assert.True(session.TryDismiss());
        session.Release();

        Assert.Equal(RouteableTaskDialogResult.None, session.Result);
        Assert.True(closed);
    }
}
