using Routeables.Abstraction;
using Xaminals.Data;
using XAnimals.Common;

namespace XAnimals.ViewModels;

/// <summary>
/// The dog is in the address (<c>/dogs/American Bulldog</c>): it is found again from <paramref name="name"/>, so the page
/// survives a refresh and can be opened from a link. <paramref name="back"/> is optional because a link has no list to return to.
/// </summary>
[Routeable("/dogs/{name}")]
public class DogDetailViewModel(IRouter router, string name, Action? back = null)
    : AnimalDetailViewModel(router, DogData.Dogs.FirstOrDefault(d => d.Name == name), back);
