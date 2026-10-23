using Routeables.Services;

namespace Routeables.Tests.Services;

public class NotificationSessionTests
{
    [Fact]
    public void Texts_default_to_empty_and_the_delay_to_five_seconds()
    {
        var session = new RouteableNotificationSession(null, null);

        Assert.Equal((string.Empty, string.Empty, RouteableNotificationType.Information), (session.Title, session.Message, session.Type));
        Assert.Equal(TimeSpan.FromSeconds(5), session.AutoCloseAfter);
    }

    [Theory]
    [InlineData(2000, 2000)]
    [InlineData(-1, null)]      // RouteableNotificationDefaults.Never
    [InlineData(0, null)]
    public void The_delay_is_kept_unless_it_is_not_positive(int milliseconds, int? expected)
    {
        var delay = milliseconds == -1 ? RouteableNotificationDefaults.Never : TimeSpan.FromMilliseconds(milliseconds);

        var session = new RouteableNotificationSession("t", "m", expiration: delay);

        Assert.Equal(expected is null ? null : TimeSpan.FromMilliseconds(expected.Value), session.AutoCloseAfter);
    }

    [Fact]
    public void Click_runs_the_callback_and_a_throwing_one_is_contained()
    {
        var clicks = 0;
        new RouteableNotificationSession("t", "m", onClick: () => clicks++).Click();
        new RouteableNotificationSession("t", "m", onClick: () => throw new InvalidOperationException("no")).Click();
        new RouteableNotificationSession("t", "m").Click();

        Assert.Equal(1, clicks);
    }

    [Fact]
    public void The_close_callback_runs_once_whatever_the_number_of_closes()
    {
        var closes = 0;
        var session = new RouteableNotificationSession("t", "m", onClose: () => closes++);

        Assert.False(session.IsClosed);
        Assert.True(session.NotifyClosed());
        Assert.False(session.NotifyClosed());

        Assert.True(session.IsClosed);
        Assert.Equal(1, closes);
    }
}
