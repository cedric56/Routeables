using Xunit;
using static Microsoft.Playwright.Assertions;

namespace Routeables.Test.Playwright;

/// <summary>The menu, the browser address and the browser history, kept in step with the router by <c>RouteableUrlSync</c>.</summary>
[Collection("Blazor")]
public sealed class NavigationTests(BlazorApp app) : BlazorTest(app)
{
    /// <summary>Choosing a menu entry shows the page and changes the browser address.</summary>
    [Fact]
    public async Task Choosing_a_menu_entry_shows_the_page_and_changes_the_address()
    {
        await OpenAsync("Dogs");

        await ExpectAddressAsync("/dogs");
        await ExpectCurrentAsync("Dogs");
        Assert.Empty(PageErrors);
    }

    /// <summary>The browser Back and Forward buttons follow the router.</summary>
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

    /// <summary>A typed address opens the page it names.</summary>
    [Fact]
    public async Task A_typed_address_opens_the_page_it_names()
    {
        await Page.GotoAsync(BaseUrl + "bears");

        await Menu.First.WaitForAsync();
        await ExpectCurrentAsync("Bears");
        await ExpectAddressAsync("/bears");
    }

    /// <summary>Each browser context has its own router; pages are not shared across visitors.</summary>
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

    /// <summary>
    /// The Back of the detail page is a PopAsync (the router forgets the detail): the browser goes back in its history too (history.go(-1)), so it does not
    /// stay on a second copy of the list, and its Forward button opens the detail again.
    /// </summary>
    [Fact]
    public async Task GoBackAsync_from_a_view_model_goes_back_in_the_browser_history_too()
    {
        await OpenAsync("Dogs");
        await ExpectAddressAsync("/dogs");
        await Page.Locator(".animal-item").First.ClickAsync();
        await Expect(Page.Locator("button.back-btn").First).ToBeVisibleAsync();
        var detail = new Uri(Page.Url).AbsolutePath;

        await Page.Locator("button.back-btn").First.ClickAsync();
        await ExpectAddressAsync("/dogs");

        await Page.GoForwardAsync();

        await ExpectAddressAsync(detail);
    }
}