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
}
