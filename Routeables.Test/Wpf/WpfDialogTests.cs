using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using Routeables.Abstraction;
using Routeables.Services;
using Routeables.Wpf;
using Routeables.Wpf.Services;
using Window = System.Windows.Window;
using static Routeables.Tests.Wpf.WpfTestWindows;
using Button = System.Windows.Controls.Button;
using ProgressBar = System.Windows.Controls.ProgressBar;

namespace Routeables.Tests.Wpf;

/// <summary>
/// The dialogs of the WPF host: a message box, a content dialog, a task dialog, and the modal of a view. A dialog is a window that is
/// shown from a dispatcher callback; the tests press its buttons the way a click would, and look at what the caller gets.
/// </summary>
[Collection(WpfCollection.Name)]
public class WpfDialogTests
{
    private static readonly DefaultMessageBoxService Box = new();

    private sealed class ActionCommand(Action run, Func<bool>? canExecute = null) : ICommand
    {
        public event EventHandler? CanExecuteChanged;

        public bool CanExecute(object? parameter) => canExecute?.Invoke() ?? true;

        public void Execute(object? parameter) => run();

        public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }

    // Runs a test with an owner window and cleans up whatever the test left open.
    private static async Task WithOwnerAsync(Func<Window, Task> test, bool visible = true)
    {
        var owner = await ShowOwnerAsync(visible);
        try
        {
            await test(owner);
        }
        finally
        {
            await CleanUpAsync(owner);
        }
    }

    // ---- message box ----

    [Theory]
    [InlineData("Yes", 0)]
    [InlineData("No", 1)]
    public Task A_message_box_gives_the_index_of_the_button_that_was_pressed(string pressed, int index) => WithOwnerAsync(async owner =>
    {
        var ct = TestContext.Current.CancellationToken;
        var service = new RouteableDialogService(Box);
        var pending = await StartAsync(() => service.ShowMessageAsync("Keep it?", "Question", "YesNo"));
        var dialog = await WaitForWindowAsync("Question", ct);

        Assert.Equal(new[] { "Yes", "No" }, await ButtonsAsync(dialog));
        Assert.Contains("Keep it?", await TextsAsync(dialog));
        // The first button answers the Enter key.
        Assert.True(await IsDefaultAsync(dialog, "Yes"));
        Assert.False(await IsDefaultAsync(dialog, "No"));

        await ClickAsync(dialog, pressed);

        Assert.Equal(index, await pending);
        Assert.False(await IsOpenAsync("Question"));
    });

    [Fact]
    public Task A_message_box_closed_by_its_window_gives_minus_one() => WithOwnerAsync(async owner =>
    {
        var service = new RouteableDialogService(Box);
        var pending = await StartAsync(() => service.ShowMessageAsync("text", "Closed", "YesNo"));
        var dialog = await WaitForWindowAsync("Closed", TestContext.Current.CancellationToken);

        await CloseAsync(dialog);

        Assert.Equal(-1, await pending);
    });

    [Fact]
    public Task Escape_dismisses_a_message_box() => WithOwnerAsync(async owner =>
    {
        var service = new RouteableDialogService(Box);
        var pending = await StartAsync(() => service.ShowMessageAsync("text", "Escaped", "OkCancel"));
        var dialog = await WaitForWindowAsync("Escaped", TestContext.Current.CancellationToken);

        await PressEscapeAsync(dialog);

        Assert.Equal(-1, await pending);
    });

