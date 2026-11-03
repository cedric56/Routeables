using Microsoft.Extensions.DependencyInjection;
using Routeables.Abstraction;
using Routeables.Routing;
using Routeables.Test.Abstraction;

namespace Routeables.Tests.Core;

/// <summary>Tests the real <see cref="RouterHistory"/> (not a copy of it).</summary>
public class RouterHistoryTests
{
    private static RouterEntry Entry(string name) => new(name, new TestView(), RouteableArgs.Create());

    [Fact]
    public void A_new_history_is_empty()
    {
        var history = new RouterHistory();

        Assert.Null(history.Current);
        Assert.False(history.CanGoBack);
        Assert.False(history.CanGoForward);
        Assert.Null(history.MoveBack());
        Assert.Null(history.MoveForward());
    }

    [Fact]
    public void Push_makes_the_entry_current()
    {
        var history = new RouterHistory();
        history.Push(Entry("a"));
        history.Push(Entry("b"));

        Assert.Equal("b", history.Current!.ViewModel);
        Assert.True(history.CanGoBack);
        Assert.False(history.CanGoForward);
    }

    [Fact]
    public void MoveBack_and_MoveForward_return_the_new_current_entry()
    {
        var history = new RouterHistory();
        history.Push(Entry("a"));
        history.Push(Entry("b"));
        history.Push(Entry("c"));

        Assert.Equal("b", history.MoveBack()!.ViewModel);
        Assert.Equal("b", history.Current!.ViewModel);
        Assert.True(history.CanGoForward);

        Assert.Equal("a", history.MoveBack()!.ViewModel);
        Assert.False(history.CanGoBack);

        Assert.Equal("b", history.MoveForward()!.ViewModel);
        Assert.Equal("c", history.MoveForward()!.ViewModel);
        Assert.False(history.CanGoForward);
    }

    [Fact]
    public void Peek_looks_at_the_neighbours_without_moving()
    {
        var history = new RouterHistory();
        history.Push(Entry("a"));
        history.Push(Entry("b"));
        history.Push(Entry("c"));
        history.MoveBack();

        Assert.Equal("a", history.PeekBack()!.ViewModel);
        Assert.Equal("c", history.PeekForward()!.ViewModel);
        Assert.Equal("b", history.Current!.ViewModel);
    }

    [Fact]
    public void Push_after_going_back_discards_the_forward_entries()
    {
        var history = new RouterHistory();
        history.Push(Entry("a"));
        history.Push(Entry("b"));
        history.Push(Entry("c"));
        history.MoveBack();

        history.Push(Entry("d"));

        Assert.Equal("d", history.Current!.ViewModel);
        Assert.False(history.CanGoForward);
        Assert.Equal("b", history.MoveBack()!.ViewModel);
    }

    [Fact]
    public void The_history_has_no_limit()
    {
        var history = new RouterHistory();
        for (var i = 0; i < 500; i++)
            Assert.Empty(history.Push(Entry(i.ToString())));

        Assert.Equal(500, history.Count);
        Assert.Equal("0", history.Root!.ViewModel);

        var steps = 0;
        while (history.MoveBack() is not null)
            steps++;

        Assert.Equal(499, steps);
        Assert.Equal("0", history.Current!.ViewModel);
    }

    [Fact]
    public void Clear_empties_the_history()
    {
        var history = new RouterHistory();
        history.Push(Entry("a"));
        history.Push(Entry("b"));

        history.Clear();

        Assert.Null(history.Current);
        Assert.False(history.CanGoBack);
        Assert.False(history.CanGoForward);
    }

    [Fact]
    public void A_limited_history_drops_the_oldest_entry_and_returns_it()
    {
        var history = new RouterHistory(3);
        history.Push(Entry("a"));
        history.Push(Entry("b"));
        history.Push(Entry("c"));

        var removed = history.Push(Entry("d"));

        Assert.Equal(["a"], removed.Select(e => (string)e.ViewModel));
        Assert.Equal(3, history.Count);
        Assert.Equal("b", history.Root!.ViewModel);
        Assert.Equal("d", history.Current!.ViewModel);
        Assert.Equal(2, history.CurrentIndex);
    }

    [Fact]
    public void A_history_without_a_limit_reports_no_MaxSize_and_a_limit_below_one_is_refused()
    {
        Assert.Null(new RouterHistory().MaxSize);
        Assert.Equal(5, new RouterHistory(5).MaxSize);
        Assert.Throws<ArgumentOutOfRangeException>(() => new RouterHistory(0));
    }

    [Fact]
    public void LimitHistory_sets_the_limit_of_the_default_router_and_of_a_region_only()
    {
        var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
        services.LimitHistory(4);
        services.LimitHistory(2, "detail");
        services.RegisterRouting();
        services.AddRegion("detail", Microsoft.Extensions.DependencyInjection.ServiceLifetime.Singleton);
        services.AddRegion("other", Microsoft.Extensions.DependencyInjection.ServiceLifetime.Singleton);
        using var provider = services.BuildServiceProvider();

        Assert.Equal(4, provider.GetRequiredService<IRouter>().History.MaxSize);
        Assert.Equal(2, provider.GetRequiredKeyedService<IRouter>("detail").History.MaxSize);
        Assert.Null(provider.GetRequiredKeyedService<IRouter>("other").History.MaxSize);
    }
}
