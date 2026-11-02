using System.Collections.ObjectModel;
using System.Windows.Input;
using DeviceRunners.UITesting;
using Routeables.Abstraction;
using Routeables.Maui.Services;
using Routeables.Services;
using Xunit;

namespace Routeables.Test.Maui;

/// <summary>
/// <see cref="RouteableDialogService"/> on the platform: it hands the dialogs to the message box service it was given, and otherwise shows them in
/// transparent modal pages over the page of the window (content dialogs, task dialogs and modals), several at a time.
/// </summary>
public class DialogServiceDeviceTests
{
    // ------------------------------------------------------------------------------------------------ doubles

    private sealed class RecordingBox : IMessageBoxService
    {
        public List<string> Calls { get; } = [];
        public int Index { get; set; }
        public RouteableContentDialogResult ContentResult { get; set; } = RouteableContentDialogResult.Secondary;
        public RouteableTaskDialogResult TaskResult { get; set; } = RouteableTaskDialogResult.Cancel;

        public Task<int> ShowMessageAsync(string title, string message, string buttons)
        {
            Calls.Add($"message|{title}|{message}|{buttons}");
            return Task.FromResult(Index);
        }

        public Task<RouteableContentDialogResult> ShowContentDialogAsync(RouteableContentDialog @params)
        {
            Calls.Add("content|" + @params.Title);
            return Task.FromResult(ContentResult);
        }

        public Task<RouteableTaskDialogResult> ShowTaskDialogAsync(RouteableTaskDialog @params, params RouteableTaskDialogResult[] results)
        {
            Calls.Add("task|" + @params.Title + "|" + results.Length);
            return Task.FromResult(TaskResult);
        }

        public Task<TResult?> ShowModalAsync<TViewModel, TResult>(TViewModel viewModel, Func<View> viewFactory)
            where TViewModel : ICloseableViewModel<TResult>
        {
            Calls.Add("modal|" + viewModel.Title);
            return Task.FromResult<TResult?>(default);
        }
    }

    private sealed class NoPage : IRouteableDialogWindowContext
    {
        public Page? CurrentPage => null;
    }

    private sealed class Closeable(string title, params RouteableCommand[] commands) : ICloseableViewModel<string>
    {
        public string Title => title;
        public ObservableCollection<RouteableCommand> Commands { get; } = new(commands);
        public ICommand? CloseCommand => null;
        public event EventHandler<string?>? CloseRequested;

        public Task CloseAsync(string? value)
        {
            CloseRequested?.Invoke(this, value);
            return Task.CompletedTask;
        }
    }

    private sealed class Run(Action action) : ICommand
    {
        public event EventHandler? CanExecuteChanged { add { } remove { } }
        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter) => action();
    }

    private static RouteableDialogService Service(IMessageBoxService? box = null, IRouteableDialogWindowContext? context = null)
#if ANDROID || WINDOWS || IOS || MACCATALYST
        => new(Device.Services, context ?? new RouteableDialogWindowContext(), box);
#else
        => new(context ?? new RouteableDialogWindowContext(), box);
#endif

    private static Page Host => Application.Current!.Windows[0].Page!;

    private static IReadOnlyList<Page> Modals => Host.Navigation.ModalStack;

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

    private static async Task<ContentPage> PopupAsync(int count = 1)
    {
        await Device.UntilAsync(() => Modals.Count >= count && All((IView)Modals[count - 1]).OfType<Button>().Any());
        return (ContentPage)Modals[count - 1];
    }

    private static Button ButtonOf(ContentPage popup, string text) => All(popup).OfType<Button>().Single(b => b.Text == text);

    private static void Tap(Button button) => ((IButton)button).Clicked();

    // A dialog that a test left on the window (it failed before closing it) would be taken for the one the next test opens.
    private static async Task CleanAsync()
    {
        for (var i = 0; i < 10 && Modals.Count > 0; i++)
        {
            try { await Host.Navigation.PopModalAsync(animated: false); }
            catch (Exception) { break; }
        }
    }

    private static string State(params (string Name, Task Task)[] tasks)
        => $"modals={Modals.Count} [{string.Join(", ", Modals.Select(m => m.GetType().Name))}]; "
           + string.Join("; ", tasks.Select(t => $"{t.Name} completed={t.Task.IsCompleted}"));

    private static async Task<T> ResultAsync<T>(Task<T> task)
    {
        await Device.UntilAsync(() => task.IsCompleted);
        return await task;
    }

    private static async Task ClosedAsync(int remaining = 0) => await Device.UntilAsync(() => Modals.Count == remaining);

    // ------------------------------------------------------------------------------------------------ the message box service it was given

    [UIFact]
    public async Task A_message_goes_to_the_message_box_service_with_the_title_first()
    {
        var box = new RecordingBox { Index = 1 };

        var index = await Service(box).ShowMessageAsync("the message", "the title", "YesNo");

        Assert.Equal(1, index);
        Assert.Equal(["message|the title|the message|YesNo"], box.Calls);
    }

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

    [UIFact]
    public async Task Without_parameters_the_dialogs_are_refused()
    {
        var service = Service(new RecordingBox());

        await Assert.ThrowsAsync<ArgumentNullException>(() => service.ShowContentDialogAsync(null!));
        await Assert.ThrowsAsync<ArgumentNullException>(() => service.ShowTaskDialogAsync(null!));
        await Assert.ThrowsAsync<ArgumentNullException>(() => service.ShowModalAsync<Closeable, string>(null!, () => new Label()));
        await Assert.ThrowsAsync<ArgumentNullException>(() => service.ShowModalAsync<Closeable, string>(new Closeable("m"), null!));
    }

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

    [UIFact]
    public async Task A_modal_view_factory_that_gives_nothing_is_refused()
    {
        var service = Service();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.ShowModalAsync<Closeable, string>(new Closeable("m"), () => null!));
        Assert.Contains("returned null", ex.Message);
    }

    // ------------------------------------------------------------------------------------------------ content dialogs in a modal page

    [UIFact]
    public async Task A_content_dialog_opens_over_the_page_and_gives_the_button_that_was_pressed()
    {
        await CleanAsync();
        var shown = Service().ShowContentDialogAsync(new RouteableContentDialog
        {
            Title = "Rename", Content = "New name?", PrimaryButtonText = "Save", IsPrimaryButtonEnabled = true, CloseButtonText = "Cancel"
        });

        var popup = await PopupAsync();
        Assert.Contains(All(popup).OfType<Label>(), l => l.Text == "New name?");
        Tap(ButtonOf(popup, "Save"));

        Assert.Equal(RouteableContentDialogResult.Primary, await ResultAsync(shown));
        await ClosedAsync();
    }

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

        await viewModel.CloseAsync("done");

        Assert.Equal("done", await ResultAsync(shown));
        await ClosedAsync();
    }

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
