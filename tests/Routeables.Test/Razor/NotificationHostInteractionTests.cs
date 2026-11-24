using Bunit;
using Routeables.Blazor.Components;
using Routeables.Blazor.Services;
using Routeables.Services;

namespace Routeables.Tests.Razor;

/// <summary>What a person does to the toasts: a click on one, its close button, and where the toasts are drawn (rendered with bUnit, which can click).</summary>
public sealed class NotificationHostInteractionTests : BunitContext
{
    private static readonly TimeSpan Never = RouteableNotificationDefaults.Never;

    private RouteableNotificationService Start(RouteableNotificationPosition position = RouteableNotificationPosition.TopRight)
    {
        Services.AddSingleton(new RouteableNotificationOptions { Position = position });
        Services.AddSingleton<RouteableNotificationService>();
        return Services.GetRequiredService<RouteableNotificationService>();
    }

    [Theory]
    [InlineData(RouteableNotificationPosition.TopLeft, "rt-top-left")]
    [InlineData(RouteableNotificationPosition.TopCenter, "rt-top-center")]
    [InlineData(RouteableNotificationPosition.TopRight, "rt-top-right")]
    [InlineData(RouteableNotificationPosition.BottomLeft, "rt-bottom-left")]
    [InlineData(RouteableNotificationPosition.BottomCenter, "rt-bottom-center")]
    [InlineData(RouteableNotificationPosition.BottomRight, "rt-bottom-right")]
    [InlineData((RouteableNotificationPosition)99, "rt-top-right")]   // a position that is none of them is the default one
    public void The_toasts_are_drawn_where_the_application_asked(RouteableNotificationPosition position, string expected)
    {
        var service = Start(position);
        var cut = Render<RouteableNotificationHost>();
        Assert.Empty(cut.FindAll(".rt-toasts"));

        service.Show("Title", "Message", expiration: Never);

        cut.WaitForAssertion(() => Assert.Contains(expected, cut.Find(".rt-toasts").ClassName));
    }

    [Fact]
    public void A_click_on_a_toast_runs_its_callback_then_closes_it()
    {
        var service = Start();
        var cut = Render<RouteableNotificationHost>();
        var log = new List<string>();
        service.Show("Title", "Message", expiration: Never, onClick: () => log.Add("click"), onClose: () => log.Add("close"));
        cut.WaitForAssertion(() => cut.Find(".rt-toast"));

        cut.Find(".rt-toast").Click();

        Assert.Equal(["click", "close"], log);
        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(".rt-toast")));
    }

    [Fact]
    public void The_close_button_closes_the_toast_without_the_click_callback()
    {
        var service = Start();
        var cut = Render<RouteableNotificationHost>();
        var log = new List<string>();
        service.Show("Title", "Message", expiration: Never, onClick: () => log.Add("click"), onClose: () => log.Add("close"));
        service.Show("Other", "Stays", expiration: Never);
        cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll(".rt-toast").Count));

        cut.Find(".rt-toast-close").Click();

        Assert.Equal(["close"], log);
        cut.WaitForAssertion(() => Assert.Contains("Stays", Assert.Single(cut.FindAll(".rt-toast")).TextContent));
    }

    [Fact]
    public void An_error_is_an_alert_and_the_other_toasts_are_a_status()
    {
        var service = Start();
        var cut = Render<RouteableNotificationHost>();

        service.Show("Bad", "Failed", RouteableNotificationType.Error, Never);
        service.Show("Fine", "Saved", RouteableNotificationType.Success, Never);

        cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll(".rt-toast").Count));
        Assert.Equal("alert", cut.Find(".rt-error").GetAttribute("role"));
        Assert.Equal("status", cut.Find(".rt-success").GetAttribute("role"));
    }
}
