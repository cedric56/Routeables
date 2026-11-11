using Routeables.Abstraction;

namespace RouteablesApp;

[Routeable("/about")]
public sealed class AboutViewModel
{
    public string Text { get; } = "Routeables routes view models: the page is chosen by an address, on every host.";
}
