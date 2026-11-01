using System.Collections.ObjectModel;
using System.Windows.Input;
using DeviceRunners.UITesting;
using Routeables.Abstraction;
using Routeables.Maui.Services;
using Routeables.Services;
using Xunit;

namespace Routeables.Test.Maui;

/// <summary>
/// The content of the dialogs of MAUI (<see cref="RouteableDialogContents"/>), dressed by <see cref="RouteableNativeDialogLook"/>: the texts, the
/// buttons and what they do, without any window: the content does not know how it is shown.
/// </summary>
public class DialogContentsDeviceTests
{
    private sealed class FlagCommand(bool initial, Action? run = null) : ICommand
    {
        private bool _enabled = initial;
        public event EventHandler? CanExecuteChanged;
        public bool CanExecute(object? parameter) => _enabled;
        public void Execute(object? parameter) => run?.Invoke();

        public void Set(bool enabled)
        {
            _enabled = enabled;
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        }
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

    private static List<string> Texts(IView root) => [.. All(root).OfType<Label>().Select(l => l.Text)];

    private static List<Button> Buttons(IView root) => [.. All(root).OfType<Button>()];

    private static Button ButtonOf(IView root, string text) => Buttons(root).Single(b => b.Text == text);

    private static void Tap(Button button) => ((IButton)button).Clicked();

    // ------------------------------------------------------------------------------------------------ content dialogs

    [UIFact]
    public void A_content_dialog_shows_its_title_and_its_text()
    {
        var parameters = new RouteableContentDialog { Title = "Rename", Content = "New name?", CloseButtonText = "Close" };
        var session = new RouteableContentDialogSession(parameters, () => { });

        var content = RouteableDialogContents.ForContentDialog(parameters, session);

        Assert.Contains("Rename", Texts(content));
        Assert.Contains("New name?", Texts(content));
    }

    [UIFact]
    public void A_content_dialog_shows_a_view_as_it_is()
    {
        var body = new Label { Text = "typed" };
        var parameters = new RouteableContentDialog { Title = "t", Content = body, CloseButtonText = "Close" };

        var content = RouteableDialogContents.ForContentDialog(parameters, new RouteableContentDialogSession(parameters, () => { }));

        Assert.Contains(body, All(content));
    }

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

    [UIFact]
    public void The_buttons_of_a_content_dialog_close_it_with_their_result()
    {
        var parameters = new RouteableContentDialog
        {
            Title = "t", PrimaryButtonText = "Save", IsPrimaryButtonEnabled = true, CloseButtonText = "Cancel"
        };
        var closed = 0;
        var session = new RouteableContentDialogSession(parameters, () => closed++);
        var content = RouteableDialogContents.ForContentDialog(parameters, session);

        Tap(ButtonOf(content, "Save"));

        Assert.Equal(RouteableContentDialogResult.Primary, session.Result);
        Assert.Equal(1, closed);
    }

    [UIFact]
    public void A_disabled_button_of_a_content_dialog_is_not_enabled()
    {
        var parameters = new RouteableContentDialog
        {
            Title = "t", PrimaryButtonText = "Save", IsPrimaryButtonEnabled = false, CloseButtonText = "Cancel"
        };

        var content = RouteableDialogContents.ForContentDialog(parameters, new RouteableContentDialogSession(parameters, () => { }));

        Assert.False(ButtonOf(content, "Save").IsEnabled);
        Assert.True(ButtonOf(content, "Cancel").IsEnabled);
    }

    [UIFact]
    public void A_button_follows_its_command()
    {
        var command = new FlagCommand(false);
        var parameters = new RouteableContentDialog
        {
            Title = "t", PrimaryButtonText = "Save", IsPrimaryButtonEnabled = true, PrimaryButtonCommand = command, CloseButtonText = "Cancel"
        };
        var content = RouteableDialogContents.ForContentDialog(parameters, new RouteableContentDialogSession(parameters, () => { }));
        var save = ButtonOf(content, "Save");
        Assert.False(save.Command!.CanExecute(null));

        command.Set(true);

        Assert.True(save.Command.CanExecute(null));
    }

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

    private static Border TaskContent(RouteableTaskDialog parameters, out RouteableTaskDialogSession session, params RouteableTaskDialogResult[] results)
    {
        session = new RouteableTaskDialogSession(parameters, results, () => { });
        return RouteableDialogContents.ForTaskDialog(parameters, session);
    }

    [UIFact]
    public void A_task_dialog_shows_its_title_its_header_and_its_sub_header()
    {
        var content = TaskContent(new RouteableTaskDialog { Title = "Update", Header = "A new version", SubHeader = "Install it now?" }, out _);

        var texts = Texts(content);
        Assert.Contains("Update", texts);
        Assert.Contains("A new version", texts);
        Assert.Contains("Install it now?", texts);
    }

