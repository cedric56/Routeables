using System.Windows;
using Routeables.Services;
using Routeables.Wpf;
using Application = System.Windows.Application;

namespace Routeables.Tests.Wpf;

#pragma warning disable WPF0001 // Application.ThemeMode is an evaluation API of WPF

[Collection(WpfCollection.Name)]
public class WpfThemeTests
{
    private readonly RouteableRequestThemeService _service = new();

    private static Task<ThemeMode> CurrentAsync() => WpfHarness.Run(() => Application.Current.ThemeMode);

    [Theory]
    [InlineData(RouteableRequestedTheme.Light)]
    [InlineData(RouteableRequestedTheme.Dark)]
    public async Task Light_and_dark_are_the_Fluent_light_and_dark_themes(RouteableRequestedTheme requested)
    {
        try
        {
            await WpfHarness.Run(() => _service.Request(requested));

            Assert.True((await CurrentAsync()) == (requested == RouteableRequestedTheme.Light ? ThemeMode.Light : ThemeMode.Dark));
        }
        finally { await WpfHarness.Run(() => _service.Request(RouteableRequestedTheme.Default)); }
    }

    [Fact]
    public async Task Default_follows_the_setting_of_Windows()
    {
        await WpfHarness.Run(() => _service.Request(RouteableRequestedTheme.Dark));
        await WpfHarness.Run(() => _service.Request(RouteableRequestedTheme.Default));

        Assert.True((await CurrentAsync()) == ThemeMode.System);
    }

    [Fact]
    public async Task A_request_from_another_thread_is_applied_by_the_UI_thread()
    {
        try
        {
            await Task.Run(() => _service.Request(RouteableRequestedTheme.Dark), TestContext.Current.CancellationToken);
            await WpfHarness.Flush();

            Assert.True((await CurrentAsync()) == ThemeMode.Dark);
        }
        finally { await WpfHarness.Run(() => _service.Request(RouteableRequestedTheme.Default)); }
    }
}
