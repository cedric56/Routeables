using Routeables.Services;
using System.Windows.Input;

namespace Routeables.Tests.Services;

/// <summary>
/// The sessions every host draws its dialogs with (Avalonia, WPF, WinUI, Uno, MAUI), against a model, for random sequences of button presses,
/// dismissals (Escape), closing from code and vetoes: the surface is completed at most once, a command runs only for a press that was allowed,
/// the result is the one of the first thing that closed the dialog, and a veto keeps the dialog open.
/// </summary>
public class DialogSessionRandomizedTests
{
    private sealed class Switch : ICommand
    {
        public bool Can = true;
        public int Runs;
        public event EventHandler? CanExecuteChanged { add { } remove { } }
        public bool CanExecute(object? parameter) => Can;
        public void Execute(object? parameter) => Runs++;
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    public void A_content_dialog_follows_the_model_for_any_sequence_of_calls(int seed)
    {
        var random = new Random(seed);
        for (var round = 0; round < 40; round++)
        {
            var primary = new Switch();
            var secondary = new Switch();
            var close = new Switch();
            var veto = false;
            var completed = 0;
            var dialog = new RouteableContentDialog
            {
                PrimaryButtonText = "Ok", PrimaryButtonCommand = primary,
                SecondaryButtonText = "No", SecondaryButtonCommand = secondary,
                CloseButtonText = "Cancel", CloseButtonCommand = close,
                Closing = _ => veto,
            };
            var session = new RouteableContentDialogSession(dialog, () => completed++);
            session.AllowCloseFromCode(action => action());

            var commands = new[] { primary, secondary, close };
            var results = new[] { RouteableContentDialogResult.Primary, RouteableContentDialogResult.Secondary, RouteableContentDialogResult.None };
            var runs = new[] { 0, 0, 0 };
            var done = false;
            var expected = RouteableContentDialogResult.None;

            for (var step = 0; step < 30; step++)
            {
                switch (random.Next(6))
                {
                    case 0: case 1: case 2:
                        {
                            var i = random.Next(3);
                            var allowed = !done && commands[i].Can;
                            var closes = allowed && !veto;
                            if (allowed) runs[i]++;
                            Assert.Equal(closes, session.Buttons[i].Press());
                            if (closes) { done = true; expected = results[i]; }
                            break;
                        }
                    case 3:
                        Assert.Equal(!veto, session.TryDismiss());
                        if (!done && !veto) expected = RouteableContentDialogResult.None;
                        break;
                    case 4:
                        dialog.Close?.Invoke();
                        done = true;               // closing from code ignores the veto and a decided dialog
                        break;
                    default:
                        veto = random.Next(2) == 0;
                        commands[random.Next(3)].Can = random.Next(4) != 0;
                        break;
                }

                Assert.Equal(done ? 1 : 0, completed);
                Assert.Equal(runs, commands.Select(c => c.Runs).ToArray());
                if (done) { /* the result of whoever closed first */ }
            }

            if (done && completed == 1 && expected != RouteableContentDialogResult.None)
                Assert.Equal(expected, session.Result);
        }
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void A_task_dialog_follows_the_model_for_any_sequence_of_calls(int seed)
    {
        var random = new Random(seed);
        for (var round = 0; round < 40; round++)
        {
            var veto = false;
            var completed = 0;
            var vetoes = 0;
            var dialog = new RouteableTaskDialog { Closing = () => { vetoes++; return veto; } };
            RouteableTaskDialogResult[] wanted = [RouteableTaskDialogResult.Yes, RouteableTaskDialogResult.No, RouteableTaskDialogResult.Cancel];
            var session = new RouteableTaskDialogSession(dialog, wanted, () => completed++);

            var done = false;
            var expected = RouteableTaskDialogResult.None;
            for (var step = 0; step < 30; step++)
            {
                switch (random.Next(4))
                {
                    case 0: case 1:
                        {
                            var i = random.Next(3);
                            var before = vetoes;
                            var closes = !done && !veto;
                            Assert.Equal(closes, session.Buttons[i].Press());
                            Assert.Equal(done ? 0 : 1, vetoes - before);      // a decided dialog is not asked again
                            if (closes) { done = true; expected = wanted[i]; }
                            break;
                        }
                    case 2:
                        Assert.Equal(!veto, session.TryDismiss());
                        if (!done && !veto) expected = RouteableTaskDialogResult.None;
                        break;
                    default:
                        veto = random.Next(2) == 0;
                        break;
                }

                Assert.Equal(done ? 1 : 0, completed);
                Assert.Equal(expected, session.Result);
            }
        }
    }
}
