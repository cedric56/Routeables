using Routeables.Abstraction;

namespace RouteablesApp;

[Routeable("/about")]
public sealed class AboutViewModel
{
    public string Text { get; } = "A second page: the route is on the view model.";
}
