using Routeables.Services;
using System.Windows.Input;

namespace Routeables.Tests.Routing;

/// <summary>How a dialog button follows the command it is tied to, whatever the host draws it with.</summary>
public class RouteableCommandFollowerTests
{
    private sealed class Switch : ICommand
    {
        public bool Allowed { get; set; } = true;
        public event EventHandler? CanExecuteChanged;
        public bool CanExecute(object? parameter) => Allowed;
        public void Execute(object? parameter) { }
        public void Raise() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        public bool HasListener => CanExecuteChanged is not null;
    }

    [Fact]
    public void Without_a_command_only_isEnabled_counts()
    {
        var states = new List<bool>();
        RouteableCommandFollower.Follow(null, null, true, states.Add, a => a(), []);
        RouteableCommandFollower.Follow(null, null, false, states.Add, a => a(), []);
        Assert.Equal([true, false], states);
    }

    [Fact]
    public void The_button_follows_the_command_through_the_interface_thread()
    {
        var command = new Switch();
        var states = new List<bool>();
        var posted = new List<Action>();

        RouteableCommandFollower.Follow(command, null, true, states.Add, posted.Add, []);
        Assert.Equal([true], states);

        command.Allowed = false;
        command.Raise();
        Assert.Equal([true], states);          // not yet: the change waits for the interface thread
        posted.Single()();
        Assert.Equal([true, false], states);
    }

    [Fact]
    public void A_button_that_is_disabled_stays_disabled_whatever_the_command_says()
    {
        var command = new Switch();
        var states = new List<bool>();
        RouteableCommandFollower.Follow(command, null, false, states.Add, a => a(), []);
        command.Raise();
        Assert.All(states, s => Assert.False(s));
    }

    [Fact]
    public void Cleanup_stops_following_the_command()
    {
        var command = new Switch();
        var cleanup = new List<Action>();
        RouteableCommandFollower.Follow(command, null, true, _ => { }, a => a(), cleanup);
        Assert.True(command.HasListener);

        foreach (var action in cleanup) action();
        Assert.False(command.HasListener);
    }
}
