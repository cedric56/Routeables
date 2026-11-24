using Bunit;
using Routeables.Blazor.Components;
using Routeables.Blazor.Services;
using Routeables.Services;
using ICommand = System.Windows.Input.ICommand;
using KeyboardEventArgs = Microsoft.AspNetCore.Components.Web.KeyboardEventArgs;

namespace Routeables.Tests.Razor;

/// <summary>
/// What a person does to the dialog hosts: Escape, a click on the backdrop, a button, the footer of a task dialog. The hosts are
/// rendered with bUnit, which can click and press keys in the markup (the static renderer of the other tests cannot).
/// </summary>
public sealed class DialogHostInteractionTests : BunitContext
{
    private static CancellationToken Ct => Xunit.TestContext.Current.CancellationToken;

    private static readonly TimeSpan Wait = TimeSpan.FromSeconds(5);

    private sealed class Command(bool canExecute) : ICommand
    {
        public int Executed { get; private set; }
        public event EventHandler? CanExecuteChanged { add { } remove { } }
        public bool CanExecute(object? parameter) => canExecute;
        public void Execute(object? parameter) => Executed++;
    }

    public DialogHostInteractionTests()
    {
        // The focus of a dialog that opens is a call to the browser: nothing answers it here, and nothing needs to.
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddScoped<RouteableMessageQueue>();
        Services.AddScoped<RouteableContentDialogQueue>();
        Services.AddScoped<RouteableTaskDialogQueue>();
    }

    private static KeyboardEventArgs Key(string key) => new() { Key = key };

    // ---- message boxes ----

