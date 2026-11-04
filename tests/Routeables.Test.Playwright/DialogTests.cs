using Microsoft.Playwright;
using Xunit;
using static Microsoft.Playwright.Assertions;

namespace Routeables.Test.Playwright;

/// <summary>The dialogs and the notifications of <c>Routeables.Razor</c>, opened by a view model and answered by the visitor.</summary>
[Collection("Blazor")]
public sealed class DialogTests(BlazorApp app) : BlazorTest(app)
{
    /// <summary>The open dialog element (<c>role=alertdialog</c>).</summary>
    private ILocator Dialog => Page.Locator(".rt-dialog[role=alertdialog]");

    /// <summary>The paragraph that displays the result of the last dialog or notification action.</summary>
    private ILocator Result => Page.Locator("p.about-result");

    /// <summary>A button inside the open dialog with the given accessible name.</summary>
    private ILocator DialogButton(string name) => Dialog.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = name, Exact = true });

    /// <summary>Opens the Dialogs page before each test.</summary>
    public override async ValueTask InitializeAsync()
    {
        await base.InitializeAsync();
        await OpenAsync("Dialogs");
        await ExpectAddressAsync("/dialogs");
    }

    /// <summary>A Yes/No dialog returns the answer chosen by the visitor.</summary>
    [Theory]
    [InlineData("Yes", "Answer: Yes.")]
    [InlineData("No", "Answer: No.")]
    public async Task A_yes_no_dialog_returns_the_answer_of_the_visitor(string choice, string expected)
    {
        await Button("Yes / No").ClickAsync();
        await Expect(Dialog).ToBeVisibleAsync();
        await Expect(Dialog).ToContainTextAsync("Add the polar bear to your favorites?");

        await DialogButton(choice).ClickAsync();

        await Expect(Dialog).ToHaveCountAsync(0);
        await Expect(Result).ToHaveTextAsync(expected);
    }

    /// <summary>An Ok/Cancel dialog can be cancelled.</summary>
    [Fact]
    public async Task An_ok_cancel_dialog_can_be_cancelled()
    {
        await Button("Ok / Cancel").ClickAsync();

        await DialogButton("Cancel").ClickAsync();

        await Expect(Result).ToHaveTextAsync("Answer: Cancel.");
    }

    /// <summary>The page underneath does not navigate while a dialog is open.</summary>
    [Fact]
    public async Task The_page_underneath_does_not_move_while_a_dialog_is_open()
    {
        await Button("Yes / No").ClickAsync();
        await Expect(Dialog).ToBeVisibleAsync();

        await ExpectAddressAsync("/dialogs");
        await ExpectCurrentAsync("Dialogs");

        await DialogButton("No").ClickAsync();
    }

    /// <summary>A notification appears with the severity of its type.</summary>
    [Theory]
    [InlineData("Information", "information", "Tip")]
    [InlineData("Success", "success", "Saved")]
    [InlineData("Warning", "warning", "Slow connection")]
    [InlineData("Error", "error", "Download failed")]
    public async Task A_notification_appears_with_its_type(string button, string type, string title)
    {
        // "Error" is also the name of a message button (above): the notification ones are in the last group of the page.
        await Page.Locator("section.about-group").Last.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = button, Exact = true }).ClickAsync();

        var toast = Page.Locator($".rt-toast.rt-{type}");
        await Expect(toast).ToBeVisibleAsync();
        await Expect(toast).ToContainTextAsync(title);
    }

    /// <summary>Closing a notification notifies the view model.</summary>
    [Fact]
    public async Task Closing_a_notification_tells_the_view_model()
    {
        await Page.Locator("section.about-group").Last.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Success", Exact = true }).ClickAsync();
        var toast = Page.Locator(".rt-toast.rt-success");
        await Expect(toast).ToBeVisibleAsync();

        await toast.Locator(".rt-toast-close").ClickAsync();

        await Expect(toast).ToHaveCountAsync(0);
        await Expect(Result).ToHaveTextAsync("Closed the success notification.");
    }
}