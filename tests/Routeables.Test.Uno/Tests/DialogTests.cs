using Microsoft.UI.Xaml.Media;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Routeables.Services;
using Routeables.Uno.Services;
using Uno.UI.RuntimeTests;

namespace Routeables.Test.Uno.Tests;

/// <summary>
/// The dialogs of Uno are <c>ContentDialog</c>s in the window: they show their texts and buttons, close with the result of the button
/// or from code, are not left by a dismissal the dialog vetoes, and wait for each other.
/// </summary>
[TestClass]
[RunsOnUIThread]
public class DialogTests
{
    /// <summary>The XamlRoot of the test host, used as the owner for dialogs.</summary>
    private static XamlRoot Root => MainPage.Host.XamlRoot!;

    /// <summary>Returns the currently open <see cref="ContentDialog"/>, if any.</summary>
    private static ContentDialog? OpenDialog() =>
        VisualTreeHelper.GetOpenPopupsForXamlRoot(Root).Select(p => p.Child).OfType<ContentDialog>().FirstOrDefault();

    /// <summary>Yields every descendant of <paramref name="parent"/> in depth-first order.</summary>
    private static IEnumerable<DependencyObject> Descendants(DependencyObject parent)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            yield return child;
            foreach (var deeper in Descendants(child)) yield return deeper;
        }
    }

    /// <summary>Returns all buttons of the dialog whose content is a string.</summary>
    private static List<Button> Buttons(ContentDialog dialog) =>
        [.. Descendants(dialog).OfType<Button>().Where(b => b.Content is string)];

    /// <summary>Returns the text of all <see cref="TextBlock"/> descendants of the dialog.</summary>
    private static List<string> Texts(ContentDialog dialog) =>
        [.. Descendants(dialog).OfType<TextBlock>().Select(t => t.Text)];

    /// <summary>Finds the single button whose content equals <paramref name="text"/>.</summary>
    private static Button ButtonOf(ContentDialog dialog, string text) => Buttons(dialog).Single(b => Equals(b.Content, text));

    /// <summary>Invokes the button through its automation peer.</summary>
    private static void Click(Button button)
    {
        var peer = Microsoft.UI.Xaml.Automation.Peers.FrameworkElementAutomationPeer.CreatePeerForElement(button)
            as Microsoft.UI.Xaml.Automation.Peers.ButtonAutomationPeer;
        Assert.IsNotNull(peer);
        ((Microsoft.UI.Xaml.Automation.Provider.IInvokeProvider)peer).Invoke();
    }

    /// <summary>Waits until a dialog with at least one button is open, then returns it.</summary>
    private static async Task<ContentDialog> OpenedAsync()
    {
        await TestHost.UntilAsync(() => OpenDialog() is { } d && Buttons(d).Count > 0);
        return OpenDialog()!;
    }

    /// <summary>Waits until <paramref name="task"/> completes, then returns its result.</summary>
    private static async Task<T> ResultAsync<T>(Task<T> task)
    {
        await TestHost.UntilAsync(() => task.IsCompleted);
        return await task;
    }

    /// <summary>Waits until no dialog is open.</summary>
    private static async Task ClosedAsync() => await TestHost.UntilAsync(() => OpenDialog() is null);

    /// <summary>A command whose <see cref="CanExecute"/> state can be toggled for testing button enablement.</summary>
    private sealed class FlagCommand(bool initial) : System.Windows.Input.ICommand
    {
        private bool _enabled = initial;

        /// <inheritdoc />
        public event EventHandler? CanExecuteChanged;

        /// <inheritdoc />
        public bool CanExecute(object? parameter) => _enabled;

        /// <inheritdoc />
        public void Execute(object? parameter) { }

        /// <summary>Sets whether the command can execute and raises <see cref="CanExecuteChanged"/>.</summary>
        public void Set(bool enabled)
        {
            _enabled = enabled;
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>A message box shows its title, message and one button per word of the buttons string, and returns the clicked button text.</summary>
    [TestMethod]
    public async Task A_message_box_shows_its_title_its_message_and_one_button_for_each_word()
    {
        var shown = new DefaultMessageBoxService().ShowMessageBoxAsync("Delete", "Are you sure?", "YesNoCancel", Root);
        var dialog = await OpenedAsync();
        Assert.AreEqual("Delete", dialog.Title);
        CollectionAssert.Contains(Texts(dialog), "Are you sure?");
        CollectionAssert.AreEqual(new[] { "Yes", "No", "Cancel" }, Buttons(dialog).Select(b => (string)b.Content).ToArray());
        Click(ButtonOf(dialog, "No"));
        Assert.AreEqual("No", await ResultAsync(shown));
        await ClosedAsync();
    }

    /// <summary>A message box with an empty buttons string offers a single Ok button.</summary>
    [TestMethod]
    public async Task A_message_box_with_no_buttons_offers_Ok()
    {
        var shown = new DefaultMessageBoxService().ShowMessageBoxAsync("t", "m", string.Empty, Root);
        var dialog = await OpenedAsync();
        CollectionAssert.AreEqual(new[] { "Ok" }, Buttons(dialog).Select(b => (string)b.Content).ToArray());
        Click(ButtonOf(dialog, "Ok"));
        Assert.AreEqual("Ok", await ResultAsync(shown));
        await ClosedAsync();
    }

    /// <summary>Dismissing a message box via the window returns null.</summary>
    [TestMethod]
    public async Task A_message_box_dismissed_by_the_window_gives_no_result()
    {
        var shown = new DefaultMessageBoxService().ShowMessageBoxAsync("t", "m", "YesNo", Root);
        var dialog = await OpenedAsync();
        dialog.Hide();
        Assert.IsNull(await ResultAsync(shown));
        await ClosedAsync();
    }

    /// <summary>A second dialog waits for the first one to close before it is shown.</summary>
    [TestMethod]
    public async Task A_second_dialog_waits_for_the_first_one_to_close()
    {
        var service = new DefaultMessageBoxService();
        var first = service.ShowMessageBoxAsync("first", "m", "Ok", Root);
        var firstDialog = await OpenedAsync();
        var second = service.ShowMessageBoxAsync("second", "m", "Ok", Root);
        await Task.Delay(100);
        Assert.AreEqual("first", OpenDialog()!.Title);
        Assert.IsFalse(second.IsCompleted);
        Click(ButtonOf(firstDialog, "Ok"));
        await ResultAsync(first);
        await TestHost.UntilAsync(() => OpenDialog() is { } d && Equals(d.Title, "second") && Buttons(d).Count > 0);
        Click(ButtonOf(OpenDialog()!, "Ok"));
        Assert.AreEqual("Ok", await ResultAsync(second));
        await ClosedAsync();
    }

    /// <summary>A content dialog shows its title, content text and configured buttons, and returns the primary result when Save is clicked.</summary>
    [TestMethod]
    public async Task A_content_dialog_shows_its_title_its_text_and_its_buttons()
    {
        var shown = new DefaultMessageBoxService().ShowAsync(new RouteableContentDialog
        {
            Title = "Rename",
            Content = "New name?",
            PrimaryButtonText = "Save",
            IsPrimaryButtonEnabled = true,
            CloseButtonText = "Cancel"
        }, Root);
        var dialog = await OpenedAsync();
        Assert.AreEqual("Rename", dialog.Title);
        CollectionAssert.Contains(Texts(dialog), "New name?");
        CollectionAssert.AreEqual(new[] { "Save", "Cancel" }, Buttons(dialog).Select(b => (string)b.Content).ToArray());
        Click(ButtonOf(dialog, "Save"));
        Assert.AreEqual(RouteableContentDialogResult.Primary, await ResultAsync(shown));
        await ClosedAsync();
    }

    /// <summary>The secondary and close buttons of a content dialog return their respective results.</summary>
    [TestMethod]
    public async Task The_secondary_and_close_buttons_give_their_results()
    {
        var service = new DefaultMessageBoxService();
        var secondary = service.ShowAsync(new RouteableContentDialog
        {
            Title = "t",
            SecondaryButtonText = "Other",
            IsSecondaryButtonEnabled = true,
            CloseButtonText = "Close"
        }, Root);
        Click(ButtonOf(await OpenedAsync(), "Other"));
        Assert.AreEqual(RouteableContentDialogResult.Secondary, await ResultAsync(secondary));
        await ClosedAsync();
        var close = service.ShowAsync(new RouteableContentDialog { Title = "t", CloseButtonText = "Close" }, Root);
        Click(ButtonOf(await OpenedAsync(), "Close"));
        Assert.AreEqual(RouteableContentDialogResult.None, await ResultAsync(close));
        await ClosedAsync();
    }

    /// <summary>A content dialog can host an arbitrary control as its content and can show the same control again after close.</summary>
    [TestMethod]
    public async Task A_content_dialog_shows_a_control_as_it_is_and_can_show_it_again()
    {
        var body = new TextBox { Text = "typed" };
        var service = new DefaultMessageBoxService();
        for (var round = 0; round < 2; round++)
        {
            var shown = service.ShowAsync(new RouteableContentDialog { Title = "t", Content = body, CloseButtonText = "Close" }, Root);
            var dialog = await OpenedAsync();
            Assert.IsTrue(Descendants(dialog).Contains(body));
            Click(ButtonOf(dialog, "Close"));
            await ResultAsync(shown);
            await ClosedAsync();
        }
    }

    /// <summary>A disabled primary button cannot be pressed; its command can enable and disable it dynamically.</summary>
    [TestMethod]
    public async Task A_disabled_button_cannot_be_pressed_and_a_command_enables_it()
    {
        var command = new FlagCommand(false);
        var shown = new DefaultMessageBoxService().ShowAsync(new RouteableContentDialog
        {
            Title = "t",
            PrimaryButtonText = "Save",
            IsPrimaryButtonEnabled = true,
            PrimaryButtonCommand = command,
            CloseButtonText = "Close"
        }, Root);
        var dialog = await OpenedAsync();
        var save = ButtonOf(dialog, "Save");
        Assert.IsFalse(save.IsEnabled);
        command.Set(true);
        await TestHost.UntilAsync(() => save.IsEnabled);
        command.Set(false);
        await TestHost.UntilAsync(() => !save.IsEnabled);
        Click(ButtonOf(dialog, "Close"));
        await ResultAsync(shown);
    }

    /// <summary>Opening, opened and closed callbacks of a content dialog each run once, in that order.</summary>
    [TestMethod]
    public async Task The_callbacks_of_a_content_dialog_run_once_each_in_order()
    {
        var events = new List<string>();
        var shown = new DefaultMessageBoxService().ShowAsync(new RouteableContentDialog
        {
            Title = "t",
            CloseButtonText = "Close",
            Opening = () => events.Add("opening"),
            Opened = () => events.Add("opened"),
            Closed = () => events.Add("closed")
        }, Root);
        var dialog = await OpenedAsync();
        await TestHost.UntilAsync(() => events.Contains("opened"));
        Click(ButtonOf(dialog, "Close"));
        await ResultAsync(shown);
        CollectionAssert.AreEqual(new[] { "opening", "opened", "closed" }, events);
    }

    /// <summary>A dismissal the dialog vetoes keeps it open until the dialog agrees to close.</summary>
    [TestMethod]
    public async Task A_dismissal_the_dialog_vetoes_keeps_it_open_until_it_agrees()
    {
        var veto = true;
        var shown = new DefaultMessageBoxService().ShowAsync(new RouteableContentDialog
        {
            Title = "t",
            CloseButtonText = "Close",
            Closing = _ => veto
        }, Root);
        var dialog = await OpenedAsync();
        dialog.Hide();
        await Task.Delay(100);
        Assert.IsFalse(shown.IsCompleted);
        Assert.IsNotNull(OpenDialog());
        veto = false;
        dialog.Hide();
        Assert.AreEqual(RouteableContentDialogResult.None, await ResultAsync(shown));
        await ClosedAsync();
    }

    /// <summary>The caller can close a content dialog from code, including from a background thread.</summary>
    [TestMethod]
    public async Task The_caller_can_close_a_content_dialog_from_code_and_from_another_thread()
    {
        var parameters = new RouteableContentDialog { Title = "t", CloseButtonText = "Close" };
        var shown = new DefaultMessageBoxService().ShowAsync(parameters, Root);
        await OpenedAsync();
        Assert.IsNotNull(parameters.Close);
        await Task.Run(() => parameters.Close!());
        Assert.AreEqual(RouteableContentDialogResult.None, await ResultAsync(shown));
        Assert.IsNull(parameters.Close);
        await ClosedAsync();
    }

    /// <summary>A task dialog shows its title, header, sub-header and one button per supplied result.</summary>
    [TestMethod]
    public async Task A_task_dialog_shows_its_header_its_sub_header_and_one_button_for_each_result()
    {
        var shown = new DefaultMessageBoxService().ShowAsync(
            new RouteableTaskDialog { Title = "Update", Header = "A new version", SubHeader = "Install it now?" },
            Root, RouteableTaskDialogResult.Yes, RouteableTaskDialogResult.Cancel);
        var dialog = await OpenedAsync();
        Assert.AreEqual("Update", dialog.Title);
        CollectionAssert.Contains(Texts(dialog), "A new version");
        CollectionAssert.Contains(Texts(dialog), "Install it now?");
        CollectionAssert.AreEqual(new[] { "Yes", "Cancel" }, Buttons(dialog).Select(b => (string)b.Content).ToArray());
        Click(ButtonOf(dialog, "Yes"));
        Assert.AreEqual(RouteableTaskDialogResult.Yes, await ResultAsync(shown));
        await ClosedAsync();
    }

    /// <summary>A task dialog without results offers a single Close button; a window dismissal returns None.</summary>
    [TestMethod]
    public async Task A_task_dialog_without_results_has_one_close_button_and_a_dismissal_gives_none()
    {
        var service = new DefaultMessageBoxService();
        var closed = service.ShowAsync(new RouteableTaskDialog { Title = "t", Header = "h" }, Root);
        var dialog = await OpenedAsync();
        CollectionAssert.AreEqual(new[] { "Close" }, Buttons(dialog).Select(b => (string)b.Content).ToArray());
        Click(ButtonOf(dialog, "Close"));
        Assert.AreEqual(RouteableTaskDialogResult.Close, await ResultAsync(closed));
        await ClosedAsync();
        var dismissed = service.ShowAsync(new RouteableTaskDialog { Title = "t", Header = "h" }, Root);
        (await OpenedAsync()).Hide();
        Assert.AreEqual(RouteableTaskDialogResult.None, await ResultAsync(dismissed));
        await ClosedAsync();
    }

    /// <summary>A task dialog that vetoes closing stays open until it agrees.</summary>
    [TestMethod]
    public async Task A_task_dialog_that_vetoes_the_closing_stays_open()
    {
        var veto = true;
        var shown = new DefaultMessageBoxService().ShowAsync(new RouteableTaskDialog { Title = "t", Header = "h", Closing = () => veto }, Root);
        var dialog = await OpenedAsync();
        dialog.Hide();
        await Task.Delay(100);
        Assert.IsFalse(shown.IsCompleted);
        veto = false;
        dialog.Hide();
        Assert.AreEqual(RouteableTaskDialogResult.None, await ResultAsync(shown));
        await ClosedAsync();
    }

    /// <summary>ShowAsync throws when the dialog parameters are null.</summary>
    [TestMethod]
    public async Task The_dialogs_refuse_no_parameters()
    {
        var service = new DefaultMessageBoxService();
        await Refused<ArgumentNullException>(() => service.ShowAsync((RouteableContentDialog)null!, Root));
        await Refused<ArgumentNullException>(() => service.ShowAsync((RouteableTaskDialog)null!, Root));
    }

    /// <summary>A root is considered displayable while its content is visible.</summary>
    [TestMethod]
    public void A_root_is_displayable_while_its_content_is_visible()
    {
        Assert.IsTrue(new DefaultMessageBoxService().IsDisplayable(Root));
    }

    /// <summary>Asserts that <paramref name="action"/> throws an exception of type <typeparamref name="T"/>.</summary>
    private static async Task Refused<T>(Func<Task> action) where T : Exception
    {
        try
        {
            await action();
        }
        catch (T)
        {
            return;
        }
        Assert.Fail($"{typeof(T).Name} was expected.");
    }
}