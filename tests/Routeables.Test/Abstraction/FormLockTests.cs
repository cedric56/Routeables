using Routeables.Abstraction.Bases;
using Routeables.Abstraction.Locking;
using Routeables.Locking;
using Routeables.Routing;
using Routeables.Tests.Core;

namespace Routeables.Tests.Abstraction;

/// <summary><see cref="IRouteableFormLock"/>: forms lock the menu while they are edited, and the last one to let go unlocks it.</summary>
public class FormLockTests
{
    private sealed class Page;

    private static (IRouteableFormLock Lock, FakeMenuViewModel Menu, ServiceProvider Provider) Build()
    {
        var services = new ServiceCollection();
        services.AddLocking();
        var menu = new FakeMenuViewModel();
        menu.Entries.Add(new RouteableMenuEntry<Page>("Page", null, "/page", "/page"));
        services.AddSingleton<IRouteableMenuViewModel>(menu);
        var provider = services.BuildServiceProvider();
        provider.GetRequiredService<IRouteableLockBroker>();
        var coordinator = new RouteableLockCoordinator(provider, provider.GetRequiredService<IRouteableLockBroker>(), menu);
        coordinator.Subscribe();
        return (provider.GetRequiredService<IRouteableFormLock>(), menu, provider);
    }

    [Fact]
    public void A_lock_locks_the_menu_until_it_is_disposed()
    {
        var (formLock, menu, provider) = Build();
        using var _ = provider;

        Assert.False(formLock.IsLocked);
        var editing = formLock.Lock();
        Assert.True(formLock.IsLocked);
        Assert.True(menu.Entries[0].IsLock);

        editing.Dispose();
        Assert.False(formLock.IsLocked);
        Assert.False(menu.Entries[0].IsLock);
    }

    [Fact]
    public void The_menu_is_unlocked_when_the_last_form_lets_go_and_a_token_counts_once()
    {
        var (formLock, menu, provider) = Build();
        using var _ = provider;

        var first = formLock.Lock();
        var second = formLock.Lock();
        Assert.True(menu.Entries[0].IsLock);

        first.Dispose();
        first.Dispose();
        Assert.True(formLock.IsLocked);
        Assert.True(menu.Entries[0].IsLock);

        second.Dispose();
        Assert.False(formLock.IsLocked);
        Assert.False(menu.Entries[0].IsLock);
    }

    [Fact]
    public void A_subscriber_that_throws_on_lock_does_not_leave_the_form_locked_for_ever()
    {
        var broker = new RouteableLockBroker();
        var formLock = new RouteableFormLock(broker);
        var fail = true;
        broker.Subscribe("LockChanged", _ => { if (fail) throw new InvalidOperationException("subscriber"); });

        Assert.ThrowsAny<Exception>(() => formLock.Lock());
        Assert.False(formLock.IsLocked);

        fail = false;
        using var token = formLock.Lock();
        Assert.True(formLock.IsLocked);
    }

    [Fact]
    public async Task A_form_that_asks_while_a_subscriber_is_busy_is_not_blocked_and_the_order_is_kept()
    {
        var broker = new RouteableLockBroker();
        var formLock = new RouteableFormLock(broker);
        var seen = new List<bool>();
        var inside = new ManualResetEventSlim(false);
        var leave = new ManualResetEventSlim(false);
        broker.Subscribe("LockChanged", message =>
        {
            var locked = ((RouteableLockMessage)message).IsLocked;
            lock (seen) seen.Add(locked);
            if (locked)
            {
                inside.Set();
                leave.Wait(TimeSpan.FromSeconds(5));
            }
        });

        // A first thread locks, and its subscriber is held in the middle of the message.
        IDisposable? first = null;
        var holder = Task.Run(() => first = formLock.Lock(), TestContext.Current.CancellationToken);
        Assert.True(inside.Wait(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));

        // Meanwhile this thread asks and locks again: neither waits for the subscriber.
        Assert.True(formLock.IsLocked);
        using var second = formLock.Lock();
        leave.Set();
        await holder;
        first!.Dispose();

        // One form is still locked: only the lock was announced, once.
        lock (seen) Assert.Equal(new[] { true }, seen);
        second.Dispose();
        lock (seen) Assert.Equal(new[] { true, false }, seen);
    }

    [Fact]
    public void A_subscriber_that_locks_again_and_then_throws_does_not_take_the_other_lock_back()
    {
        var broker = new RouteableLockBroker();
        var formLock = new RouteableFormLock(broker);
        var fail = true;
        IDisposable? nested = null;
        broker.Subscribe("LockChanged", message =>
        {
            if (fail && ((RouteableLockMessage)message).IsLocked)
            {
                fail = false;
                nested = formLock.Lock();
                throw new InvalidOperationException("subscriber");
            }
        });

        Assert.ThrowsAny<Exception>(() => formLock.Lock());

        // The failed call gave back its own hold; the form that locked meanwhile still holds the lock.
        Assert.True(formLock.IsLocked);
        nested!.Dispose();
        Assert.False(formLock.IsLocked);
        using var again = formLock.Lock();
        Assert.True(formLock.IsLocked);
    }
}
