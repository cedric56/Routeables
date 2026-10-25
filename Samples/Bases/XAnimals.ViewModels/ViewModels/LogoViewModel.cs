using Routeables.Abstraction;

namespace XAnimals.ViewModels;

/// <summary>
/// The logo, shown in the content of the Regions page by the credits segments (<c>RouteCoordination</c>): a page with nothing to
/// say, whose view draws the logo of the platform.
/// </summary>
[Routeable("shell-logo")]
public class LogoViewModel
{
    public string Name => "Routeables";
}
