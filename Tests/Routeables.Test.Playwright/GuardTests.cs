using Xunit;
using static Microsoft.Playwright.Assertions;

namespace Routeables.Test.Playwright;

/// <summary>Refusals: a route guard that turns a visitor away, and a page that refuses to be left. The address always goes back to the page that stays.</summary>
[Collection("Blazor")]
public sealed class GuardTests(BlazorApp app) : BlazorTest(app)
{
    [Fact]
    public async Task Members_turns_a_signed_out_visitor_back_to_Features()
    {
        await OpenAsync("Features");
        await ExpectAddressAsync("/features");

        await Button("Open Members").ClickAsync();

        await ExpectAddressAsync("/features");
        await ExpectCurrentAsync("Features");
        Assert.Empty(PageErrors);
    }

    [Fact]
    public async Task A_typed_address_of_Members_is_redirected_when_signed_out()
    {
        await Page.GotoAsync(BaseUrl + "members");

        await Menu.First.WaitForAsync();
        await ExpectAddressAsync("/features");
        await ExpectCurrentAsync("Features");
    }

    [Fact]
    public async Task Once_signed_in_Members_opens_and_signing_out_leaves_it()
    {
        await OpenAsync("Features");
        await Button("Sign in").ClickAsync();
        await Expect(Page.GetByText("Signed in: Open Members now goes there.")).ToBeVisibleAsync();

        await Button("Open Members").ClickAsync();

        await ExpectAddressAsync("/members");
        await Expect(Page.GetByText("You are signed in.")).ToBeVisibleAsync();

        await Button("Sign out").ClickAsync();

        await ExpectAddressAsync("/features");
    }

    [Fact]
    public async Task A_page_that_refuses_to_be_left_keeps_the_history_where_it_is()
    {
        await OpenAsync("History");
        var cards = Page.Locator(".nav-card");
        var block = Page.Locator("label.nav-check input[type=checkbox]");
        var refused = Page.Locator("p.nav-status.refused");

        await Button("＋ Push").ClickAsync();
        await Expect(cards).ToHaveCountAsync(2);

        await block.CheckAsync();
        await Button("＋ Push").ClickAsync();

        await Expect(refused).ToContainTextAsync("refused");
        await Expect(cards).ToHaveCountAsync(2);

        await Button("◀ Back").ClickAsync();
        await Expect(refused).ToContainTextAsync("refused");
        await Expect(cards).ToHaveCountAsync(2);

        await block.UncheckAsync();
        await Expect(refused).ToHaveCountAsync(0);
        await Button("＋ Push").ClickAsync();
        await Expect(cards).ToHaveCountAsync(3);
    }

    [Fact]
    public async Task A_Back_of_the_browser_that_the_page_refuses_leaves_the_browser_history_untouched()
    {
        await OpenAsync("Cats");
        await ExpectAddressAsync("/cats");
        await OpenAsync("History");
        await ExpectAddressAsync("/history");
        await Page.Locator("label.nav-check input[type=checkbox]").CheckAsync();

        await Page.GoBackAsync();

        // The browser is put back on the page that refuses to be left...
        await ExpectAddressAsync("/history");
        await ExpectCurrentAsync("History");

        // ...and its entries are the ones it had: once the page lets go, Back reaches Cats instead of skipping it.
        await Page.Locator("label.nav-check input[type=checkbox]").UncheckAsync();
        await Page.GoBackAsync();

        await ExpectAddressAsync("/cats");
        await ExpectCurrentAsync("Cats");
        Assert.Empty(PageErrors);
    }
}
