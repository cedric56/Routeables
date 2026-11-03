using System.Windows.Input;

namespace Routeables.Abstraction;

/// <summary>A named command, for a button of a dialog or a menu.</summary>
public class RouteableCommand
{
    /// <summary>The text of the command.</summary>
    public required string Name { get; set; }
    /// <summary>What the command does.</summary>
    public required ICommand Command { get; set; }
}