    [Fact]
    public Task A_dialog_asked_while_another_is_open_appears_over_it_and_both_answer() => WithOwnerAsync(async owner =>
    {
        var ct = TestContext.Current.CancellationToken;
        var first = await StartAsync(() => Box.ShowMessageBoxAsync("One", "first", "Ok", owner));
        var firstDialog = await WaitForWindowAsync("One", ct);
        var second = await StartAsync(() => Box.ShowMessageBoxAsync("Two", "second", "Ok", owner));
        var secondDialog = await WaitForWindowAsync("Two", ct);

        // A window cannot own two modal windows: the second belongs to the first, so both stay blocked in order.
        Assert.Same(firstDialog, await WpfHarness.Run(() => secondDialog.Owner));
        Assert.Same(owner, await WpfHarness.Run(() => firstDialog.Owner));

        await ClickAsync(secondDialog, "Ok");
        Assert.Equal("Ok", await second);
        Assert.False(first.IsCompleted);

        await ClickAsync(firstDialog, "Ok");
        Assert.Equal("Ok", await first);
    });

    [Fact]
    public Task A_dialog_over_a_window_that_was_never_shown_is_shown_on_the_screen() => WithOwnerAsync(async owner =>
    {
        var pending = await StartAsync(() => Box.ShowMessageBoxAsync("Alone", "text", "Ok", owner));
        var dialog = await WaitForWindowAsync("Alone", TestContext.Current.CancellationToken);

        Assert.Null(await WpfHarness.Run(() => dialog.Owner));
        Assert.Equal(WindowStartupLocation.CenterScreen, await WpfHarness.Run(() => dialog.WindowStartupLocation));

        await ClickAsync(dialog, "Ok");
        Assert.Equal("Ok", await pending);
    }, visible: false);

    // ---- content dialog ----

    private static RouteableContentDialog Content(List<string> log, Func<bool>? keepOpen = null, string title = "Content") => new()
    {
        Title = title,
        Content = "hello",
        PrimaryButtonText = "Save",
        IsPrimaryButtonEnabled = true,
        SecondaryButtonText = "Discard",
        IsSecondaryButtonEnabled = true,
        CloseButtonText = "Cancel",
        Opening = () => log.Add("opening"),
        Opened = () => log.Add("opened"),
        Closed = () => log.Add("closed"),
        PrimaryButtonClick = () => log.Add("primary"),
        SecondaryButtonClick = () => log.Add("secondary"),
        CloseButtonClick = () => log.Add("close"),
        Closing = button => { log.Add("closing " + button); return keepOpen?.Invoke() ?? false; }
    };

    [Theory]
    [InlineData("Save", RouteableContentDialogResult.Primary, "primary")]
    [InlineData("Discard", RouteableContentDialogResult.Secondary, "secondary")]
    [InlineData("Cancel", RouteableContentDialogResult.None, "close")]
    public Task A_content_dialog_gives_the_button_that_was_pressed_and_tells_its_steps(string pressed, RouteableContentDialogResult expected, string click)
        => WithOwnerAsync(async owner =>
    {
        var ct = TestContext.Current.CancellationToken;
        var log = new List<string>();
        var pending = await StartAsync(() => Box.ShowAsync(Content(log), owner));
        var dialog = await WaitForWindowAsync("Content", ct);
        await WpfHarness.WaitUntil(() => log.Contains("opened"), "the dialog is announced as opened", ct);

        Assert.Equal(new[] { "Save", "Discard", "Cancel" }, await ButtonsAsync(dialog));

        await ClickAsync(dialog, pressed);

        Assert.Equal(expected, await pending);
        Assert.Equal(new[] { "opening", "opened", click, "closing " + expected, "closed" }, log);
    });

    [Fact]
    public Task A_content_dialog_that_vetoes_the_closing_stays_open_until_it_agrees() => WithOwnerAsync(async owner =>
    {
        var ct = TestContext.Current.CancellationToken;
        var log = new List<string>();
        var keep = true;
        var pending = await StartAsync(() => Box.ShowAsync(Content(log, () => keep), owner));
        var dialog = await WaitForWindowAsync("Content", ct);

        await ClickAsync(dialog, "Save");
        Assert.False(pending.IsCompleted);
        Assert.True(await IsOpenAsync("Content"));

        // The window's own close button and Escape are dismissals: the same veto.
        await PressEscapeAsync(dialog);
        await CloseAsync(dialog);
        Assert.False(pending.IsCompleted);
        Assert.True(await IsOpenAsync("Content"));

        keep = false;
        await PressEscapeAsync(dialog);

        Assert.Equal(RouteableContentDialogResult.None, await pending);
    });

