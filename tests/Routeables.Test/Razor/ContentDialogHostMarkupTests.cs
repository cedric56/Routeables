using Routeables.Blazor.Components;
using Routeables.Blazor.Services;
using Routeables.Services;

namespace Routeables.Tests.Razor;

/// <summary>The markup of <see cref="RouteableContentDialogHost"/>: what is drawn for the title, the size, the content and the buttons.</summary>
public class ContentDialogHostMarkupTests
{
    private static Task<RazorHostHarness<RouteableContentDialogHost>> StartAsync() =>
        RazorHostHarness<RouteableContentDialogHost>.StartAsync(services => services.AddScoped<RouteableContentDialogQueue>());

    [Fact]
    public async Task Nothing_is_drawn_while_no_dialog_is_shown_and_again_once_it_is_closed()
    {
        await using var host = await StartAsync();
        var service = host.Get<RouteableContentDialogQueue>();
        Assert.DoesNotContain("rt-dialog", await host.HtmlAsync());

        var task = service.ShowAsync(new RouteableContentDialog { Title = "Edit", CloseButtonText = "Close" });
        Assert.Contains("rt-dialog", await host.HtmlAsync());

        service.Dismiss();
        await task;

        Assert.DoesNotContain("rt-dialog", await host.HtmlAsync());
    }

    [Fact]
    public async Task A_dialog_with_a_title_is_labelled_by_it_and_one_without_has_no_heading()
    {
        await using var host = await StartAsync();
        var service = host.Get<RouteableContentDialogQueue>();

        var titled = service.ShowAsync(new RouteableContentDialog { Title = "Edit", CloseButtonText = "Close" });
        var html = await host.HtmlAsync();
        Assert.Contains("aria-labelledby=\"rt-content-title\"", html);
        Assert.Contains("id=\"rt-content-title\"", html);
        service.Dismiss();
        await titled;

        var untitled = service.ShowAsync(new RouteableContentDialog { Title = "  ", CloseButtonText = "Close" });
        html = await host.HtmlAsync();
        Assert.DoesNotContain("aria-labelledby", html);
        Assert.DoesNotContain("rt-content-title", html);
        service.Dismiss();
        await untitled;
    }

    [Fact]
    public async Task A_dialog_that_wants_the_whole_window_has_the_full_size_class_and_another_does_not()
    {
        await using var host = await StartAsync();
        var service = host.Get<RouteableContentDialogQueue>();

        var full = service.ShowAsync(new RouteableContentDialog { FullSizeDesired = true, CloseButtonText = "Close" });
        Assert.Contains("rt-full", await host.HtmlAsync());
        service.Dismiss();
        await full;

        var normal = service.ShowAsync(new RouteableContentDialog { CloseButtonText = "Close" });
        Assert.DoesNotContain("rt-full", await host.HtmlAsync());
        service.Dismiss();
        await normal;
    }

    [Fact]
    public async Task A_content_that_is_text_is_written_escaped()
    {
        await using var host = await StartAsync();
        var service = host.Get<RouteableContentDialogQueue>();
        var task = service.ShowAsync(new RouteableContentDialog { Content = "Delete <all>?", CloseButtonText = "Close" });

        var html = await host.HtmlAsync();

        Assert.Contains("Delete &lt;all&gt;?", html);
        service.Dismiss();
        await task;
    }

    [Fact]
    public async Task A_button_that_is_disabled_is_drawn_disabled_and_the_primary_one_is_marked()
    {
        await using var host = await StartAsync();
        var service = host.Get<RouteableContentDialogQueue>();
        var task = service.ShowAsync(new RouteableContentDialog
        {
            PrimaryButtonText = "Save",
            IsPrimaryButtonEnabled = false,
            SecondaryButtonText = "Discard",
            IsSecondaryButtonEnabled = true,
            CloseButtonText = "Close",
        });

        var html = await host.HtmlAsync();

        var save = html[html.IndexOf("<button", StringComparison.Ordinal)..html.IndexOf(">Save<", StringComparison.Ordinal)];
        Assert.Contains("disabled", save);
        var discard = html[html.LastIndexOf("<button", html.IndexOf(">Discard<", StringComparison.Ordinal), StringComparison.Ordinal)..html.IndexOf(">Discard<", StringComparison.Ordinal)];
        Assert.DoesNotContain("disabled", discard);
        service.Dismiss();
        await task;
    }
}
