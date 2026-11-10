using System.ComponentModel;
using Routeables.Abstraction;

namespace RouteablesApp;

[Bindable(true)]
[Routeable("/about")]
public sealed class AboutViewModel
{
    public string Text { get; } = "Routeables routes view models: the page is chosen by an address, on every host.";
}
