using Routeables.Razor.Services;
using System.Text.Json;

namespace Routeables.Tests.Razor;

/// <summary>The data a browser notification carries and the answers it sends back, through the source-generated JSON context.</summary>
public class NotificationDataJsonTests
{
    [Fact]
    public void The_options_of_a_browser_notification_round_trip_with_their_data_and_actions()
    {
        var options = new RouteableBlazorNotificationOptions
        {
            Body = "body",
            Tag = "tag",
            RequireInteraction = true,
            Data = new RouteableNotificationData { Id = 42, ReplyActionTag = "reply" },
            Vibrations = [100, 50],
            Actions = [new RouteableNotificationAction { Title = "Open", Type = "button", Action = "open" }]
        };

        var json = JsonSerializer.Serialize(options, RouteableNotificationJsonContext.Default.RouteableBlazorNotificationOptions);
        var back = JsonSerializer.Deserialize(json, RouteableNotificationJsonContext.Default.RouteableBlazorNotificationOptions)!;

        Assert.Equal("body", back.Body);
        Assert.Equal("tag", back.Tag);
        Assert.True(back.RequireInteraction);
        Assert.Equal(42u, back.Data!.Id);
        Assert.Equal("reply", back.Data.ReplyActionTag);
        Assert.Equal([100, 50], back.Vibrations);
        var action = Assert.Single(back.Actions);
        Assert.Equal(("Open", "button", "open"), (action.Title, action.Type, action.Action));
    }

    [Fact]
    public void A_notification_without_options_has_no_data_vibration_or_action()
    {
        var options = new RouteableBlazorNotificationOptions();

        Assert.Null(options.Data);
        Assert.Empty(options.Vibrations);
        Assert.Empty(options.Actions);
        Assert.False(options.RequireInteraction);
    }

    [Fact]
    public void A_notification_data_needs_its_id()
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize("{\"ReplyActionTag\":\"x\"}", RouteableNotificationJsonContext.Default.RouteableNotificationData));
    }

    [Fact]
    public void The_answer_of_the_browser_is_read_with_its_lowercase_names()
    {
        const string json = "{\"action\":\"reply\",\"data\":{\"id\":7},\"Reply\":\"hello\"}";

        var answer = JsonSerializer.Deserialize(json, RouteableNotificationJsonContext.Default.RouteableReplyData)!;

        Assert.Equal("reply", answer.action);
        Assert.Equal(7u, answer.data!.id);
        Assert.Equal("hello", answer.Reply);
    }

    [Fact]
    public void An_answer_without_data_leaves_everything_null()
    {
        var answer = JsonSerializer.Deserialize("{}", RouteableNotificationJsonContext.Default.RouteableData)!;

        Assert.Null(answer.action);
        Assert.Null(answer.data);
    }
}
