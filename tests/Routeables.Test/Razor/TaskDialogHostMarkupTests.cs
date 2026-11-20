using Routeables.Blazor.Components;
using Routeables.Blazor.Services;
using Routeables.Services;

namespace Routeables.Tests.Razor;

/// <summary>The markup of <see cref="RouteableTaskDialogHost"/>: the title, the footer and its toggle.</summary>
public class TaskDialogHostMarkupTests
{
    private static Task<RazorHostHarness<RouteableTaskDialogHost>> StartAsync() =>
        RazorHostHarness<RouteableTaskDialogHost>.StartAsync(services => services.AddScoped<RouteableTaskDialogQueue>());

    [Fact]
    public async Task Nothing_is_drawn_while_no_dialog_is_shown_and_again_once_it_is_closed()
    {
        await using var host = await StartAsync();
        var service = host.Get<RouteableTaskDialogQueue>();
        Assert.DoesNotContain("rt-dialog", await host.HtmlAsync());

        var task = service.ShowAsync(new RouteableTaskDialog { Title = "Saving" }, RouteableTaskDialogResult.OK);
        Assert.Contains("rt-dialog", await host.HtmlAsync());

        service.Dismiss();
        await task;
        await Task.Yield();

        Assert.DoesNotContain("rt-dialog", await host.HtmlAsync());
    }

    [Fact]
    public async Task A_dialog_without_a_title_has_no_heading_and_no_label()
    {
        await using var host = await StartAsync();
        var task = host.Get<RouteableTaskDialogQueue>().ShowAsync(new RouteableTaskDialog { Header = "Only a header" }, RouteableTaskDialogResult.OK);

        var html = await host.HtmlAsync();

        Assert.Contains("Only a header", html);
        Assert.DoesNotContain("rt-task-title", html);
        Assert.DoesNotContain("aria-labelledby", html);
        Assert.DoesNotContain("rt-subheader", html);
        host.Get<RouteableTaskDialogQueue>().Dismiss();
        await task;
    }

    [Fact]
    public async Task A_dialog_with_a_title_is_labelled_by_it()
    {
        await using var host = await StartAsync();
        var task = host.Get<RouteableTaskDialogQueue>().ShowAsync(new RouteableTaskDialog { Title = "Saving" }, RouteableTaskDialogResult.OK);

        var html = await host.HtmlAsync();

        Assert.Contains("aria-labelledby=\"rt-task-title\"", html);
        Assert.Contains("id=\"rt-task-title\"", html);
        host.Get<RouteableTaskDialogQueue>().Dismiss();
        await task;
    }

    [Fact]
    public async Task A_footer_that_can_be_folded_shows_its_toggle_folded_unless_the_dialog_asks_for_it_open()
    {
        await using var host = await StartAsync();
        var service = host.Get<RouteableTaskDialogQueue>();
        var task = service.ShowAsync(
            new RouteableTaskDialog { Footer = "details", FooterVisibility = RouteableTaskDialogFooterVisibility.Auto },
            RouteableTaskDialogResult.OK);

        var html = await host.HtmlAsync();

        Assert.Contains("rt-footer-toggle", html);
        Assert.Contains("More details", html);
        Assert.Contains("aria-expanded=\"false\"", html);
        service.Dismiss();
        await task;
    }

    [Fact]
    public async Task A_footer_that_the_dialog_asks_for_open_shows_the_toggle_open()
    {
        await using var host = await StartAsync();
        var service = host.Get<RouteableTaskDialogQueue>();
        var task = service.ShowAsync(
            new RouteableTaskDialog { Footer = "details", FooterVisibility = RouteableTaskDialogFooterVisibility.Auto, IsFooterExpanded = true },
            RouteableTaskDialogResult.OK);

        var html = await host.HtmlAsync();

        Assert.Contains("Less details", html);
        Assert.Contains("aria-expanded=\"true\"", html);
        service.Dismiss();
        await task;
    }

    [Theory]
    [InlineData(RouteableTaskDialogFooterVisibility.Always)]
    [InlineData(RouteableTaskDialogFooterVisibility.Never)]
    public async Task A_footer_that_is_always_or_never_shown_has_no_toggle(RouteableTaskDialogFooterVisibility visibility)
    {
        await using var host = await StartAsync();
        var service = host.Get<RouteableTaskDialogQueue>();
        var task = service.ShowAsync(new RouteableTaskDialog { Footer = "details", FooterVisibility = visibility }, RouteableTaskDialogResult.OK);

        Assert.DoesNotContain("rt-footer-toggle", await host.HtmlAsync());
        service.Dismiss();
        await task;
    }

    [Fact]
    public async Task A_dialog_that_follows_another_starts_from_its_own_footer_state()
    {
        await using var host = await StartAsync();
        var service = host.Get<RouteableTaskDialogQueue>();
        var first = service.ShowAsync(
            new RouteableTaskDialog { Footer = "one", FooterVisibility = RouteableTaskDialogFooterVisibility.Auto, IsFooterExpanded = true },
            RouteableTaskDialogResult.OK);
        var second = service.ShowAsync(
            new RouteableTaskDialog { Footer = "two", FooterVisibility = RouteableTaskDialogFooterVisibility.Auto },
            RouteableTaskDialogResult.OK);
        Assert.Contains("Less details", await host.HtmlAsync());

        service.Dismiss();
        await first;
        await Task.Yield();

        Assert.Contains("More details", await host.HtmlAsync());
        service.Dismiss();
        await second;
    }
}
