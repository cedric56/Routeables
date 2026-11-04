using Microsoft.Playwright;
using Xunit;
using static Microsoft.Playwright.Assertions;

namespace Routeables.Test.Playwright;

/// <summary>Four zones, four routers: the left menu chooses what the content shows, and each zone keeps its own history.</summary>
[Collection("Blazor")]
public sealed class RegionTests(BlazorApp app) : BlazorTest(app)
{
    /// <summary>The menu items of the left region.</summary>
    private ILocator LeftItems => Page.Locator(".rg-left-region .rg-menu-item");

    /// <summary>The currently selected item of the left region.</summary>
    private ILocator CurrentLeftItem => Page.Locator(".rg-left-region .rg-menu-item.current");

    /// <summary>The Back and Forward buttons of the left region.</summary>
    private ILocator Moves => Page.Locator(".rg-left-region .rg-menu-moves button");

    /// <summary>Opens the Regions page before each test.</summary>
    public override async ValueTask InitializeAsync()
    {
        await base.InitializeAsync();
        await OpenAsync("Regions");
        await ExpectAddressAsync("/regions");
    }

    /// <summary>The four zones are each filled by their own router.</summary>
    [Fact]
    public async Task The_four_zones_are_filled_by_their_own_router()
    {
        await Expect(Page.Locator(".rg-header")).Not.ToBeEmptyAsync();
        await Expect(LeftItems.First).ToBeVisibleAsync();
        await Expect(Page.Locator(".rg-content")).Not.ToBeEmptyAsync();
        await Expect(Page.Locator(".rg-footer")).Not.ToBeEmptyAsync();
        await Expect(CurrentLeftItem).ToHaveCountAsync(1);
    }

    /// <summary>Choosing in the left panel changes the content but not the browser address.</summary>
    [Fact]
    public async Task Choosing_in_the_left_panel_changes_the_content_but_not_the_address()
    {
        var content = Page.Locator(".rg-content");
        var before = await content.InnerTextAsync();
        var first = await CurrentLeftItem.InnerTextAsync();

        await LeftItems.Nth(1).ClickAsync();

        await Expect(LeftItems.Nth(1)).ToHaveClassAsync(new System.Text.RegularExpressions.Regex("current"));
        Assert.NotEqual(first, await CurrentLeftItem.InnerTextAsync());
        await Expect(content).Not.ToHaveTextAsync(before);
        await ExpectAddressAsync("/regions");   // the regions navigate inside the page: the address of the main router stays
    }

    /// <summary>Back and Forward of the left panel walk the history of the content.</summary>
    [Fact]
    public async Task Back_and_Forward_of_the_left_panel_walk_the_history_of_the_content()
    {
        var back = Moves.Nth(0);
        var forward = Moves.Nth(1);
        await Expect(back).ToBeDisabledAsync();

        await LeftItems.Nth(1).ClickAsync();
        await Expect(LeftItems.Nth(1)).ToHaveClassAsync(new System.Text.RegularExpressions.Regex("current"));
        await Expect(back).ToBeEnabledAsync();

        await back.ClickAsync();
        await Expect(LeftItems.First).ToHaveClassAsync(new System.Text.RegularExpressions.Regex("current"));
        await Expect(forward).ToBeEnabledAsync();

        await forward.ClickAsync();
        await Expect(LeftItems.Nth(1)).ToHaveClassAsync(new System.Text.RegularExpressions.Regex("current"));
    }
}