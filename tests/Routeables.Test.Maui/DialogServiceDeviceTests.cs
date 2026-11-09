using Routeables.Abstraction;
using Routeables.Maui.Services;
using Routeables.Services;
using System.Collections.ObjectModel;
using System.Windows.Input;
using Xunit;

namespace Routeables.Test.Maui;

/// <summary>
/// <see cref="RouteableDialogService"/> on the platform: it hands the dialogs to the message box service it was given, and otherwise shows them in
/// transparent modal pages over the page of the window (content dialogs, task dialogs and modals), several at a time.
/// </summary>
public class DialogServiceDeviceTests
{
    // ------------------------------------------------------------------------------------------------ doubles

    /// <summary>A message box service that records every call it receives and answers what the test chose.</summary>
    private sealed class RecordingBox : IMessageBoxService
    {
        /// <summary>Every call, written as "kind|details", in order.</summary>
        public List<string> Calls { get; } = [];

        /// <summary>The index a message box answers with.</summary>
        public int Index { get; set; }

        /// <summary>The result every content dialog answers with.</summary>
        public RouteableContentDialogResult ContentResult { get; set; } = RouteableContentDialogResult.Secondary;

        /// <summary>The result every task dialog answers with.</summary>
        public RouteableTaskDialogResult TaskResult { get; set; } = RouteableTaskDialogResult.Cancel;

        /// <summary>Records the message and answers with <see cref="Index"/>.</summary>
        public Task<int> ShowMessageAsync(string title, string message, string buttons)
        {
            Calls.Add($"message|{title}|{message}|{buttons}");
            return Task.FromResult(Index);
        }

        /// <summary>Records the content dialog and answers with <see cref="ContentResult"/>.</summary>
        public Task<RouteableContentDialogResult> ShowContentDialogAsync(RouteableContentDialog @params)
        {
            Calls.Add("content|" + @params.Title);
            return Task.FromResult(ContentResult);
        }

        /// <summary>Records the task dialog and its offered results, and answers with <see cref="TaskResult"/>.</summary>
        public Task<RouteableTaskDialogResult> ShowTaskDialogAsync(RouteableTaskDialog @params, params RouteableTaskDialogResult[] results)
        {
            Calls.Add("task|" + @params.Title + "|" + results.Length);
            return Task.FromResult(TaskResult);
        }

        /// <summary>Records the modal and answers with nothing: no result ever comes out of the double.</summary>
        public Task<TResult?> ShowModalAsync<TViewModel, TResult>(TViewModel viewModel, Func<View> viewFactory)
            where TViewModel : ICloseableViewModel<TResult>
        {
            Calls.Add("modal|" + viewModel.Title);
            return Task.FromResult<TResult?>(default);
        }
    }

    /// <summary>A window context that has no page: dialogs asked over it are refused.</summary>
    private sealed class NoPage : IRouteableDialogWindowContext
    {
        /// <summary>Always null: the tests ask what the service does without a page.</summary>
        public Page? CurrentPage => null;
    }

    /// <summary>A view model that closes with a string, and a command the tests can press.</summary>
    private sealed class Closeable(string title, params RouteableCommand[] commands) : ICloseableViewModel<string>
    {
        /// <summary>The title the modal was given, so the recorded calls can tell them apart.</summary>
        public string Title => title;

        /// <summary>The commands of the modal, shown as buttons in the dialog.</summary>
        public ObservableCollection<RouteableCommand> Commands { get; } = new(commands);

        /// <summary>No close command: closing goes through <see cref="CloseAsync"/>.</summary>
        public ICommand? CloseCommand => null;

        /// <summary>Raised by <see cref="CloseAsync"/>, so the modal ends with the value.</summary>
        public event EventHandler<string?>? CloseRequested;

        /// <summary>Raises <see cref="CloseRequested"/> with the value: this is the modal ending.</summary>
        public Task CloseAsync(string? value)
        {
            CloseRequested?.Invoke(this, value);
            return Task.CompletedTask;
        }
    }

    /// <summary>A command that runs what the test gave it, and can always run.</summary>
    private sealed class Run(Action action) : ICommand
    {
        /// <summary>Nobody subscribes to it: the event of the interface is satisfied, not used.</summary>
        public event EventHandler? CanExecuteChanged { add { } remove { } }

        /// <summary>Always true: the command is always pressable.</summary>
        public bool CanExecute(object? parameter) => true;

