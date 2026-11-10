using Routeables.Abstraction;

namespace RouteablesApp;

[Routeable("/hello/{name}")]
public sealed class HelloViewModel(string name)
{
    public string Greeting { get; } = $"Hello, {name}!";
}
