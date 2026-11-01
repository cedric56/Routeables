using Xunit;
using static Microsoft.Playwright.Assertions;

namespace Routeables.Test.Playwright;

/// <summary>The menu, the browser address and the browser history, kept in step with the router by <c>RouteableUrlSync</c>.</summary>
[Collection("Blazor")]
public sealed class NavigationTests(BlazorApp app) : BlazorTest(app)
{
    [Fact]
    public async Task Choosing_a_menu_entry_shows_the_page_and_changes_the_address()
    {
        await OpenAsync("Dogs");

        await ExpectAddressAsync("/dogs");
        await ExpectCurrentAsync("Dogs");
        Assert.Empty(PageErrors);
    }

    [Fact]
    public async Task The_browser_Back_and_Forward_buttons_follow_the_router()
    {
        await OpenAsync("Cats");
        await ExpectAddressAsync("/cats");
        await OpenAsync("Dogs");
        await ExpectAddressAsync("/dogs");

        await Page.GoBackAsync();
        await ExpectAddressAsync("/cats");
        await ExpectCurrentAsync("Cats");

        await Page.GoForwardAsync();
        await ExpectAddressAsync("/dogs");
        await ExpectCurrentAsync("Dogs");
    }

    [Fact]
    public async Task A_typed_address_opens_the_page_it_names()
    {
        await Page.GotoAsync(BaseUrl + "bears");

        await Menu.First.WaitForAsync();
        await ExpectCurrentAsync("Bears");
        await ExpectAddressAsync("/bears");
    }

    [Fact]
    public async Task Each_browser_context_has_its_own_router()
    {
        await OpenAsync("Dogs");
        await ExpectCurrentAsync("Dogs");

        // A second visitor starts where nobody has been: the first one's page is not shared.
        var other = await Page.Context.Browser!.NewContextAsync();
        try
        {
            var page = await other.NewPageAsync();
            await page.GotoAsync(BaseUrl);
            await page.Locator("nav[aria-label='Menu'] button.rt-nav-link").First.WaitForAsync();
            await Expect(page.Locator("nav[aria-label='Menu'] button.rt-nav-link[aria-current='page']")).Not.ToContainTextAsync("Dogs");
        }
        finally
        {
            await other.CloseAsync();
        }
    }
}