    [UIFact]
    public void Without_a_title_the_header_is_the_title_and_is_shown_once()
    {
        var content = TaskContent(new RouteableTaskDialog { Header = "Only a header" }, out _);

        Assert.Single(Texts(content), "Only a header");
    }

    [UIFact]
    public void A_task_dialog_has_one_button_for_each_result_and_pressing_one_gives_it()
    {
        var content = TaskContent(new RouteableTaskDialog { Title = "t", Header = "h" }, out var session,
            RouteableTaskDialogResult.Yes, RouteableTaskDialogResult.Cancel);

        Assert.Equal(["Cancel", "Yes"], Buttons(content).Select(b => b.Text).Order());
        Tap(ButtonOf(content, "Yes"));

        Assert.Equal(RouteableTaskDialogResult.Yes, session.Result);
    }

    [UIFact]
    public void A_task_dialog_without_results_has_a_close_button()
    {
        var content = TaskContent(new RouteableTaskDialog { Title = "t", Header = "h" }, out _);

        Assert.Equal(["Close"], Buttons(content).Select(b => b.Text));
    }

    [UIFact]
    public void The_progress_bar_is_shown_only_when_asked()
    {
        var without = TaskContent(new RouteableTaskDialog { Title = "t", Header = "h" }, out _);
        var with = TaskContent(new RouteableTaskDialog { Title = "t", Header = "h", ShowProgressBar = true }, out _);

        Assert.Empty(All(without).OfType<ProgressBar>());
        Assert.Single(All(with).OfType<ProgressBar>());
    }

    [UIFact]
    public void A_collapsible_footer_is_toggled_by_its_button()
    {
        var content = TaskContent(new RouteableTaskDialog
        {
            Title = "t", Header = "h", Footer = "The details", FooterVisibility = RouteableTaskDialogFooterVisibility.Auto, IsFooterExpanded = false
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

    [UIFact]
    public void A_footer_that_starts_expanded_is_visible_and_its_button_offers_to_fold_it()
    {
        var content = TaskContent(new RouteableTaskDialog
        {
            Title = "t", Header = "h", Footer = "The details", FooterVisibility = RouteableTaskDialogFooterVisibility.Auto, IsFooterExpanded = true
        }, out _);

        Assert.Single(Buttons(content).Where(b => b.Text == "Fewer Details"));
        Assert.True(((View)All(content).OfType<Label>().Single(l => l.Text == "The details").Parent).IsVisible);
    }

    [UIFact]
    public void A_footer_that_is_not_shown_is_not_in_the_dialog()
    {
        var content = TaskContent(new RouteableTaskDialog { Title = "t", Header = "h", Footer = "The details", FooterVisibility = RouteableTaskDialogFooterVisibility.Never }, out _);

        Assert.DoesNotContain("The details", Texts(content));
    }

    // ------------------------------------------------------------------------------------------------ modals

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

    [UIFact]
    public void A_command_without_a_name_gives_no_button_and_a_modal_without_commands_has_none()
    {
        var unnamed = new Closeable("t", new RouteableCommand { Name = " ", Command = new FlagCommand(true) });
        var none = new Closeable("t");

        Assert.Empty(Buttons(RouteableDialogContents.ForModal(unnamed, new Label())));
        Assert.Empty(Buttons(RouteableDialogContents.ForModal(none, new Label())));
    }

    // ------------------------------------------------------------------------------------------------ the look

    [UIFact]
    public void The_dialog_is_dressed_like_the_platform_it_runs_on()
    {
        var expected = DeviceInfo.Platform == DevicePlatform.Android ? 28
            : DeviceInfo.Platform == DevicePlatform.iOS || DeviceInfo.Platform == DevicePlatform.MacCatalyst ? 14
            : 8;

        Assert.Equal(expected, RouteableNativeDialogLook.CornerRadius);
    }

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

    [UIFact]
    public async Task A_button_whose_action_throws_does_not_end_the_application_and_can_be_tapped_again()
    {
        var tapped = 0;
        var frame = RouteableNativeDialogLook.Build("t", new Label(), [new DialogButton("Go", true, true, () => { tapped++; throw new InvalidOperationException("boom"); })]);
        var button = Buttons(frame).Single();

        Tap(button);
        await Device.UntilAsync(() => { Tap(button); return tapped >= 2; });
    }

    [UIFact]
    public void The_text_helper_gives_a_label_of_the_size_and_the_attributes_asked_for()
    {
        var label = RouteableNativeDialogLook.Text("hello", 20, FontAttributes.Bold);

        Assert.Equal("hello", label.Text);
        Assert.Equal(20, label.FontSize);
        Assert.Equal(FontAttributes.Bold, label.FontAttributes);
    }
}
