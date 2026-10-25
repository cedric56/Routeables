using Routeables.Abstraction;

namespace Routeables.Test.Abstraction;

public class TestView : IRouteableFor
{
    public object? Context { get; set; }
}

public sealed class TestViewModel : IRouteable
{
    public bool CanLeave { get; set; } = true;

    public List<string> Events { get; } = [];

    public Task<bool> CanLeaveAsync(CancellationToken ct = default)
    {
        Events.Add("CanLeave");
        return Task.FromResult(CanLeave);
    }

    public Task OnEnteringAsync(IRouteableArgs context, CancellationToken ct = default)
    {
        Events.Add("To");
        return Task.CompletedTask;
    }

    public Task OnLeavingAsync(IRouteableArgs context, CancellationToken ct = default)
    {
        Events.Add("From");
        return Task.CompletedTask;
    }
}

public sealed class TestNavigableView : TestView, IRouteable
{
    public bool CanLeave { get; set; } = true;

    public List<string> Events { get; } = [];

    public Task<bool> CanLeaveAsync(CancellationToken ct = default)
    {
        Events.Add("CanLeave");
        return Task.FromResult(CanLeave);
    }

    public Task OnEnteringAsync(IRouteableArgs context, CancellationToken ct = default)
    {
        Events.Add("To");
        return Task.CompletedTask;
    }

    public Task OnLeavingAsync(IRouteableArgs context, CancellationToken ct = default)
    {
        Events.Add("From");
        return Task.CompletedTask;
    }
}
