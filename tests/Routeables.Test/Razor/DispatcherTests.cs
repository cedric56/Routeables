using Routeables.Razor;

namespace Routeables.Tests.Razor;

public class DispatcherTests
{
    // A synchronization context that keeps what is posted to it, as the circuit of Blazor Server does for its own thread.
    private sealed class Circuit : SynchronizationContext
    {
        public List<(SendOrPostCallback Callback, object? State)> Posted { get; } = [];

        public override void Post(SendOrPostCallback d, object? state) => Posted.Add((d, state));

        public void RunPosted()
        {
            foreach (var (callback, state) in Posted.ToList())
                callback(state);
            Posted.Clear();
        }
    }

    private static T InContext<T>(SynchronizationContext? context, Func<T> work)
    {
        var previous = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(context);
        try { return work(); }
        finally { SynchronizationContext.SetSynchronizationContext(previous); }
    }

    [Fact]
    public void Without_a_context_there_is_one_thread_and_the_work_runs_now()
    {
        var dispatcher = InContext(null, () => new RouteableDispatcher());
        var ran = false;

        dispatcher.Post(() => ran = true);

        Assert.True(dispatcher.CheckAccess());
        Assert.True(ran);
    }

    [Fact]
    public void With_a_context_the_work_is_posted_to_it_and_runs_when_the_circuit_runs_it()
    {
        var circuit = new Circuit();
        var dispatcher = InContext(circuit, () => new RouteableDispatcher());
        var ran = false;

        dispatcher.Post(() => ran = true);

        Assert.False(ran);
        Assert.Single(circuit.Posted);

        circuit.RunPosted();
        Assert.True(ran);
    }

    [Fact]
    public void The_dispatcher_is_on_the_thread_of_the_interface_only_inside_its_context()
    {
        var circuit = new Circuit();
        var dispatcher = InContext(circuit, () => new RouteableDispatcher());

        Assert.False(dispatcher.CheckAccess());                                     // outside the circuit
        Assert.True(InContext(circuit, dispatcher.CheckAccess));                    // inside it
    }

    [Fact]
    public async Task InvokeAsync_from_outside_the_circuit_ends_when_the_circuit_has_run_the_work()
    {
        var circuit = new Circuit();
        var dispatcher = InContext(circuit, () => new RouteableDispatcher());

        var task = dispatcher.InvokeAsync(() => 7);
        Assert.False(task.IsCompleted);

        circuit.RunPosted();
        Assert.Equal(7, await task);
    }
}