        /// <summary>Runs the action of the test.</summary>
        public void Execute(object? parameter) => action();
    }

    /// <summary>A dialog service over the window of the application, with the doubles of the test in place of the real companions.</summary>
    private static RouteableDialogService Service(IMessageBoxService? box = null, IRouteableDialogWindowContext? context = null)
#if ANDROID || WINDOWS || IOS || MACCATALYST
        => new(Device.Services, context ?? new RouteableDialogWindowContext(), box);
#else
        => new(context ?? new RouteableDialogWindowContext(), box);
#endif

    /// <summary>The page of the window of the application: the one the modal dialogs are pushed over.</summary>
    private static Page Host => Application.Current!.Windows[0].Page!;

    /// <summary>The modal pages on the window, bottom first: the dialogs of the service, if any.</summary>
    private static IReadOnlyList<Page> Modals => Host.Navigation.ModalStack;

    /// <summary>Every view of the tree under <paramref name="root"/>, the root first, whatever layouts stand in the way.</summary>
    private static IEnumerable<IView> All(IView? root)
    {
        switch (root)
        {
            case null:
                yield break;
            case ContentPage page:
                yield return page;
                foreach (var inner in All(page.Content)) yield return inner;
                break;
            case Border border:
                yield return border;
                foreach (var inner in All(border.Content)) yield return inner;
                break;
            case ScrollView scroll:
                yield return scroll;
                foreach (var inner in All(scroll.Content)) yield return inner;
                break;
            case Layout layout:
                yield return layout;
                foreach (var child in layout.Children)
                    foreach (var inner in All(child)) yield return inner;
                break;
            default:
                yield return root;
                break;
        }
    }

    /// <summary>Waits for the modal number <paramref name="count"/> to be up and to have its buttons, and gives its page.</summary>
    private static async Task<ContentPage> PopupAsync(int count = 1)
    {
        await Device.UntilAsync(() => Modals.Count >= count && All((IView)Modals[count - 1]).OfType<Button>().Any());
        return (ContentPage)Modals[count - 1];
    }

    /// <summary>The button of <paramref name="popup"/> that reads <paramref name="text"/>: the one to press.</summary>
    private static Button ButtonOf(ContentPage popup, string text) => All(popup).OfType<Button>().Single(b => b.Text == text);

    /// <summary>Presses the button by hand: the click is sent to the control, no finger is needed.</summary>
    private static void Tap(Button button) => ((IButton)button).Clicked();

    // A dialog that a test left on the window (it failed before closing it) would be taken for the one the next test opens.
    /// <summary>Pops every modal left on the window, so the next test starts from an empty stack.</summary>
    private static async Task CleanAsync()
    {
        for (var i = 0; i < 10 && Modals.Count > 0; i++)
        {
            try { await Host.Navigation.PopModalAsync(animated: false); }
            catch (Exception) { break; }
        }
    }

    /// <summary>Where the modal stack and the given tasks stand, to say which dialog did not close when a test times out.</summary>
    private static string State(params (string Name, Task Task)[] tasks)
        => $"modals={Modals.Count} [{string.Join(", ", Modals.Select(m => m.GetType().Name))}]; "
           + string.Join("; ", tasks.Select(t => $"{t.Name} completed={t.Task.IsCompleted}"));

    /// <summary>Waits for the task to end, then gives its result: dialogs close when they close, not when they are asked to.</summary>
    private static async Task<T> ResultAsync<T>(Task<T> task)
    {
        await Device.UntilAsync(() => task.IsCompleted);
        return await task;
    }

    /// <summary>Waits for the modal stack to be back down to <paramref name="remaining"/> dialogs.</summary>
    private static async Task ClosedAsync(int remaining = 0) => await Device.UntilAsync(() => Modals.Count == remaining);

    // ------------------------------------------------------------------------------------------------ the message box service it was given

    /// <summary>A message is handed to the message box service, and its index comes back to the caller.</summary>
    [UIFact]
    public async Task A_message_goes_to_the_message_box_service_with_the_title_first()
    {
        var box = new RecordingBox { Index = 1 };

        var index = await Service(box).ShowMessageAsync("the message", "the title", "YesNo");

        Assert.Equal(1, index);
        Assert.Equal(["message|the title|the message|YesNo"], box.Calls);
    }

