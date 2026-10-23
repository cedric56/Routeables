using System.Windows.Input;

namespace Routeables.Abstraction;

public class RouteableCommand
{
    public required string Name { get; set; }
    public required ICommand Command { get; set; }
}
