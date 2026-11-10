namespace RouteablesApp.Pages;

// {name} of the route is the constructor parameter of the same name: /hello/Ada gives "Hello, Ada!".
public sealed class HelloViewModel(string name)
{
    public string Greeting { get; } = $"Hello, {name}!";
}
