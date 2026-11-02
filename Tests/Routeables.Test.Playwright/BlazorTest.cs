using System.Text.RegularExpressions;
using Microsoft.Playwright;
using Xunit;
using static Microsoft.Playwright.Assertions;

namespace Routeables.Test.Playwright;

/// <summary>
/// One page in a browser context of its own, opened on the sample. The menu is the drawer of <c>MainLayout</c> of Routeables.Razor:
/// <c>nav[aria-label=Menu]</c>, one <c>button.rt-nav-link</c> per entry, the current one with <c>aria-current="page"</c>.
/// </summary>
public abstract class BlazorTest(BlazorApp app) : IAsyncLifetime
{
    private IBrowserContext _context = null!;
    private readonly List<string> _errors = [];

    protected IPage Page { get; private set; } = null!;

    protected string BaseUrl => app.BaseUrl;

    /// <summary>The errors of the page (exceptions that nobody caught) seen so far.</summary>
    protected IReadOnlyList<string> PageErrors => _errors;

    public virtual async ValueTask InitializeAsync()
    {
        _context = await app.Browser.NewContextAsync(new BrowserNewContextOptions { ViewportSize = new ViewportSize { Width = 1280, Height = 900 } });
        Page = await _context.NewPageAsync();
        Page.SetDefaultTimeout(15_000);
        Page.PageError += (_, message) => _errors.Add(message);

        await Page.GotoAsync(app.BaseUrl);
        await Menu.First.WaitForAsync();   // the circuit is connected: nothing is drawn before
    }

    public async ValueTask DisposeAsync() => await _context.CloseAsync();

    protected ILocator Menu => Page.Locator("nav[aria-label='Menu'] button.rt-nav-link");

    protected ILocator MenuItem(string name) => Page.Locator($"nav[aria-label='Menu'] button.rt-nav-link:has(.rt-nav-name:text-is('{name}'))");

    protected ILocator CurrentMenuItem => Page.Locator("nav[aria-label='Menu'] button.rt-nav-link[aria-current='page']");

    protected Task OpenAsync(string name) => MenuItem(name).ClickAsync();

    protected Task ExpectAddressAsync(string path) => Expect(Page).ToHaveURLAsync(new Regex(Regex.Escape(path) + "$"));

    protected Task ExpectCurrentAsync(string name) => Expect(CurrentMenuItem).ToContainTextAsync(name);

    protected ILocator Button(string name) => Page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = name, Exact = true });
}
