using System.Windows.Input;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Routeables.Avalonia.Services;
using Routeables.Services;
using Xunit;

namespace Routeables.Test.Avalonia;

/// <summary>
/// The dialogs of Avalonia in a real (headless) window: the message boxes, content dialogs and task dialogs of
/// <see cref="DefaultMessageBoxService"/> open as modal windows owned by the window they are asked for, show their texts and buttons,
/// and close with the result of the button, with Escape, from code, or not at all when the dialog vetoes it.
/// </summary>
[Collection("Avalonia headless")]
public sealed class HeadlessDialogTests(HeadlessApp app)
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    private Task RunAsync(Func<Window, DefaultMessageBoxService, Task> test) => app.RunAsync(async () =>
    {
        var owner = new Window { Width = 500, Height = 400, Content = new TextBlock { Text = "owner" } };
        owner.Show();
        Dispatcher.UIThread.RunJobs();
        try
        {
            await test(owner, new DefaultMessageBoxService());
        }
        finally
        {
            foreach (var open in owner.OwnedWindows.ToList()) open.Close();
            owner.Close();
        }
    });

    private static async Task UntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.Add(Timeout);
        while (true)
        {
            Dispatcher.UIThread.RunJobs();
            if (condition()) return;
            if (DateTime.UtcNow > deadline) throw new TimeoutException("The condition was not met in 5 seconds.");
            await Task.Delay(10);
        }
    }

    private static async Task<Window> DialogOfAsync(Window owner, int count = 1)
    {
        await UntilAsync(() => owner.OwnedWindows.Count >= count && Buttons(owner.OwnedWindows[count - 1]).Count > 0);
        return owner.OwnedWindows[count - 1];
    }

    private static List<Button> Buttons(Window window) => [.. window.GetVisualDescendants().OfType<Button>()];

    private static List<string> Texts(Window window) => [.. window.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text ?? string.Empty)];

    private static Button ButtonOf(Window window, string text) => Buttons(window).Single(b => Equals(b.Content, text));

    private static void Click(Button button) => button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

    private static void PressEscape(Window window)
        => window.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Escape });

    private static async Task<T> ResultAsync<T>(Task<T> task)
    {
        await UntilAsync(() => task.IsCompleted);
        return await task;
    }

    private sealed class FlagCommand(bool initial) : ICommand
    {
        private bool _enabled = initial;
        public event EventHandler? CanExecuteChanged;
        public bool CanExecute(object? parameter) => _enabled;
        public void Execute(object? parameter) { }

        public void Set(bool enabled)
        {
            _enabled = enabled;
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    // ------------------------------------------------------------------------------------------------ message boxes

    [Fact]
    public Task A_message_box_shows_its_title_its_message_and_one_button_for_each_word_of_the_buttons() => RunAsync(async (owner, service) =>
    {
        var shown = service.ShowMessageBoxAsync("Delete", "Are you sure?", "YesNoCancel", owner);

        var dialog = await DialogOfAsync(owner);

        Assert.Equal("Delete", dialog.Title);
        Assert.Contains("Are you sure?", Texts(dialog));
        Assert.Equal(["Yes", "No", "Cancel"], Buttons(dialog).Select(b => (string)b.Content!));
        Click(ButtonOf(dialog, "No"));
        Assert.Equal("No", await ResultAsync(shown));
    });

    [Fact]
    public Task The_first_button_is_the_default_one() => RunAsync(async (owner, service) =>
    {
        var shown = service.ShowMessageBoxAsync("t", "m", "YesNo", owner);
        var dialog = await DialogOfAsync(owner);

        Assert.True(ButtonOf(dialog, "Yes").IsDefault);
        Assert.False(ButtonOf(dialog, "No").IsDefault);
        Click(ButtonOf(dialog, "Yes"));
        await ResultAsync(shown);
    });

    [Fact]
    public Task A_message_box_with_no_buttons_offers_Ok() => RunAsync(async (owner, service) =>
    {
        var shown = service.ShowMessageBoxAsync("t", "m", string.Empty, owner);
        var dialog = await DialogOfAsync(owner);

        Assert.Equal(["Ok"], Buttons(dialog).Select(b => (string)b.Content!));
        Click(ButtonOf(dialog, "Ok"));
        Assert.Equal("Ok", await ResultAsync(shown));
    });

    [Fact]
    public Task Escape_dismisses_a_message_box_without_a_result() => RunAsync(async (owner, service) =>
    {
        var shown = service.ShowMessageBoxAsync("t", "m", "YesNo", owner);
        var dialog = await DialogOfAsync(owner);

        PressEscape(dialog);

        Assert.Null(await ResultAsync(shown));
        await UntilAsync(() => owner.OwnedWindows.Count == 0);
    });

    [Fact]
    public Task The_dialog_closes_its_window_when_a_button_is_pressed() => RunAsync(async (owner, service) =>
    {
        var shown = service.ShowMessageBoxAsync("t", "m", "Ok", owner);
        Click(ButtonOf(await DialogOfAsync(owner), "Ok"));

        await ResultAsync(shown);

        await UntilAsync(() => owner.OwnedWindows.Count == 0);
    });

    [Fact]
    public Task A_second_message_box_opens_over_the_first_and_each_gets_its_own_result() => RunAsync(async (owner, service) =>
    {
        var first = service.ShowMessageBoxAsync("first", "m", "Ok", owner);
        var firstDialog = await DialogOfAsync(owner);
        var second = service.ShowMessageBoxAsync("second", "m", "YesNo", owner);
        await UntilAsync(() => firstDialog.OwnedWindows.Count == 1 && Buttons(firstDialog.OwnedWindows[0]).Count > 0);
        var secondDialog = firstDialog.OwnedWindows[0];

        Assert.Equal("second", secondDialog.Title);

        Click(ButtonOf(secondDialog, "Yes"));
        Assert.Equal("Yes", await ResultAsync(second));
        Assert.False(first.IsCompleted);

        Click(ButtonOf(firstDialog, "Ok"));
        Assert.Equal("Ok", await ResultAsync(first));
    });

    [Fact]
    public Task A_long_message_is_shown_in_full() => RunAsync(async (owner, service) =>
    {
        var message = string.Join(' ', Enumerable.Repeat("word", 400));
        var shown = service.ShowMessageBoxAsync("t", message, "Ok", owner);
        var dialog = await DialogOfAsync(owner);

        Assert.Contains(message, Texts(dialog));
        Click(ButtonOf(dialog, "Ok"));
        await ResultAsync(shown);
    });

    // ------------------------------------------------------------------------------------------------ content dialogs

    [Fact]
    public Task A_content_dialog_shows_its_title_its_text_and_its_buttons() => RunAsync(async (owner, service) =>
    {
        var shown = service.ShowContentDialogAsync(new RouteableContentDialog
        {
            Title = "Rename",
            Content = "New name?",
            PrimaryButtonText = "Save",
            IsPrimaryButtonEnabled = true,
            CloseButtonText = "Cancel"
        }, owner);

        var dialog = await DialogOfAsync(owner);

        Assert.Equal("Rename", dialog.Title);
        Assert.Contains("New name?", Texts(dialog));
        Assert.Equal(["Save", "Cancel"], Buttons(dialog).Select(b => (string)b.Content!));
        Click(ButtonOf(dialog, "Save"));
        Assert.Equal(RouteableContentDialogResult.Primary, await ResultAsync(shown));
    });

    [Fact]
    public Task A_content_dialog_shows_a_control_as_it_is() => RunAsync(async (owner, service) =>
    {
        var body = new TextBox { Text = "typed" };
        var shown = service.ShowContentDialogAsync(new RouteableContentDialog { Title = "t", Content = body, CloseButtonText = "Close" }, owner);
        var dialog = await DialogOfAsync(owner);

        Assert.Same(body, dialog.GetVisualDescendants().OfType<TextBox>().Single());
        Click(ButtonOf(dialog, "Close"));
        Assert.Equal(RouteableContentDialogResult.None, await ResultAsync(shown));
    });

    [Fact]
    public Task The_secondary_and_close_buttons_give_their_results() => RunAsync(async (owner, service) =>
    {
        var secondary = service.ShowContentDialogAsync(new RouteableContentDialog
        {
            Title = "t", SecondaryButtonText = "Other", IsSecondaryButtonEnabled = true, CloseButtonText = "Close"
        }, owner);
        var dialog = await DialogOfAsync(owner);
        Click(ButtonOf(dialog, "Other"));
        Assert.Equal(RouteableContentDialogResult.Secondary, await ResultAsync(secondary));
        await UntilAsync(() => owner.OwnedWindows.Count == 0);

        var close = service.ShowContentDialogAsync(new RouteableContentDialog { Title = "t", CloseButtonText = "Close" }, owner);
        Click(ButtonOf(await DialogOfAsync(owner), "Close"));
        Assert.Equal(RouteableContentDialogResult.None, await ResultAsync(close));
    });

    [Fact]
    public Task A_disabled_primary_button_cannot_be_pressed() => RunAsync(async (owner, service) =>
    {
        var shown = service.ShowContentDialogAsync(new RouteableContentDialog
        {
            Title = "t", PrimaryButtonText = "Save", IsPrimaryButtonEnabled = false, CloseButtonText = "Close"
        }, owner);
        var dialog = await DialogOfAsync(owner);

        Assert.False(ButtonOf(dialog, "Save").IsEnabled);
        Click(ButtonOf(dialog, "Close"));
        await ResultAsync(shown);
    });

    [Fact]
    public Task A_button_follows_its_command() => RunAsync(async (owner, service) =>
    {
        var command = new FlagCommand(false);
        var shown = service.ShowContentDialogAsync(new RouteableContentDialog
        {
            Title = "t", PrimaryButtonText = "Save", IsPrimaryButtonEnabled = true, PrimaryButtonCommand = command, CloseButtonText = "Close"
        }, owner);
        var dialog = await DialogOfAsync(owner);
        var save = ButtonOf(dialog, "Save");
        Assert.False(save.IsEnabled);

        command.Set(true);
        await UntilAsync(() => save.IsEnabled);

        command.Set(false);
        await UntilAsync(() => !save.IsEnabled);
        Click(ButtonOf(dialog, "Close"));
        await ResultAsync(shown);
    });

    [Fact]
    public Task The_callbacks_of_a_content_dialog_run_once_each_in_order() => RunAsync(async (owner, service) =>
    {
        var events = new List<string>();
        var shown = service.ShowContentDialogAsync(new RouteableContentDialog
        {
            Title = "t",
            CloseButtonText = "Close",
            Opening = () => events.Add("opening"),
            Opened = () => events.Add("opened"),
            Closed = () => events.Add("closed")
        }, owner);
        var dialog = await DialogOfAsync(owner);
        await UntilAsync(() => events.Contains("opened"));

        Click(ButtonOf(dialog, "Close"));
        await ResultAsync(shown);

        Assert.Equal(["opening", "opened", "closed"], events);
    });

    [Fact]
    public Task Escape_dismisses_a_content_dialog_unless_it_vetoes_it() => RunAsync(async (owner, service) =>
    {
        var veto = true;
        var shown = service.ShowContentDialogAsync(new RouteableContentDialog
        {
            Title = "t", CloseButtonText = "Close", Closing = _ => veto
        }, owner);
        var dialog = await DialogOfAsync(owner);

        PressEscape(dialog);
        Dispatcher.UIThread.RunJobs();
        Assert.False(shown.IsCompleted);
        Assert.Single(owner.OwnedWindows);

        veto = false;
        PressEscape(dialog);

        Assert.Equal(RouteableContentDialogResult.None, await ResultAsync(shown));
    });

    [Fact]
    public Task The_caller_can_close_a_content_dialog_from_code_and_from_another_thread() => RunAsync(async (owner, service) =>
    {
        var dialogParameters = new RouteableContentDialog { Title = "t", CloseButtonText = "Close" };
        var shown = service.ShowContentDialogAsync(dialogParameters, owner);
        await DialogOfAsync(owner);
        Assert.NotNull(dialogParameters.Close);

        await Task.Run(() => dialogParameters.Close!());

        Assert.Equal(RouteableContentDialogResult.None, await ResultAsync(shown));
        Assert.Null(dialogParameters.Close);
    });

    [Fact]
    public Task A_content_dialog_that_wants_the_full_size_is_wider() => RunAsync(async (owner, service) =>
    {
        var shown = service.ShowContentDialogAsync(new RouteableContentDialog { Title = "t", CloseButtonText = "Close", FullSizeDesired = true }, owner);
        var dialog = await DialogOfAsync(owner);

        var card = dialog.GetVisualDescendants().OfType<StackPanel>().First(p => p.MinWidth >= 640);

        Assert.True(card.MinWidth >= 640);
        Click(ButtonOf(dialog, "Close"));
        await ResultAsync(shown);
    });

    [Fact]
    public async Task A_content_dialog_refuses_no_parameters()
        => await Assert.ThrowsAsync<ArgumentNullException>(() => new DefaultMessageBoxService().ShowContentDialogAsync(null!));

    // ------------------------------------------------------------------------------------------------ task dialogs

    [Fact]
    public Task A_task_dialog_shows_its_header_its_sub_header_and_one_button_for_each_result() => RunAsync(async (owner, service) =>
    {
        var shown = service.ShowTaskDialogAsync(
            new RouteableTaskDialog { Title = "Update", Header = "A new version", SubHeader = "Install it now?" },
            owner, RouteableTaskDialogResult.Yes, RouteableTaskDialogResult.Cancel);

        var dialog = await DialogOfAsync(owner);

        Assert.Equal("Update", dialog.Title);
        Assert.Contains("A new version", Texts(dialog));
        Assert.Contains("Install it now?", Texts(dialog));
        Assert.Equal(["Yes", "Cancel"], Buttons(dialog).Select(b => (string)b.Content!));
        Assert.True(ButtonOf(dialog, "Yes").IsDefault);
        Click(ButtonOf(dialog, "Yes"));
        Assert.Equal(RouteableTaskDialogResult.Yes, await ResultAsync(shown));
    });

    [Fact]
    public Task A_task_dialog_with_no_results_offers_Close() => RunAsync(async (owner, service) =>
    {
        var shown = service.ShowTaskDialogAsync(new RouteableTaskDialog { Title = "t", Header = "h" }, owner);
        var dialog = await DialogOfAsync(owner);

        Assert.Equal(["Close"], Buttons(dialog).Select(b => (string)b.Content!));
        Click(ButtonOf(dialog, "Close"));
        Assert.Equal(RouteableTaskDialogResult.Close, await ResultAsync(shown));
    });

    [Fact]
    public Task Escape_dismisses_a_task_dialog_with_None_unless_it_vetoes_it() => RunAsync(async (owner, service) =>
    {
        var veto = true;
        var shown = service.ShowTaskDialogAsync(new RouteableTaskDialog { Title = "t", Header = "h", Closing = () => veto }, owner);
        var dialog = await DialogOfAsync(owner);

        PressEscape(dialog);
        Dispatcher.UIThread.RunJobs();
        Assert.False(shown.IsCompleted);

        veto = false;
        PressEscape(dialog);

        Assert.Equal(RouteableTaskDialogResult.None, await ResultAsync(shown));
    });

    [Fact]
    public Task The_callbacks_of_a_task_dialog_run_once_each_in_order() => RunAsync(async (owner, service) =>
    {
        var events = new List<string>();
        var shown = service.ShowTaskDialogAsync(new RouteableTaskDialog
        {
            Title = "t",
            Header = "h",
            Opening = () => events.Add("opening"),
            Opened = () => events.Add("opened"),
            Closed = () => events.Add("closed")
        }, owner);
        var dialog = await DialogOfAsync(owner);
        await UntilAsync(() => events.Contains("opened"));

        Click(ButtonOf(dialog, "Close"));
        await ResultAsync(shown);

        Assert.Equal(["opening", "opened", "closed"], events);
    });

    [Fact]
    public async Task A_task_dialog_refuses_no_parameters()
        => await Assert.ThrowsAsync<ArgumentNullException>(() => new DefaultMessageBoxService().ShowTaskDialogAsync(null!));
}