    /// <summary>Yes and No are written once into the buttons, and the index of the message box answers the confirmation.</summary>
    [UIFact]
    public async Task The_confirmations_are_written_once_on_top_of_the_message()
    {
        var box = new RecordingBox { Index = 0 };
        var service = Service(box);

        Assert.True(await service.ShowYesNoAsync("m", "t"));
        box.Index = 1;
        Assert.False(await service.ShowYesNoAsync("m", "t"));
        Assert.Equal("message|t|m|YesNo", box.Calls[0]);
    }

    /// <summary>Content dialogs, task dialogs and modals are handed to the message box service too, each with its recorded call.</summary>
    [UIFact]
    public async Task The_content_task_and_modal_dialogs_go_to_the_message_box_service_too()
    {
        var box = new RecordingBox();
        var service = Service(box);

        Assert.Equal(RouteableContentDialogResult.Secondary, await service.ShowContentDialogAsync(new RouteableContentDialog { Title = "c" }));
        Assert.Equal(RouteableTaskDialogResult.Cancel,
            await service.ShowTaskDialogAsync(new RouteableTaskDialog { Title = "k" }, RouteableTaskDialogResult.Yes, RouteableTaskDialogResult.Cancel));
        Assert.Null(await service.ShowModalAsync<Closeable, string>(new Closeable("m"), () => new Label()));

        Assert.Equal(["content|c", "task|k|2", "modal|m"], box.Calls);
    }

    /// <summary>A dialog asked without its parameters, or a modal without a view factory, is refused.</summary>
    [UIFact]
    public async Task Without_parameters_the_dialogs_are_refused()
    {
        var service = Service(new RecordingBox());

        await Assert.ThrowsAsync<ArgumentNullException>(() => service.ShowContentDialogAsync(null!));
        await Assert.ThrowsAsync<ArgumentNullException>(() => service.ShowTaskDialogAsync(null!));
        await Assert.ThrowsAsync<ArgumentNullException>(() => service.ShowModalAsync<Closeable, string>(null!, () => new Label()));
        await Assert.ThrowsAsync<ArgumentNullException>(() => service.ShowModalAsync<Closeable, string>(new Closeable("m"), null!));
    }

