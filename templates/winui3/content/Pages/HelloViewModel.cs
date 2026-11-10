using System.ComponentModel;
using Routeables.Abstraction;

namespace RouteablesApp;

// [Bindable] lets the XAML compiler generate the binding code: no reflection at run time (needed for trimming and Native AOT).
[Bindable(true)]
[Routeable("/hello/{name}")]
public sealed class HelloViewModel(string name)
{
    public string Greeting { get; } = $"Hello, {name}!";
}