    [Fact]
    public Task A_button_whose_command_cannot_execute_is_disabled_and_follows_the_command() => WithOwnerAsync(async owner =>
    {
        var ct = TestContext.Current.CancellationToken;
        var allowed = false;
        var runs = 0;
        var command = new ActionCommand(() => runs++, () => allowed);
        var parameters = Content([]);
        parameters.PrimaryButtonCommand = command;
        var pending = await StartAsync(() => Box.ShowAsync(parameters, owner));
        var dialog = await WaitForWindowAsync("Content", ct);

        Assert.False(await IsEnabledAsync(dialog, "Save"));
        await ClickAsync(dialog, "Save");
        Assert.Equal(0, runs);
        Assert.False(pending.IsCompleted);

        // The command may change its mind from another thread.
        allowed = true;
        await Task.Run(command.RaiseCanExecuteChanged, ct);
        await WpfHarness.WaitUntil(() => Logical<Button>(dialog).Single(b => (b.Content as string) == "Save").IsEnabled, "the button follows the command", ct);

        await ClickAsync(dialog, "Save");

        Assert.Equal(RouteableContentDialogResult.Primary, await pending);
        Assert.Equal(1, runs);
    });

    [Fact]
    public Task A_content_dialog_can_be_closed_from_code_and_then_cannot_any_more() => WithOwnerAsync(async owner =>
    {
        var ct = TestContext.Current.CancellationToken;
        var parameters = Content([]);
        var pending = await StartAsync(() => Box.ShowAsync(parameters, owner));
        await WaitForWindowAsync("Content", ct);
        Assert.NotNull(parameters.Close);

        parameters.Close!();

        Assert.Equal(RouteableContentDialogResult.None, await pending);
        Assert.Null(parameters.Close);
    });

    [Fact]
    public Task The_control_of_a_content_dialog_can_be_shown_again_after_it_closed() => WithOwnerAsync(async owner =>
    {
        var ct = TestContext.Current.CancellationToken;
        var body = await WpfHarness.Run(() => new TextBlock { Text = "shared body" });
        var parameters = Content([]);
        parameters.Content = body;

        for (var round = 0; round < 2; round++)
        {
            var pending = await StartAsync(() => Box.ShowAsync(parameters, owner));
            var dialog = await WaitForWindowAsync("Content", ct);
            Assert.Contains("shared body", await TextsAsync(dialog));

            await ClickAsync(dialog, "Cancel");
            await pending;

            Assert.Null(await WpfHarness.Run(() => LogicalTreeHelper.GetParent(body)));
        }
    });

    //[Fact]
    //public Task The_content_dialog_service_shows_the_dialog_over_the_active_window() => WithOwnerAsync(async owner =>
    //{
    //    var ct = TestContext.Current.CancellationToken;
    //    var service = new RouteableContentDialogService(Box);
    //    var pending = await StartAsync(() => service.ShowAsync(Content([], title: "Service")));
    //    var dialog = await WaitForWindowAsync("Service", ct);

    //    Assert.Same(owner, await WpfHarness.Run(() => dialog.Owner));

    //    await ClickAsync(dialog, "Save");
    //    Assert.Equal(RouteableContentDialogResult.Primary, await pending);
    //});

    // ---- task dialog ----