    [Fact]
    public async Task A_message_box_is_dismissed_by_Escape_and_by_the_backdrop()
    {
        var queue = Services.GetRequiredService<RouteableMessageQueue>();
        var cut = Render<RouteableDialogHost>();
        var service = new DefaultMessageBoxService(queue, new RouteableContentDialogQueue(), new RouteableTaskDialogQueue());

        var escape = service.ShowMessageAsync("Title", "Message", "YesNo");
        cut.WaitForAssertion(() => cut.Find(".rt-dialog"));
        cut.Find(".rt-dialog").KeyDown(Key("Enter"));       // another key does nothing
        Assert.False(escape.IsCompleted);
        cut.Find(".rt-dialog").KeyDown(Key("Escape"));
        Assert.Equal(1, await escape.WaitAsync(Wait, Ct));  // the last button, No

        var backdrop = service.ShowMessageAsync("Title", "Message", "YesNoCancel");
        cut.WaitForAssertion(() => cut.Find(".rt-backdrop"));
        cut.Find(".rt-backdrop").Click();
        Assert.Equal(2, await backdrop.WaitAsync(Wait, Ct));  // Cancel

        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(".rt-dialog")));
    }

    [Fact]
    public async Task A_message_box_answers_with_the_button_that_is_pressed()
    {
        var queue = Services.GetRequiredService<RouteableMessageQueue>();
        var cut = Render<RouteableDialogHost>();
        var service = new DefaultMessageBoxService(queue, new RouteableContentDialogQueue(), new RouteableTaskDialogQueue());

        var answer = service.ShowMessageAsync("Title", "Message", "YesNoCancel");
        cut.WaitForAssertion(() => cut.Find(".rt-dialog"));
        cut.FindAll("button").Single(b => b.TextContent.Trim() == "No").Click();

        Assert.Equal(1, await answer.WaitAsync(Wait, Ct));
    }

    // ---- content dialogs ----

    [Fact]
    public async Task A_content_dialog_is_dismissed_by_Escape_and_by_the_backdrop()
    {
        var queue = Services.GetRequiredService<RouteableContentDialogQueue>();
        var cut = Render<RouteableContentDialogHost>();

        var escape = queue.ShowAsync(new RouteableContentDialog { Title = "Edit", CloseButtonText = "Close" });
        cut.WaitForAssertion(() => cut.Find(".rt-dialog"));
        cut.Find(".rt-dialog").KeyDown(Key("Tab"));
        Assert.False(escape.IsCompleted);
        cut.Find(".rt-dialog").KeyDown(Key("Escape"));
        Assert.Equal(RouteableContentDialogResult.None, await escape.WaitAsync(Wait, Ct));

        var backdrop = queue.ShowAsync(new RouteableContentDialog { Title = "Edit", CloseButtonText = "Close" });
        cut.WaitForAssertion(() => cut.Find(".rt-backdrop"));
        cut.Find(".rt-backdrop").Click();
        Assert.Equal(RouteableContentDialogResult.None, await backdrop.WaitAsync(Wait, Ct));
    }

    [Fact]
    public async Task A_button_whose_command_cannot_execute_is_disabled_and_the_other_ones_close_the_dialog()
    {
        var queue = Services.GetRequiredService<RouteableContentDialogQueue>();
        var cut = Render<RouteableContentDialogHost>();
        var primary = new Command(canExecute: false);

        var task = queue.ShowAsync(new RouteableContentDialog
        {
            Title = "Edit",
            PrimaryButtonText = "Save",
            IsPrimaryButtonEnabled = true,
            PrimaryButtonCommand = primary,
            CloseButtonText = "Close"
        });
        cut.WaitForAssertion(() => cut.Find(".rt-dialog"));

        var save = cut.FindAll("button").Single(b => b.TextContent.Trim() == "Save");
        Assert.True(save.HasAttribute("disabled"));
        Assert.False(cut.FindAll("button").Single(b => b.TextContent.Trim() == "Close").HasAttribute("disabled"));

        cut.FindAll("button").Single(b => b.TextContent.Trim() == "Close").Click();

        Assert.Equal(RouteableContentDialogResult.None, await task.WaitAsync(Wait, Ct));
        Assert.Equal(0, primary.Executed);
    }

    [Fact]
    public async Task A_button_whose_command_can_execute_runs_it_and_the_dialog_is_shown_again_for_the_next_one()
    {
        var queue = Services.GetRequiredService<RouteableContentDialogQueue>();
        var cut = Render<RouteableContentDialogHost>();
        var primary = new Command(canExecute: true);

        var first = queue.ShowAsync(new RouteableContentDialog
        {
            Title = "First", PrimaryButtonText = "Save", IsPrimaryButtonEnabled = true, PrimaryButtonCommand = primary, CloseButtonText = "Close"
        });
        var second = queue.ShowAsync(new RouteableContentDialog { Title = "Second", CloseButtonText = "Close" });
        cut.WaitForAssertion(() => Assert.Contains("First", cut.Markup));

        cut.FindAll("button").Single(b => b.TextContent.Trim() == "Save").Click();

        Assert.Equal(RouteableContentDialogResult.Primary, await first.WaitAsync(Wait, Ct));
        Assert.Equal(1, primary.Executed);
        cut.WaitForAssertion(() => Assert.Contains("Second", cut.Markup));
        cut.Find(".rt-dialog").KeyDown(Key("Escape"));
        Assert.Equal(RouteableContentDialogResult.None, await second.WaitAsync(Wait, Ct));
    }

    // ---- task dialogs ----

    [Fact]
    public async Task A_task_dialog_is_dismissed_by_Escape_and_by_the_backdrop()
    {
        var queue = Services.GetRequiredService<RouteableTaskDialogQueue>();
        var cut = Render<RouteableTaskDialogHost>();

        var escape = queue.ShowAsync(new RouteableTaskDialog { Title = "Work" }, RouteableTaskDialogResult.Close);
        cut.WaitForAssertion(() => cut.Find(".rt-dialog"));
        cut.Find(".rt-dialog").KeyDown(Key("Escape"));
        Assert.Equal(RouteableTaskDialogResult.None, await escape.WaitAsync(Wait, Ct));

        var backdrop = queue.ShowAsync(new RouteableTaskDialog { Title = "Work" }, RouteableTaskDialogResult.Close);
        cut.WaitForAssertion(() => cut.Find(".rt-backdrop"));
        cut.Find(".rt-backdrop").Click();
        Assert.Equal(RouteableTaskDialogResult.None, await backdrop.WaitAsync(Wait, Ct));
    }

    [Fact]
    public async Task The_footer_of_a_task_dialog_opens_and_closes_with_its_button_and_starts_as_the_dialog_asks()
    {
        var queue = Services.GetRequiredService<RouteableTaskDialogQueue>();
        var cut = Render<RouteableTaskDialogHost>();

        var closed = queue.ShowAsync(
            new RouteableTaskDialog { Title = "Work", Footer = "details", FooterVisibility = RouteableTaskDialogFooterVisibility.Auto },
            RouteableTaskDialogResult.Close);
        cut.WaitForAssertion(() => cut.Find(".rt-footer-toggle"));
        var toggle = cut.Find(".rt-footer-toggle");
        Assert.Equal("More details", toggle.TextContent.Trim());
        Assert.Equal("false", toggle.GetAttribute("aria-expanded"));

        toggle.Click();

        toggle = cut.Find(".rt-footer-toggle");
        Assert.Equal("Less details", toggle.TextContent.Trim());
        Assert.Equal("true", toggle.GetAttribute("aria-expanded"));
        toggle.Click();
        Assert.Equal("More details", cut.Find(".rt-footer-toggle").TextContent.Trim());
        cut.FindAll("button").Single(b => b.TextContent.Trim() == "Close").Click();
        Assert.Equal(RouteableTaskDialogResult.Close, await closed.WaitAsync(Wait, Ct));

        // A footer that is always visible has no button to hide it and starts open; one that asks to start open does too.
        var open = queue.ShowAsync(
            new RouteableTaskDialog { Title = "Work", Footer = "details", FooterVisibility = RouteableTaskDialogFooterVisibility.Auto, IsFooterExpanded = true },
            RouteableTaskDialogResult.Close);
        cut.WaitForAssertion(() => Assert.Equal("Less details", cut.Find(".rt-footer-toggle").TextContent.Trim()));
        cut.FindAll("button").Single(b => b.TextContent.Trim() == "Close").Click();
        await open.WaitAsync(Wait, Ct);

        var always = queue.ShowAsync(
            new RouteableTaskDialog { Title = "Work", Footer = "details", FooterVisibility = RouteableTaskDialogFooterVisibility.Always },
            RouteableTaskDialogResult.Close);
        cut.WaitForAssertion(() => cut.Find(".rt-dialog"));
        Assert.Empty(cut.FindAll(".rt-footer-toggle"));
        cut.FindAll("button").Single(b => b.TextContent.Trim() == "Close").Click();
        await always.WaitAsync(Wait, Ct);
    }

    [Fact]
    public async Task A_task_dialog_without_header_sub_header_or_footer_draws_none_of_them()
    {
        var queue = Services.GetRequiredService<RouteableTaskDialogQueue>();
        var cut = Render<RouteableTaskDialogHost>();

        var task = queue.ShowAsync(new RouteableTaskDialog { Title = "Work", Header = " ", SubHeader = "" }, RouteableTaskDialogResult.Close);
        cut.WaitForAssertion(() => cut.Find(".rt-dialog"));

        Assert.Empty(cut.FindAll(".rt-header"));
        Assert.Empty(cut.FindAll(".rt-subheader"));
        Assert.Empty(cut.FindAll(".rt-footer-toggle"));
        Assert.Empty(cut.FindAll(".rt-progress"));
        cut.FindAll("button").Single().Click();
        Assert.Equal(RouteableTaskDialogResult.Close, await task.WaitAsync(Wait, Ct));
    }
}
