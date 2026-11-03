using Routeables.Services;

namespace XAnimals.Maui
{
    /// <summary>
    /// The inbox of the application, a field of its own because the platform delivers the link before the container is attached.
    /// The platform code calls <c>Push</c>, <c>Restore</c> and <c>Save</c>; <see cref="App"/> attaches the container.
    /// </summary>
    public static class LinkInbox
    {
        public static RouteableLinkInbox Instance { get; } = new(["xanimals"]);
    }
}
