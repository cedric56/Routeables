using Microsoft.Playwright;
using System.Text;
using System.Text.Json;
using Xunit;
using static Microsoft.Playwright.Assertions;

namespace Routeables.Test.Playwright;

/// <summary>
/// An automatic accessibility check (axe-core, WCAG 2 A and AA) of every page the menu reaches, in the light and in the dark scheme. It
/// finds what a person cannot see on a screenshot: a text whose colour is too close to its background, a button without a name, a form
/// field without a label. axe-core is loaded from a CDN when the test runs. Only the serious and critical findings fail the test.
/// </summary>
[Collection("Blazor")]
public sealed class AccessibilityTests(BlazorApp app) : BlazorTest(app)
{
    /// <summary>CDN URL of the axe-core script used for the accessibility scan.</summary>
    private const string AxeUrl = "https://cdn.jsdelivr.net/npm/axe-core@4.10.2/axe.min.js";

    /// <summary>
    /// Browser context options used for every accessibility test.
    /// Animations are reduced so that a page is never captured mid-transition with intermediate colours.
    /// </summary>
    protected override BrowserNewContextOptions ContextOptions => new()
    {
        ViewportSize = new ViewportSize { Width = 1280, Height = 900 },
        ReducedMotion = ReducedMotion.Reduce,
    };

    /// <summary>
    /// Walks every menu entry in the given colour scheme, runs axe-core (WCAG 2 A / AA) and fails
    /// if any serious or critical violation is found.
    /// </summary>
    /// <param name="scheme">Light or dark colour scheme to emulate.</param>
    [Theory]
    [InlineData(ColorScheme.Light)]
    [InlineData(ColorScheme.Dark)]
    public async Task No_page_of_the_menu_has_a_serious_accessibility_violation(ColorScheme scheme)
    {
        await Page.EmulateMediaAsync(new PageEmulateMediaOptions { ColorScheme = scheme });
        await Page.AddScriptTagAsync(new PageAddScriptTagOptions { Url = AxeUrl });

        var names = await Page.Locator("nav[aria-label='Menu'] button.rt-nav-link .rt-nav-name").AllInnerTextsAsync();
        Assert.NotEmpty(names);

        var report = new StringBuilder();
        var signedIn = false;
        try
        {
            foreach (var name in names)
            {
                // Each page is reached from a fresh start: a page can keep the user from leaving it (a guard, a lock), so a walk from one
                // entry to the next would stop at the first one that does.
                await Page.GotoAsync(BaseUrl);
                await Menu.First.WaitForAsync();

                // Signed in: Members turns a signed-out visitor back to Features, and the page to audit would be another one.
                await OpenAsync("Features");
                // The sample keeps one visitor for the whole server: after the first entry the visitor is still signed in.
                if (!signedIn)
                {
                    await Button("Sign in").ClickAsync();
                    await Expect(Page.GetByText("Signed in: Open Members now goes there.")).ToBeVisibleAsync();
                    signedIn = true;
                }

                await OpenAsync(name);
                await ExpectCurrentAsync(name);

                // A navigation that reloads the page drops the script: it is added again when it is gone.
                if (!await Page.EvaluateAsync<bool>("typeof axe !== 'undefined'"))
                    await Page.AddScriptTagAsync(new PageAddScriptTagOptions { Url = AxeUrl });

                var json = await Page.EvaluateAsync<string>(
                    "async () => JSON.stringify((await axe.run(document, { runOnly: ['wcag2a', 'wcag2aa'] })).violations" +
                    ".filter(v => v.impact === 'serious' || v.impact === 'critical')" +
                    ".map(v => ({ id: v.id, impact: v.impact, count: v.nodes.length, first: v.nodes.slice(0, 4).map(n => n.target.join(' ') + (n.any[0] && n.any[0].data && n.any[0].data.contrastRatio ? ' ' + n.any[0].data.fgColor + ' on ' + n.any[0].data.bgColor + ' = ' + n.any[0].data.contrastRatio : '')).join(' | ') })))");

                if (json != "[]")
                    report.AppendLine(scheme + " / " + name + ": " + await Page.EvaluateAsync<string>(
                        "() => { const l = document.querySelector('.rt-layout'); const c = getComputedStyle(l); " +
                        "return 'theme=' + l.dataset.wuiTheme + ' layout-bg=' + c.backgroundColor + ' color=' + c.color + ' scheme=' + " +
                        "matchMedia('(prefers-color-scheme: dark)').matches; }"));

                foreach (var violation in JsonSerializer.Deserialize<JsonElement>(json).EnumerateArray())
                    report.AppendLine($"{scheme} / {name}: {violation.GetProperty("id").GetString()} ({violation.GetProperty("impact").GetString()}, " +
                                      $"{violation.GetProperty("count").GetInt32()}x) {violation.GetProperty("first").GetString()}");
            }
        }
        finally
        {
            // The sample keeps one visitor for the whole server: leaving it signed in would change what the next test finds.
            if (signedIn)
            {
                await Page.GotoAsync(BaseUrl);
                await Menu.First.WaitForAsync();
                await OpenAsync("Features");
                await Button("Sign out").ClickAsync();
            }
        }

        Assert.True(report.Length == 0, Environment.NewLine + report);
    }
}