    [Fact]
    public Task A_task_dialog_shows_its_texts_and_gives_the_result_that_was_pressed() => WithOwnerAsync(async owner =>
    {
        var ct = TestContext.Current.CancellationToken;
        var log = new List<string>();
        var parameters = new RouteableTaskDialog
        {
            Title = "Task",
            Header = "Delete the file?",
            SubHeader = "It cannot be restored.",
            Content = "details",
            Opening = () => log.Add("opening"),
            Opened = () => log.Add("opened"),
            Closed = () => log.Add("closed")
        };
        var pending = await StartAsync(() => Box.ShowAsync(parameters, owner, RouteableTaskDialogResult.Yes, RouteableTaskDialogResult.No));
        var dialog = await WaitForWindowAsync("Task", ct);
        await WpfHarness.WaitUntil(() => log.Contains("opened"), "the dialog is announced as opened", ct);

        var texts = await TextsAsync(dialog);
        Assert.Contains("Delete the file?", texts);
        Assert.Contains("It cannot be restored.", texts);
        Assert.Contains("details", texts);
        Assert.Equal(new[] { "Yes", "No" }, await ButtonsAsync(dialog));

        await ClickAsync(dialog, "No");

        Assert.Equal(RouteableTaskDialogResult.No, await pending);
        Assert.Equal(new[] { "opening", "opened", "closed" }, log);
    });

    //[Fact]
    //public Task A_task_dialog_without_results_has_one_close_button_and_a_dismissal_gives_none() => WithOwnerAsync(async owner =>
    //{
    //    var ct = TestContext.Current.CancellationToken;
    //    var service = new RouteableTaskDialogService(Box);
    //    var pending = await StartAsync(() => service.ShowAsync(new RouteableTaskDialog { Title = "Info", Header = "Done" }));
    //    var dialog = await WaitForWindowAsync("Info", ct);

    //    Assert.Equal(new[] { "Close" }, await ButtonsAsync(dialog));

    //    await PressEscapeAsync(dialog);

    //    Assert.Equal(RouteableTaskDialogResult.None, await pending);
    //});

    [Fact]
    public Task A_task_dialog_that_vetoes_the_closing_stays_open() => WithOwnerAsync(async owner =>
    {
        var ct = TestContext.Current.CancellationToken;
        var keep = true;
        var parameters = new RouteableTaskDialog { Title = "Busy", Header = "Working", ShowProgressBar = true, Closing = () => keep };
        var pending = await StartAsync(() => Box.ShowAsync(parameters, owner, RouteableTaskDialogResult.Cancel));
        var dialog = await WaitForWindowAsync("Busy", ct);

        await ClickAsync(dialog, "Cancel");
        await PressEscapeAsync(dialog);
        Assert.False(pending.IsCompleted);
        Assert.Single(await WpfHarness.Run(() => Logical<ProgressBar>(dialog).ToArray()));

        keep = false;
        await ClickAsync(dialog, "Cancel");

        Assert.Equal(RouteableTaskDialogResult.Cancel, await pending);
    });

    [Fact]
    public Task The_controls_of_a_task_dialog_can_be_shown_again_after_it_closed() => WithOwnerAsync(async owner =>
    {
        var ct = TestContext.Current.CancellationToken;
        var body = await WpfHarness.Run(() => new TextBlock { Text = "body" });
        var footer = await WpfHarness.Run(() => new TextBlock { Text = "footer" });
        var parameters = new RouteableTaskDialog
        {
            Title = "Again",
            Content = body,
            Footer = footer,
            FooterVisibility = RouteableTaskDialogFooterVisibility.Auto
        };

        for (var round = 0; round < 2; round++)
        {
            var pending = await StartAsync(() => Box.ShowAsync(parameters, owner));
            var dialog = await WaitForWindowAsync("Again", ct);
            Assert.Contains("body", await TextsAsync(dialog));

            await ClickAsync(dialog, "Close");
            await pending;

            Assert.Null(await WpfHarness.Run(() => LogicalTreeHelper.GetParent(body)));
            Assert.Null(await WpfHarness.Run(() => LogicalTreeHelper.GetParent(footer)));
        }
    });

    // ---- modal of a view ----

    private sealed class ModalViewModel : ICloseableViewModel<string?>
    {
        public event EventHandler<string?>? CloseRequested;

        public string Title => "Modal title";

