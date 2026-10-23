using Routeables.Abstraction;

namespace RouteablesApp.ViewModels;

/// <summary>The first page. A view model asks the router to move; it knows nothing about the UI framework.</summary>
[Routeable("/home")]
public sealed class HomeViewModel(IRouter router)
{
    public Task SayHelloAsync() => router.GoToAsync("/hello/Ada");
}
