using Microsoft.Extensions.DependencyInjection;
using Routeables.Abstraction;
using Routeables.Razor;
using Routeables.Razor.Services;
using Routeables.Services;

namespace Routeables.Tests.Razor;

public class NotificationServiceTests
{
    private static RouteableNotificationService CreateService(int maxDisplayed = 5) =>
        new(new RouteableNotificationOptions { MaxDisplayed = maxDisplayed });

    [Fact]
    public async Task The_host_encodes_untrusted_text_and_maps_the_severity_and_position()
    {
        await using var host = await RazorHostHarness<RouteableNotificationHost>.StartAsync(services =>
        {
            services.AddSingleton(new RouteableNotificationOptions { Position = RouteableNotificationPosition.BottomLeft });
            services.AddScoped<RouteableNotificationService>();
            services.AddScoped<IRouteableNotificationService>(sp => sp.GetRequiredService<RouteableNotificationService>());
        });
        var service = host.Get<RouteableNotificationService>();

        ((IRouteableNotificationService)service).Show("<title>", "<script>alert(1)</script>", RouteableNotificationType.Error);
        var html = await host.HtmlAsync();

        Assert.Contains("&lt;title&gt;", html);
        Assert.Contains("&lt;script&gt;alert(1)&lt;/script&gt;", html);
        Assert.DoesNotContain("<script>", html);
        Assert.Contains("rt-error", html);
        Assert.Contains("rt-bottom-left", html);
    }

    [Fact]
    public async Task The_host_wires_click_handlers_instead_of_writing_plain_onclick_attributes()
    {
        await using var host = await RazorHostHarness<RouteableNotificationHost>.StartAsync(services =>
        {
            services.AddSingleton(new RouteableNotificationOptions());
            services.AddScoped<RouteableNotificationService>();
        });

        host.Get<RouteableNotificationService>().Show("Title", "Message");
        var html = await host.HtmlAsync();

        Assert.Contains("rt-toast", html);
        Assert.DoesNotContain("onclick=", html);
    }

    [Fact]
    public void Show_with_Never_keeps_the_notification_until_it_is_closed()
    {
        using var service = CreateService();

        service.Show("Title", "Message", expiration: RouteableNotificationDefaults.Never);

        var toast = Assert.Single(service.Items);
        Assert.Null(toast.Expiration);
        service.Close(toast);
        Assert.Empty(service.Items);
    }

    [Fact]
    public void Show_without_expiration_uses_the_default_one()
    {
        using var service = CreateService();

        service.Show("Title", "Message");

        var toast = Assert.Single(service.Items);
        Assert.NotNull(toast.Expiration);
    }

    [Fact]
    public async Task Show_with_expiration_closes_the_notification_and_reports_it()
    {
        using var service = CreateService();
        var closed = new TaskCompletionSource();

        service.Show("Title", "Message", expiration: TimeSpan.FromMilliseconds(50), onClose: () => closed.TrySetResult());

        Assert.Single(service.Items);
        Assert.Same(closed.Task, await Task.WhenAny(closed.Task, Task.Delay(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken)));
        Assert.Empty(service.Items);
    }

    [Fact]
    public void Click_invokes_the_callback_then_closes_the_notification()
    {
        using var service = CreateService();
        var clicks = 0;
        var closes = 0;
        service.Show("Title", "Message", onClick: () => clicks++, onClose: () => closes++);

        service.Click(service.Items[0]);

        Assert.Equal((1, 1), (clicks, closes));
        Assert.Empty(service.Items);
    }

    [Fact]
    public void The_close_callback_runs_only_once()
    {
        using var service = CreateService();
        var closes = 0;
        service.Show("Title", "Message", onClose: () => closes++);
        var toast = service.Items[0];

        service.Close(toast);
        service.Close(toast);

        Assert.Equal(1, closes);
    }

    [Fact]
    public void Beyond_the_maximum_the_oldest_notification_is_closed_and_reported()
    {
        using var service = CreateService(maxDisplayed: 2);
        var closed = new List<string>();

        service.Show("a", "m", onClose: () => closed.Add("a"));
        service.Show("b", "m", onClose: () => closed.Add("b"));
        service.Show("c", "m", onClose: () => closed.Add("c"));

        Assert.Equal(["b", "c"], service.Items.Select(i => i.Title));
        Assert.Equal(["a"], closed);
    }

    [Fact]
    public void Null_text_is_shown_as_empty()
    {
        using var service = CreateService();

        service.Show(null!, null!);

        var toast = Assert.Single(service.Items);
        Assert.Equal(string.Empty, toast.Title);
        Assert.Equal(string.Empty, toast.Message);
    }

    [Fact]
    public void Changed_is_raised_when_notifications_are_added_and_removed()
    {
        using var service = CreateService();
        var changes = 0;
        service.Changed += () => changes++;

        service.Show("Title", "Message");
        service.Close(service.Items[0]);

        Assert.Equal(2, changes);
    }
}
