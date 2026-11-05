using Microsoft.Playwright;
using System.Text.RegularExpressions;
using Xunit;
using static Microsoft.Playwright.Assertions;

namespace Routeables.Test.Playwright;

/// <summary>
/// One page in a browser context of its own, opened on the sample. The menu is the drawer of <c>MainLayout</c> of Routeables.Blazor:
/// <c>nav[aria-label=Menu]</c>, one <c>button.rt-nav-link</c> per entry, the current one with <c>aria-current="page"</c>.
/// </summary>
public abstract class BlazorTest(BlazorApp app) : IAsyncLifetime
{
    private IBrowserContext _context = null!;
    private readonly List<string> _errors = [];

    /// <summary>The Playwright page used by the test.</summary>
    protected IPage Page { get; private set; } = null!;

    /// <summary>The base URL of the running sample.</summary>
    protected string BaseUrl => app.BaseUrl;

    /// <summary>The errors of the page (exceptions that nobody caught) seen so far.</summary>
    protected IReadOnlyList<string> PageErrors => _errors;

    /// <summary>Opens a new browser context and page on the sample, waiting until the menu is drawn.</summary>
    public virtual async ValueTask InitializeAsync()
    {
        _context = await app.Browser.NewContextAsync(new BrowserNewContextOptions { ViewportSize = new ViewportSize { Width = 1280, Height = 900 } });
        Page = await _context.NewPageAsync();
        Page.SetDefaultTimeout(15_000);
        Page.PageError += (_, message) => _errors.Add(message);

        await Page.GotoAsync(app.BaseUrl);
        await Menu.First.WaitForAsync();   // the circuit is connected: nothing is drawn before
    }

    /// <summary>Closes the browser context of the test.</summary>
    public async ValueTask DisposeAsync() => await _context.CloseAsync();

    /// <summary>All navigation links in the menu drawer.</summary>
    protected ILocator Menu => Page.Locator("nav[aria-label='Menu'] button.rt-nav-link");

    /// <summary>The menu link whose name equals <paramref name="name"/>.</summary>
    protected ILocator MenuItem(string name) => Page.Locator($"nav[aria-label='Menu'] button.rt-nav-link:has(.rt-nav-name:text-is('{name}'))");

    /// <summary>The currently selected menu link (<c>aria-current="page"</c>).</summary>
    protected ILocator CurrentMenuItem => Page.Locator("nav[aria-label='Menu'] button.rt-nav-link[aria-current='page']");

    /// <summary>Clicks the menu item with the given name.</summary>
    protected Task OpenAsync(string name) => MenuItem(name).ClickAsync();

    /// <summary>Asserts that the page URL ends with <paramref name="path"/>.</summary>
    protected Task ExpectAddressAsync(string path) => Expect(Page).ToHaveURLAsync(new Regex(Regex.Escape(path) + "$"));

    /// <summary>Asserts that the current menu item contains <paramref name="name"/>.</summary>
    protected Task ExpectCurrentAsync(string name) => Expect(CurrentMenuItem).ToContainTextAsync(name);

    /// <summary>Finds a button by its accessible name.</summary>
    protected ILocator Button(string name) => Page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = name, Exact = true });
}