using System.Text.Json.Serialization;

namespace Routeables.Razor.Services;

[JsonSerializable(typeof(RouteableBlazorNotificationOptions))]
[JsonSerializable(typeof(RouteableNotificationAction))]
[JsonSerializable(typeof(RouteableNotificationData))]
[JsonSerializable(typeof(RouteableData))]
[JsonSerializable(typeof(RouteableInnerData))]
[JsonSerializable(typeof(RouteableReplyData))]
internal partial class RouteableNotificationJsonContext : JsonSerializerContext
{
}

internal class RouteableBlazorNotificationOptions
{
    public string? Body { get; set; }
    public string? Icon { get; set; }
    public string? Badge { get; set; }
    public string? Tag { get; set; }
    public bool RequireInteraction { get; set; }
    public RouteableNotificationData? Data { get; set; }
    public int[] Vibrations { get; set; } = [];
    public RouteableNotificationAction[] Actions { get; set; } = [];
}

internal class RouteableNotificationAction
{
    public string? Title { get; set; }
    public string? Type { get; set; }
    public string? Action { get; set; }

    public string? Icon { get; set; }
}

internal class RouteableNotificationData
{
    public required uint Id { get; set; }
    public string? ReplyActionTag { get; set; }
}

class RouteableData
{
    public string? action { get; set; }
    public RouteableInnerData? data { get; set; }
}

class RouteableInnerData
{
    public uint? id { get; set; }
}

class RouteableReplyData : RouteableData
{
    public string? Reply { get; set; }
}