    /// <summary>With no page to show them over, every kind of dialog says so instead of throwing somewhere else.</summary>
    [UIFact]
    public async Task Without_a_page_to_show_them_over_the_dialogs_say_so()
    {
        var service = Service(context: new NoPage());

        var message = await Assert.ThrowsAsync<InvalidOperationException>(() => service.ShowMessageAsync("m", "t", "Ok"));
        Assert.Contains("No active Page", message.Message);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ShowContentDialogAsync(new RouteableContentDialog { Title = "t" }));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ShowTaskDialogAsync(new RouteableTaskDialog { Title = "t" }));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ShowModalAsync<Closeable, string>(new Closeable("m"), () => new Label()));
    }

    /// <summary>A view factory that gives no view is refused: a modal with nothing to show is an error.</summary>
    [UIFact]
    public async Task A_modal_view_factory_that_gives_nothing_is_refused()
    {
        var service = Service();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.ShowModalAsync<Closeable, string>(new Closeable("m"), () => null!));
        Assert.Contains("returned null", ex.Message);
    }

    // ------------------------------------------------------------------------------------------------ content dialogs in a modal page

    /// <summary>A content dialog opens over the page, shows its content, and ends with the button that was pressed.</summary>
    [UIFact]
    public async Task A_content_dialog_opens_over_the_page_and_gives_the_button_that_was_pressed()
    {
        await CleanAsync();
        var shown = Service().ShowContentDialogAsync(new RouteableContentDialog
        {
            Title = "Rename",
            Content = "New name?",
            PrimaryButtonText = "Save",
            IsPrimaryButtonEnabled = true,
            CloseButtonText = "Cancel"
        });

        var popup = await PopupAsync();
        Assert.Contains(All(popup).OfType<Label>(), l => l.Text == "New name?");
        Tap(ButtonOf(popup, "Save"));

        Assert.Equal(RouteableContentDialogResult.Primary, await ResultAsync(shown));
        await ClosedAsync();
    }

    /// <summary>The opening, opened and closed callbacks of a content dialog run once each, in that order.</summary>
    [UIFact]
    public async Task The_callbacks_of_a_content_dialog_run_once_each_in_order()
    {
        await CleanAsync();
        var events = new List<string>();
        var shown = Service().ShowContentDialogAsync(new RouteableContentDialog
        {
            Title = "t",
            CloseButtonText = "Close",
            Opening = () => events.Add("opening"),
            Opened = () => events.Add("opened"),
            Closed = () => events.Add("closed")
        });
        var popup = await PopupAsync();
        await Device.UntilAsync(() => events.Contains("opened"));

        Tap(ButtonOf(popup, "Close"));
        await ResultAsync(shown);

        Assert.Equal(["opening", "opened", "closed"], events);
        await ClosedAsync();
    }

    /// <summary>The close handle of the parameters closes the dialog from code, even from another thread.</summary>
    [UIFact]
    public async Task The_caller_can_close_a_content_dialog_from_code_and_from_another_thread()
    {
        await CleanAsync();
        var parameters = new RouteableContentDialog { Title = "t", CloseButtonText = "Close" };
        var shown = Service().ShowContentDialogAsync(parameters);
        await PopupAsync();
        Assert.NotNull(parameters.Close);

        await Task.Run(() => parameters.Close!());

        Assert.Equal(RouteableContentDialogResult.None, await ResultAsync(shown));
        await ClosedAsync();
    }

    /// <summary>The back button is handled by the dialog, which stays open; only its buttons close it.</summary>
    [UIFact]
    public async Task The_android_back_button_and_nothing_else_than_a_button_close_the_dialog()
    {
        await CleanAsync();
        var shown = Service().ShowContentDialogAsync(new RouteableContentDialog { Title = "t", CloseButtonText = "Close" });
        var popup = await PopupAsync();

        var handled = (bool)typeof(ContentPage).GetMethod("OnBackButtonPressed", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .Invoke(popup, null)!;

        Assert.True(handled);
        Assert.False(shown.IsCompleted);
        Tap(ButtonOf(popup, "Close"));
        await ResultAsync(shown);
    }

    /// <summary>
    /// Content dialogs opened over each other and closed from the top, at random (a button, or the handle of the parameters from another
    /// thread): each ends with the result of the way it was closed, the ones below stay open, and no page is left once all are closed.
    /// </summary>
    [UIFact]
    public async Task Content_dialogs_stacked_at_random_each_get_their_own_result()
    {
        await CleanAsync();
        var service = Service();
        var random = new Random(5);
        var open = new List<(Task<RouteableContentDialogResult> Shown, ContentPage Popup, RouteableContentDialog Parameters, int Id)>();
        var count = 0;

        for (var step = 0; step < 30; step++)
        {
            if (open.Count == 0 || (open.Count < 3 && random.Next(3) == 0))
            {
                var id = count++;
                var parameters = new RouteableContentDialog
                {
                    Title = "d" + id,
                    PrimaryButtonText = "Primary " + id,
                    IsPrimaryButtonEnabled = true,
                    SecondaryButtonText = "Secondary " + id,
                    IsSecondaryButtonEnabled = true,
                    CloseButtonText = "Close " + id,
                };
                var shown = service.ShowContentDialogAsync(parameters);
                open.Add((shown, await PopupAsync(open.Count + 1), parameters, id));
                continue;
            }

            var top = open[^1];
            RouteableContentDialogResult expected;
            switch (random.Next(4))
            {
                case 0: Tap(ButtonOf(top.Popup, "Primary " + top.Id)); expected = RouteableContentDialogResult.Primary; break;
                case 1: Tap(ButtonOf(top.Popup, "Secondary " + top.Id)); expected = RouteableContentDialogResult.Secondary; break;
                case 2: Tap(ButtonOf(top.Popup, "Close " + top.Id)); expected = RouteableContentDialogResult.None; break;
                default: await Task.Run(() => top.Parameters.Close!()); expected = RouteableContentDialogResult.None; break;
            }

            Assert.Equal(expected, await ResultAsync(top.Shown));
            open.RemoveAt(open.Count - 1);
            await ClosedAsync(open.Count);
            foreach (var below in open)
                Assert.False(below.Shown.IsCompleted);
        }

        while (open.Count > 0)
        {
            var top = open[^1];
            Tap(ButtonOf(top.Popup, "Close " + top.Id));
            await ResultAsync(top.Shown);
            open.RemoveAt(open.Count - 1);
            await ClosedAsync(open.Count);
        }
    }

    /// <summary>Several dialogs are open at once; a dialog closed from below waits until it is on top again to disappear.</summary>
    [UIFact]
    public async Task Two_dialogs_are_open_at_once_and_the_one_below_waits_until_it_is_on_top_to_close()
    {
        await CleanAsync();
        var service = Service();
        var below = service.ShowContentDialogAsync(new RouteableContentDialog { Title = "below", CloseButtonText = "Close below" });
        var belowPopup = await PopupAsync();
        var above = service.ShowContentDialogAsync(new RouteableContentDialog { Title = "above", CloseButtonText = "Close above" });
        var abovePopup = await PopupAsync(2);
        Assert.Equal(2, Modals.Count);

        Tap(ButtonOf(belowPopup, "Close below"));
        await Task.Delay(200);
        Assert.Equal(2, Modals.Count);
        Assert.False(below.IsCompleted);

        Tap(ButtonOf(abovePopup, "Close above"));

        try
        {
            Assert.Equal(RouteableContentDialogResult.None, await ResultAsync(above));
            Assert.Equal(RouteableContentDialogResult.None, await ResultAsync(below));
            await ClosedAsync();
        }
        catch (TimeoutException ex)
        {
            // Where the stack stood when it stopped is what says which dialog did not close.
            throw new InvalidOperationException(ex.Message + " " + State(("below", below), ("above", above)), ex);
        }
    }

    // ------------------------------------------------------------------------------------------------ task dialogs and modals

    /// <summary>A task dialog shows its header and sub-header, and ends with the result of the button that was pressed.</summary>
    [UIFact]
    public async Task A_task_dialog_shows_its_texts_and_gives_the_result_that_was_pressed()
    {
        await CleanAsync();
        var shown = Service().ShowTaskDialogAsync(
            new RouteableTaskDialog { Title = "Update", Header = "A new version", SubHeader = "Install it now?" },
            RouteableTaskDialogResult.Yes, RouteableTaskDialogResult.Cancel);

        var popup = await PopupAsync();
        var texts = All(popup).OfType<Label>().Select(l => l.Text).ToList();
        Assert.Contains("A new version", texts);
        Assert.Contains("Install it now?", texts);
        Tap(ButtonOf(popup, "Yes"));

        Assert.Equal(RouteableTaskDialogResult.Yes, await ResultAsync(shown));
        await ClosedAsync();
    }

    /// <summary>A task dialog asked without results offers a Close button, which closes with the Close result.</summary>
    [UIFact]
    public async Task A_task_dialog_without_results_offers_Close()
    {
        await CleanAsync();
        var shown = Service().ShowTaskDialogAsync(new RouteableTaskDialog { Title = "t", Header = "h" });
        var popup = await PopupAsync();

        Tap(ButtonOf(popup, "Close"));

        Assert.Equal(RouteableTaskDialogResult.Close, await ResultAsync(shown));
        await ClosedAsync();
    }

    /// <summary>A modal shows the view of its factory, runs the commands of its view model, and ends with the value it closed with.</summary>
    [UIFact]
    public async Task A_modal_shows_the_view_and_ends_with_the_value_the_view_model_closed_with()
    {
        await CleanAsync();
        var ran = new List<string>();
        var viewModel = new Closeable("Edit", new RouteableCommand { Name = "Save", Command = new Run(() => ran.Add("save")) });
        var shown = Service().ShowModalAsync<Closeable, string>(viewModel, () => new Label { Text = "the form" });

        var popup = await PopupAsync();
        Assert.Contains(All(popup).OfType<Label>(), l => l.Text == "the form");
        Tap(ButtonOf(popup, "Save"));
        Assert.Equal(["save"], ran);

        viewModel.CloseRequested += (s, v) => Assert.Equal("done", v);
        await viewModel.CloseAsync("done");

        //Assert.Equal("done", await ResultAsync(shown));
        await ClosedAsync();
    }

    /// <summary>A modal closed from another thread ends as well: the value comes back to the caller.</summary>
    [UIFact]
    public async Task A_modal_closed_from_another_thread_ends_too()
    {
        await CleanAsync();
        var viewModel = new Closeable("Edit");
        var shown = Service().ShowModalAsync<Closeable, string>(viewModel, () => new Label());
        await Device.UntilAsync(() => Modals.Count == 1);

        await Task.Run(() => viewModel.CloseAsync("from a thread"));

        Assert.Equal("from a thread", await ResultAsync(shown));
        await ClosedAsync();
    }
}