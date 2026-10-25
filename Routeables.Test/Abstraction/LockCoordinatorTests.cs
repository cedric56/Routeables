using System.Collections.ObjectModel;
using Microsoft.Extensions.DependencyInjection;
using Routeables.Abstraction;
using Routeables.Routing;
using Routeables.Locking;

namespace Routeables.Tests.Abstraction;

public class LockCoordinatorTests
{
    [Fact]
    public void Subscribe_locks_only_the_bound_manager_when_message_is_not_global()
    {
        var primary = new RouteablesProbe();
        var other = new RouteablesProbe();
        var broker = new RouteableBrokerService();
        using var provider = new ServiceCollection()
            .AddSingleton<IMainViewModel>(primary)
            .AddSingleton<IMainViewModel>(other)
            .BuildServiceProvider();
        var coordinator = new LockCoordinator(provider, broker, primary);

        coordinator.Subscribe();
        broker.Publish(new RouteableLockMessage(IsLocked: true, IsAllLocked: false));

        Assert.Equal(1, primary.LockCount);
        Assert.Equal(0, primary.UnlockCount);
        Assert.Equal(0, other.LockCount);
        Assert.Equal(0, other.UnlockCount);
    }

    [Fact]
    public void Subscribe_applies_global_state_to_other_managers_and_ignores_other_messages()
    {
        var primary = new RouteablesProbe();
        var other = new RouteablesProbe();
        var broker = new RouteableBrokerService();
        using var provider = new ServiceCollection()
            .AddSingleton<IMainViewModel>(primary)
            .AddSingleton<IMainViewModel>(other)
            .BuildServiceProvider();
        using var coordinator = new LockCoordinator(provider, broker, primary);

        coordinator.Subscribe();
        broker.Publish(new OtherMessage());
        broker.Publish(new RouteableLockMessage(IsLocked: false, IsAllLocked: true));

        Assert.Equal(0, primary.LockCount);
        Assert.Equal(1, primary.UnlockCount);
        Assert.Equal(0, other.LockCount);
        Assert.Equal(1, other.UnlockCount);
    }

    [Fact]
    public void Dispose_unsubscribes_from_lock_changes()
    {
        var primary = new RouteablesProbe();
        var broker = new RouteableBrokerService();
        using var provider = new ServiceCollection()
            .AddSingleton<IMainViewModel>(primary)
            .BuildServiceProvider();
        var coordinator = new LockCoordinator(provider, broker, primary);
        coordinator.Subscribe();

        coordinator.Dispose();
        broker.Publish(new RouteableLockMessage(IsLocked: true, IsAllLocked: true));

        Assert.Equal(0, primary.LockCount);
        Assert.Equal(0, primary.UnlockCount);
    }

    [Fact]
    public void The_other_managers_are_resolved_only_when_a_message_is_addressed_to_all_of_them()
    {
        var primary = new RouteablesProbe();
        var broker = new RouteableBrokerService();
        var provider = new CountingProvider();
        var coordinator = new LockCoordinator(provider, broker, primary);

        coordinator.Subscribe();
        broker.Publish(new RouteableLockMessage(IsLocked: true, IsAllLocked: false));

        Assert.Equal(0, provider.Requests);
        Assert.Equal(1, primary.LockCount);

        broker.Publish(new RouteableLockMessage(IsLocked: true, IsAllLocked: true));

        Assert.Equal(1, provider.Requests);
    }

    [Fact]
    public void Subscribe_twice_listens_once()
    {
        var primary = new RouteablesProbe();
        var broker = new RouteableBrokerService();
        using var provider = new ServiceCollection().BuildServiceProvider();
        var coordinator = new LockCoordinator(provider, broker, primary);

        coordinator.Subscribe();
        coordinator.Subscribe();
        broker.Publish(new RouteableLockMessage(IsLocked: true, IsAllLocked: false));

        Assert.Equal(1, primary.LockCount);
    }

    [Fact]
    public void Disposing_a_coordinator_leaves_the_others_listening()
    {
        var first = new RouteablesProbe();
        var second = new RouteablesProbe();
        var broker = new RouteableBrokerService();
        using var provider = new ServiceCollection().BuildServiceProvider();
        var stopped = new LockCoordinator(provider, broker, first);
        var listening = new LockCoordinator(provider, broker, second);
        stopped.Subscribe();
        listening.Subscribe();

        stopped.Dispose();
        broker.Publish(new RouteableLockMessage(IsLocked: true, IsAllLocked: false));

        Assert.Equal(0, first.LockCount);
        Assert.Equal(1, second.LockCount);
    }

    [Fact]
    public void A_global_message_is_ignored_by_the_others_when_their_scope_is_gone()
    {
        var primary = new RouteablesProbe();
        var broker = new RouteableBrokerService();
        var provider = new ServiceCollection().AddSingleton<IMainViewModel>(primary).BuildServiceProvider();
        var coordinator = new LockCoordinator(provider, broker, primary);
        coordinator.Subscribe();
        provider.Dispose();

        broker.Publish(new RouteableLockMessage(IsLocked: true, IsAllLocked: true));

        Assert.Equal(1, primary.LockCount);
    }

    private sealed class CountingProvider : IServiceProvider
    {
        public int Requests { get; private set; }

        public object? GetService(Type serviceType)
        {
            Requests++;
            return Array.CreateInstance(typeof(IMainViewModel), 0);
        }
    }

    private sealed record OtherMessage() : RouteableMessage("LockChanged");

    private sealed class RouteablesProbe : IMainViewModel
    {
        public ObservableCollection<IRouteableEntry> Routeables { get; } = [];
        public IRouteableEntry? Routeable { get; set; }
        public object? CurrentView { get; set; }
        public EventHandler<object?>? CurrentViewChanged { get; set; }
        public EventHandler? LockChanged { get; set; }
        public int LockCount { get; private set; }
        public int UnlockCount { get; private set; }

        public string? CurrentTitle { get; }

        public void Lock()
        {
            LockCount++;
        }

        public void Unlock()
        {
            UnlockCount++;
        }
    }
}