        public ObservableCollection<RouteableCommand> Commands { get; } = [];

        public ICommand? CloseCommand => null;

        public Task CloseAsync(string? value)
        {
            CloseRequested?.Invoke(this, value);
            return Task.CompletedTask;
        }
    }

    private sealed class ModalView : DialogView<ModalViewModel, string>
    {
        public string? Validated { get; private set; } = "(not validated)";

        protected override Task OnValidateAsync(string? result)
        {
            Validated = result;
            return Task.CompletedTask;
        }
    }

    // The modal looks for the dialog service in the container of the application.
    private static async Task WithContainerAsync(Func<Task> test)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IMessageBoxService>(Box);
        var provider = services.BuildServiceProvider();
        ServiceLocator.Services = provider;
        try
        {
            await test();
        }
        finally
        {
            ServiceLocator.Services = null;
            provider.Dispose();
        }
    }

    [Fact]
    public Task A_modal_view_gives_the_result_its_view_model_closed_with() => WithOwnerAsync(owner => WithContainerAsync(async () =>
    {
        var ct = TestContext.Current.CancellationToken;
        var viewModel = new ModalViewModel();
        viewModel.Commands.Add(new RouteableCommand { Name = "Save", Command = new ActionCommand(() => _ = viewModel.CloseAsync("saved")) });
        var view = await WpfHarness.Run(() => new ModalView { Context = viewModel });
        var pending = await StartAsync(() => view.ShowModalAsync());
        var dialog = await WaitForWindowAsync("Modal title", ct);

        Assert.Equal(new[] { "Save" }, await ButtonsAsync(dialog));

        await ClickAsync(dialog, "Save");

        Assert.Equal("saved", await pending);
        Assert.Equal("saved", await WpfHarness.Run(() => view.Validated));
        Assert.False(await IsOpenAsync("Modal title"));
        // The view is not kept by the dialog: it can be shown again.
        Assert.Null(await WpfHarness.Run(() => LogicalTreeHelper.GetParent(view)));
    }));

    [Fact]
    public Task A_modal_view_without_commands_has_a_close_button_and_gives_nothing() => WithOwnerAsync(owner => WithContainerAsync(async () =>
    {
        var ct = TestContext.Current.CancellationToken;
        var view = await WpfHarness.Run(() => new ModalView { Context = new ModalViewModel() });
        var pending = await StartAsync(() => view.ShowModalAsync());
        var dialog = await WaitForWindowAsync("Modal title", ct);

        Assert.Equal(new[] { "Close" }, await ButtonsAsync(dialog));

        await ClickAsync(dialog, "Close");

        Assert.Null(await pending);
    }));

    [Fact]
    public Task A_modal_view_closed_by_its_view_model_from_code_closes_the_dialog() => WithOwnerAsync(owner => WithContainerAsync(async () =>
    {
        var ct = TestContext.Current.CancellationToken;
        var viewModel = new ModalViewModel();
        var view = await WpfHarness.Run(() => new ModalView { Context = viewModel });
        var pending = await StartAsync(() => view.ShowModalAsync());
        await WaitForWindowAsync("Modal title", ct);

        await WpfHarness.Run(() => viewModel.CloseAsync("from code"));

        Assert.Equal("from code", await pending);
        Assert.False(await IsOpenAsync("Modal title"));
    }));

    [Fact]
    public async Task A_modal_view_without_a_view_model_is_refused()
    {
        var view = await WpfHarness.Run(() => new ModalView());

        await Assert.ThrowsAsync<ArgumentNullException>(() => WpfHarness.Run(() => view.ShowModalAsync()));
    }

    [Fact]
    public async Task A_modal_view_without_the_host_says_what_to_call()
    {
        ServiceLocator.Services = null;
        var view = await WpfHarness.Run(() => new ModalView { Context = new ModalViewModel() });

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => WpfHarness.Run(() => view.ShowModalAsync()));

        Assert.Contains("UseRouteables", error.Message);
    }
}
