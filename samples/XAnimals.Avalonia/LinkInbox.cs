using Routeables.Services;

namespace XAnimals.Avalonia;

/// <summary>
/// The inbox of the application, a field of its own because the Android activity receives the link before the container exists.
/// The activity calls <c>Push</c>, <c>Restore</c> and <c>Save</c>; <see cref="App"/> attaches the container once it is built.
/// </summary>
public static class LinkInbox
{
    public static RouteableLinkInbox Instance { get; } = new(["xanimals"]);
}
