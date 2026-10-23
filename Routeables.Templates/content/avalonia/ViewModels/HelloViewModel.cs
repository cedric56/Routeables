using Routeables.Abstraction;

namespace RouteablesApp.ViewModels;

/// <summary>A page with a parameter: <c>name</c> is read from the route, <c>router</c> comes from the container.</summary>
[Routeable("/hello/{name}")]
public sealed class HelloViewModel(IRouter router, string name)
{
    public string Greeting { get; } = $"Hello, {name}!";

    public Task BackAsync() => router.GoBackAsync();
}
