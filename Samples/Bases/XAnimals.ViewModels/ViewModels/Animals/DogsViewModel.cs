using Routeables.Abstraction;
using Routeables.Routing;
using Xaminals.Data;
using XAminals.Models;
using XAnimals.Common;

namespace XAnimals.ViewModels;

[Routeable("/dogs")]
public class DogsViewModel(IRouter router) : AnimalsViewModel(router, DogData.Dogs)
{
    protected override Task<RouteableResult> GoToDetailAsync(Animal animal, Action back)
    {
        // Route = the registration key and template of the view ("/dogs/{name}"); Arguments.Route = the address asked for.
        // The animal is not passed: the view model finds it again from {name}, as it does when opened from its address.
        var arguments = RouteableArgs.Create()
            .ForViewModel(back)
            .WithRoute($"/dogs/{Uri.EscapeDataString(animal.Name)}");

        var routeableView = new RouteableEntry<DogDetailViewModel>(string.Empty, route: "/dogs/{name}", template: "/dogs/{name}")
        {
            Arguments = arguments
        };
        return _router.GoToRouteAsync(routeableView);
    }
}