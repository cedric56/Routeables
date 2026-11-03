using Routeables.Abstraction;

namespace RouteablesApp;

[Routeable("/hello/{name}")]
public sealed class HelloViewModel(IRouter router, string name)
{
    public string Greeting { get; } = $"Hello, {name}!";

    public Task GoToAbout() => router.GoToAsync("/about");
}
