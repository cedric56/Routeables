using Routeables.Abstraction;
using Routeables.Abstraction.Bases;
using Routeables.Maui.Services;
using Routeables.Services;
using System.Collections.ObjectModel;
using System.Windows.Input;
using Xunit;

namespace Routeables.Test.Maui;

/// <summary>
/// The content of the dialogs of MAUI (<see cref="RouteableDialogContents"/>), dressed by <see cref="RouteableNativeDialogLook"/>: the texts, the
/// buttons and what they do, without any window: the content does not know how it is shown.
/// </summary>
public class DialogContentsDeviceTests
{
    /// <summary>A command that can execute or not, and can be turned at will: the tests watch the buttons follow it.</summary>
    private sealed class FlagCommand(bool initial, Action? run = null) : ICommand
    {
        private bool _enabled = initial;

        /// <summary>Raised by <see cref="Set"/>, so the buttons redraw themselves.</summary>
        public event EventHandler? CanExecuteChanged;

        /// <summary>Whether the command runs at the moment: the buttons follow it.</summary>
        public bool CanExecute(object? parameter) => _enabled;

        /// <summary>Runs what the test gave, if anything.</summary>
        public void Execute(object? parameter) => run?.Invoke();

        /// <summary>Turns the command on or off and announces it.</summary>
        public void Set(bool enabled)
        {
            _enabled = enabled;
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>A view model that closes with a string, and commands the tests can press.</summary>
    private sealed class Closeable(string title, params RouteableCommand[] commands) : ICloseableViewModel<string>
    {
        /// <summary>The title of the modal, so the content can show it.</summary>
        public string Title => title;

        /// <summary>The commands of the modal, shown as buttons in the content.</summary>
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

    /// <summary>Every view of the tree under <paramref name="root"/>, the root first, whatever layouts stand in the way.</summary>
    private static IEnumerable<IView> All(IView? root)
    {
        switch (root)
        {
            case null:
                yield break;
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

    /// <summary>The texts of the labels under <paramref name="root"/>, in the order of the tree.</summary>
    private static List<string> Texts(IView root) => [.. All(root).OfType<Label>().Select(l => l.Text)];

    /// <summary>Every button under <paramref name="root"/>.</summary>
    private static List<Button> Buttons(IView root) => [.. All(root).OfType<Button>()];

    /// <summary>The button under <paramref name="root"/> that reads <paramref name="text"/>: the one to press.</summary>
    private static Button ButtonOf(IView root, string text) => Buttons(root).Single(b => b.Text == text);

    /// <summary>Presses the button by hand: the click is sent to the control, no finger is needed.</summary>
    private static void Tap(Button button) => ((IButton)button).Clicked();

    // ------------------------------------------------------------------------------------------------ content dialogs

    /// <summary>A content dialog shows the title it was given, and its text as a body.</summary>
    [UIFact]
    public void A_content_dialog_shows_its_title_and_its_text()
    {
        var parameters = new RouteableContentDialog { Title = "Rename", Content = "New name?", CloseButtonText = "Close" };
        var session = new RouteableContentDialogSession(parameters, () => { });

        var content = RouteableDialogContents.ForContentDialog(parameters, session);

        Assert.Contains("Rename", Texts(content));
        Assert.Contains("New name?", Texts(content));
    }

    /// <summary>A content dialog that was given a view shows it as it is, without wrapping it in a text.</summary>
    [UIFact]
    public void A_content_dialog_shows_a_view_as_it_is()
    {
        var body = new Label { Text = "typed" };
        var parameters = new RouteableContentDialog { Title = "t", Content = body, CloseButtonText = "Close" };

        var content = RouteableDialogContents.ForContentDialog(parameters, new RouteableContentDialogSession(parameters, () => { }));

        Assert.Contains(body, All(content));
    }

    /// <summary>A content dialog draws one button for each text it was given: close, secondary, then primary.</summary>
    [UIFact]
    public void A_content_dialog_has_one_button_for_each_text_it_was_given()
    {
        var parameters = new RouteableContentDialog
        {
            Title = "t",
            PrimaryButtonText = "Save",
            IsPrimaryButtonEnabled = true,
            SecondaryButtonText = "Other",
            IsSecondaryButtonEnabled = true,
            CloseButtonText = "Cancel"
        };

        var content = RouteableDialogContents.ForContentDialog(parameters, new RouteableContentDialogSession(parameters, () => { }));

        Assert.Equal(["Cancel", "Other", "Save"], Buttons(content).Select(b => b.Text).Order());
    }

    /// <summary>Pressing a button of a content dialog records its result and closes the session.</summary>
    [UIFact]
    public void The_buttons_of_a_content_dialog_close_it_with_their_result()
    {
        var parameters = new RouteableContentDialog
        {
            Title = "t",
            PrimaryButtonText = "Save",
            IsPrimaryButtonEnabled = true,
            CloseButtonText = "Cancel"
        };
        var closed = 0;
        var session = new RouteableContentDialogSession(parameters, () => closed++);
        var content = RouteableDialogContents.ForContentDialog(parameters, session);

        Tap(ButtonOf(content, "Save"));

        Assert.Equal(RouteableContentDialogResult.Primary, session.Result);
        Assert.Equal(1, closed);
    }

    /// <summary>A button asked disabled is drawn disabled; the others are not touched.</summary>
    [UIFact]
    public void A_disabled_button_of_a_content_dialog_is_not_enabled()
    {
        var parameters = new RouteableContentDialog
        {
            Title = "t",
            PrimaryButtonText = "Save",
            IsPrimaryButtonEnabled = false,
            CloseButtonText = "Cancel"
        };

        var content = RouteableDialogContents.ForContentDialog(parameters, new RouteableContentDialogSession(parameters, () => { }));

        Assert.False(ButtonOf(content, "Save").IsEnabled);
        Assert.True(ButtonOf(content, "Cancel").IsEnabled);
    }

    // A button is drawn once: it is enabled when its command can execute at the moment the dialog opens.
    /// <summary>A button whose command can execute as the dialog opens is enabled.</summary>
    [UIFact]
    public void A_button_is_enabled_when_its_command_can_execute_as_the_dialog_opens()
    {
        var content = Opened(new FlagCommand(true));

        Assert.True(ButtonOf(content, "Save").IsEnabled);
    }

    /// <summary>A button whose command cannot execute as the dialog opens is disabled, and the others are not.</summary>
    [UIFact]
    public void A_button_is_disabled_when_its_command_cannot_execute_as_the_dialog_opens()
    {
        var content = Opened(new FlagCommand(false));

        Assert.False(ButtonOf(content, "Save").IsEnabled);
        Assert.True(ButtonOf(content, "Cancel").IsEnabled);
    }

    /// <summary>A dialog with a primary button bound to the command of the test, ready to be read.</summary>
    private static Border Opened(ICommand command)
    {
        var parameters = new RouteableContentDialog
        {
            Title = "t",
            PrimaryButtonText = "Save",
            IsPrimaryButtonEnabled = true,
            PrimaryButtonCommand = command,
            CloseButtonText = "Cancel"
        };
        return RouteableDialogContents.ForContentDialog(parameters, new RouteableContentDialogSession(parameters, () => { }));
    }

    /// <summary>A content or a task dialog without its parameters, or without its session, is refused.</summary>
    [UIFact]
    public void A_null_dialog_and_a_null_session_are_refused()
    {
        var parameters = new RouteableContentDialog { Title = "t" };
        var session = new RouteableContentDialogSession(parameters, () => { });

        Assert.Throws<ArgumentNullException>(() => RouteableDialogContents.ForContentDialog(null!, session));
        Assert.Throws<ArgumentNullException>(() => RouteableDialogContents.ForContentDialog(parameters, null!));
        Assert.Throws<ArgumentNullException>(() => RouteableDialogContents.ForTaskDialog(null!, new RouteableTaskDialogSession(new RouteableTaskDialog(), null, () => { })));
        Assert.Throws<ArgumentNullException>(() => RouteableDialogContents.ForTaskDialog(new RouteableTaskDialog(), null!));
    }

    // ------------------------------------------------------------------------------------------------ task dialogs

    /// <summary>The content of the task dialog of the test, with its session given back to the caller.</summary>
    private static Border TaskContent(RouteableTaskDialog parameters, out RouteableTaskDialogSession session, params RouteableTaskDialogResult[] results)
    {
        session = new RouteableTaskDialogSession(parameters, results, () => { });
        return RouteableDialogContents.ForTaskDialog(parameters, session);
    }

    /// <summary>A task dialog shows its title, its header, and its sub-header under the header.</summary>
    [UIFact]
    public void A_task_dialog_shows_its_title_its_header_and_its_sub_header()
    {
        var content = TaskContent(new RouteableTaskDialog { Title = "Update", Header = "A new version", SubHeader = "Install it now?" }, out _);

        var texts = Texts(content);
        Assert.Contains("Update", texts);
        Assert.Contains("A new version", texts);
        Assert.Contains("Install it now?", texts);
    }

    /// <summary>Without a title, the header takes its place and is shown once, not twice.</summary>
    [UIFact]
    public void Without_a_title_the_header_is_the_title_and_is_shown_once()
    {
        var content = TaskContent(new RouteableTaskDialog { Header = "Only a header" }, out _);

        Assert.Single(Texts(content), "Only a header");
    }

    /// <summary>A task dialog draws one button per offered result, and pressing one gives that result to the session.</summary>
    [UIFact]
    public void A_task_dialog_has_one_button_for_each_result_and_pressing_one_gives_it()
    {
        var content = TaskContent(new RouteableTaskDialog { Title = "t", Header = "h" }, out var session,
            RouteableTaskDialogResult.Yes, RouteableTaskDialogResult.Cancel);

        Assert.Equal(["Cancel", "Yes"], Buttons(content).Select(b => b.Text).Order());
        Tap(ButtonOf(content, "Yes"));

        Assert.Equal(RouteableTaskDialogResult.Yes, session.Result);
    }

    /// <summary>A task dialog asked without results offers a single Close button.</summary>
    [UIFact]
    public void A_task_dialog_without_results_has_a_close_button()
    {
        var content = TaskContent(new RouteableTaskDialog { Title = "t", Header = "h" }, out _);

        Assert.Equal(["Close"], Buttons(content).Select(b => b.Text));
    }

    /// <summary>The progress bar appears only when the dialog asked for one.</summary>
    [UIFact]
    public void The_progress_bar_is_shown_only_when_asked()
    {
        var without = TaskContent(new RouteableTaskDialog { Title = "t", Header = "h" }, out _);
        var with = TaskContent(new RouteableTaskDialog { Title = "t", Header = "h", ShowProgressBar = true }, out _);

        Assert.Empty(All(without).OfType<ProgressBar>());
        Assert.Single(All(with).OfType<ProgressBar>());
    }

    /// <summary>The button of a collapsible footer hides it, and pressing it again shows it back: the text follows.</summary>
    [UIFact]
    public void A_collapsible_footer_is_toggled_by_its_button()
    {
        var content = TaskContent(new RouteableTaskDialog
        {
            Title = "t",
            Header = "h",
            Footer = "The details",
            FooterVisibility = RouteableTaskDialogFooterVisibility.Auto,
            IsFooterExpanded = false
        }, out _);
        var toggle = ButtonOf(content, "More Details");
        var footer = All(content).OfType<Label>().Single(l => l.Text == "The details");
        Assert.False(footer.IsVisible && ((View)footer.Parent).IsVisible);

        Tap(toggle);
        Assert.Equal("Fewer Details", toggle.Text);
        Assert.True(((View)footer.Parent).IsVisible);

        Tap(toggle);
        Assert.Equal("More Details", toggle.Text);
        Assert.False(((View)footer.Parent).IsVisible);
    }

    /// <summary>A footer that starts expanded is visible, and its button offers to fold it.</summary>
    [UIFact]
    public void A_footer_that_starts_expanded_is_visible_and_its_button_offers_to_fold_it()
    {
        var content = TaskContent(new RouteableTaskDialog
        {
            Title = "t",
            Header = "h",
            Footer = "The details",
            FooterVisibility = RouteableTaskDialogFooterVisibility.Auto,
            IsFooterExpanded = true
        }, out _);

        Assert.Single(Buttons(content).Where(b => b.Text == "Fewer Details"));
        Assert.True(((View)All(content).OfType<Label>().Single(l => l.Text == "The details").Parent).IsVisible);
    }

    /// <summary>A footer that is never shown is not in the dialog at all.</summary>
    [UIFact]
    public void A_footer_that_is_not_shown_is_not_in_the_dialog()
    {
        var content = TaskContent(new RouteableTaskDialog { Title = "t", Header = "h", Footer = "The details", FooterVisibility = RouteableTaskDialogFooterVisibility.Never }, out _);

        Assert.DoesNotContain("The details", Texts(content));
    }

    // ------------------------------------------------------------------------------------------------ modals

    /// <summary>A modal shows its title, its view, and its first three commands as buttons; the rest is ignored.</summary>
    [UIFact]
    public void A_modal_shows_the_title_the_view_and_the_first_three_commands_as_buttons()
    {
        var ran = new List<string>();
        RouteableCommand Named(string name) => new() { Name = name, Command = new FlagCommand(true, () => ran.Add(name)) };
        var viewModel = new Closeable("Edit", Named("Save"), Named("Reset"), Named("Help"), Named("Ignored"));
        var body = new Label { Text = "the form" };

        var content = RouteableDialogContents.ForModal(viewModel, body);

        Assert.Contains("Edit", Texts(content));
        Assert.Contains(body, All(content));
        Assert.Equal(3, Buttons(content).Count);
        Assert.DoesNotContain(Buttons(content), b => b.Text == "Ignored");

        Tap(ButtonOf(content, "Reset"));
        Assert.Equal(["Reset"], ran);
    }

    /// <summary>A command without a real name gives no button; a modal without commands gives none either.</summary>
    [UIFact]
    public void A_command_without_a_name_gives_no_button_and_a_modal_without_commands_has_none()
    {
        var unnamed = new Closeable("t", new RouteableCommand { Name = " ", Command = new FlagCommand(true) });
        var none = new Closeable("t");

        Assert.Empty(Buttons(RouteableDialogContents.ForModal(unnamed, new Label())));
        Assert.Empty(Buttons(RouteableDialogContents.ForModal(none, new Label())));
    }

    // ------------------------------------------------------------------------------------------------ the look

    /// <summary>The radius of the corners is the one of the platform: rounder on Android, softer on Apple, square on the desktop.</summary>
    [UIFact]
    public void The_dialog_is_dressed_like_the_platform_it_runs_on()
    {
        var expected = DeviceInfo.Platform == DevicePlatform.Android ? 28
            : DeviceInfo.Platform == DevicePlatform.iOS || DeviceInfo.Platform == DevicePlatform.MacCatalyst ? 14
            : 8;

        Assert.Equal(expected, RouteableNativeDialogLook.CornerRadius);
    }

    /// <summary>The look builds a frame with a title, a body and its buttons; without buttons, none is drawn and the body is still there.</summary>
    [UIFact]
    public void The_look_builds_a_frame_with_a_title_a_body_and_its_buttons_and_none_without_buttons()
    {
        var tapped = 0;
        var body = new Label { Text = "body" };

        var with = RouteableNativeDialogLook.Build("Title", body, [new DialogButton("Ok", true, true, () => { tapped++; return Task.CompletedTask; })]);
        var without = RouteableNativeDialogLook.Build(null, body, []);

        Assert.Contains("Title", Texts(with));
        Assert.Single(Buttons(with));
        Assert.Empty(Buttons(without));
        Assert.Contains(body, All(without));
    }

    /// <summary>While the action of a button is running, the button refuses to be tapped again; once it ended, it runs once more.</summary>
    [UIFact]
    public async Task A_button_taps_once_while_its_action_is_running_and_again_when_it_ended()
    {
        var gate = new TaskCompletionSource();
        var tapped = 0;
        var frame = RouteableNativeDialogLook.Build("t", new Label(), [new DialogButton("Go", true, true, () => { tapped++; return gate.Task; })]);
        var button = Buttons(frame).Single();

        Tap(button);
        Tap(button);
        Assert.Equal(1, tapped);

        gate.SetResult();
        await Device.UntilAsync(() => { Tap(button); return tapped == 2; });
    }

    /// <summary>An action that throws does not end the application: the button can be tapped again.</summary>
    [UIFact]
    public async Task A_button_whose_action_throws_does_not_end_the_application_and_can_be_tapped_again()
    {
        var tapped = 0;
        var frame = RouteableNativeDialogLook.Build("t", new Label(), [new DialogButton("Go", true, true, () => { tapped++; throw new InvalidOperationException("boom"); })]);
        var button = Buttons(frame).Single();

        Tap(button);
        await Device.UntilAsync(() => { Tap(button); return tapped >= 2; });
    }

    /// <summary>The text helper gives a label with the text, the size and the attributes that were asked for.</summary>
    [UIFact]
    public void The_text_helper_gives_a_label_of_the_size_and_the_attributes_asked_for()
    {
        var label = RouteableNativeDialogLook.Text("hello", 20, FontAttributes.Bold);

        Assert.Equal("hello", label.Text);
        Assert.Equal(20, label.FontSize);
        Assert.Equal(FontAttributes.Bold, label.FontAttributes);
    }
}