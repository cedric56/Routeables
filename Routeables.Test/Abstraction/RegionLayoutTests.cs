using Microsoft.Extensions.DependencyInjection;
using Routeables.Abstraction;
using Routeables.Routing;
using Routeables.Regions;

namespace Routeables.Tests.Abstraction;

/// <summary>The whole of a layout a user writes: what it starts with and what the others show when the primary moves.</summary>
public sealed class TitleLayout(IServiceProvider provider) : RegionLayout(provider, "content", "header", "footer")
{
    public List<string> Titles { get; } = [];
    public int Moves { get; private set; }

    protected override Task OnStartAsync(CancellationToken cancellationToken)
        => ShowAsync(new RouteableEntry<RegionCatsViewModel>("/cats", null, "/cats", "/cats"), cancellationToken);

    protected override async Task OnPrimaryChangedAsync()
    {
        Moves++;
        var title = PrimaryRouter.History.Current!.ViewModel.GetType().Name;
        Titles.Add(title);
        await FollowAsync("header", title, () => new RouteableEntry<RegionDogsViewModel>("/dogs", null, "/dogs", "/dogs"));
    }

    public void Pause() => Following = false;
    public void Resume() => Following = true;
}

public class RegionLayoutTests
{
    private static ServiceProvider Build() => RegionTestServices.Build(services =>
    {
        services.AddRegionLayout<TitleLayout>("content", "header", "footer");
    });

    private static RouteableEntry<RegionCatsViewModel> Cats() => new("/cats", null, "/cats", "/cats");

    [Fact]
    public async Task Registering_a_layout_registers_its_regions_and_starting_it_fills_them()
    {
        using var provider = Build();
        var layout = provider.GetRequiredService<TitleLayout>();
        Assert.Equal(["content", "header", "footer"], layout.Regions);

        await layout.StartAsync(TestContext.Current.CancellationToken);
        await layout.SettledAsync();

        Assert.Equal(1, provider.GetRouter("content").History.Count);
        Assert.Equal(1, provider.GetRouter("header").History.Count);
    }

    [Fact]
    public async Task The_followers_update_when_the_primary_moves_and_only_when_what_they_show_changes()
    {
        using var provider = Build();
        var layout = provider.GetRequiredService<TitleLayout>();
        await layout.StartAsync(TestContext.Current.CancellationToken);

        await layout.ShowAsync(Cats(), TestContext.Current.CancellationToken);
        await layout.ShowAsync(Cats(), TestContext.Current.CancellationToken);

        Assert.Equal(3, layout.Moves);
        Assert.Equal(1, provider.GetRouter("header").History.Count);
    }

    [Fact]
    public async Task A_follower_is_updated_again_when_what_it_shows_changes()
    {
        using var provider = Build();
        var layout = provider.GetRequiredService<TitleLayout>();
        await layout.StartAsync(TestContext.Current.CancellationToken);
        await layout.SettledAsync();

        await layout.ShowAsync(new RouteableEntry<RegionDogsViewModel>("/dogs", null, "/dogs", "/dogs"), TestContext.Current.CancellationToken);
        await layout.SettledAsync();

        Assert.Equal([nameof(RegionCatsViewModel), nameof(RegionDogsViewModel)], layout.Titles);
        Assert.Equal(2, provider.GetRouter("header").History.Count);
    }

    [Fact]
    public async Task Back_and_forward_move_the_primary_region_and_the_followers_settle()
    {
        using var provider = Build();
        var layout = provider.GetRequiredService<TitleLayout>();
        await layout.StartAsync(TestContext.Current.CancellationToken);
        await layout.ShowAsync(Cats(), TestContext.Current.CancellationToken);
        Assert.True(layout.CanGoBack);

        await layout.BackAsync(TestContext.Current.CancellationToken);
        Assert.True(layout.CanGoForward);
        Assert.Equal(3, layout.Moves);
        await layout.ForwardAsync(TestContext.Current.CancellationToken);
        Assert.Equal(4, layout.Moves);
    }

    [Fact]
    public async Task A_paused_layout_does_not_follow_and_follows_again_when_resumed()
    {
        using var provider = Build();
        var layout = provider.GetRequiredService<TitleLayout>();
        await layout.StartAsync(TestContext.Current.CancellationToken);
        var moves = layout.Moves;

        layout.Pause();
        await layout.ShowAsync(Cats(), TestContext.Current.CancellationToken);
        Assert.Equal(moves, layout.Moves);

        layout.Resume();
        await layout.ShowAsync(Cats(), TestContext.Current.CancellationToken);
        Assert.Equal(moves + 1, layout.Moves);
    }

    [Fact]
    public async Task Starting_again_empties_every_region_and_clearing_stops_following()
    {
        using var provider = Build();
        var layout = provider.GetRequiredService<TitleLayout>();
        await layout.StartAsync(TestContext.Current.CancellationToken);
        await layout.ShowAsync(Cats(), TestContext.Current.CancellationToken);

        await layout.StartAsync(TestContext.Current.CancellationToken);
        Assert.Equal(1, provider.GetRouter("content").History.Count);

        await layout.ClearAsync(TestContext.Current.CancellationToken);
        Assert.Equal(0, provider.GetRouter("content").History.Count);
        Assert.Equal(0, provider.GetRouter("header").History.Count);

        var moves = layout.Moves;
        await provider.GetRouter("content").GoToRouteAsync(Cats(), TestContext.Current.CancellationToken);
        await layout.SettledAsync();
        Assert.Equal(moves, layout.Moves);
    }

    [Fact]
    public async Task Changed_is_raised_each_time_the_primary_moves()
    {
        using var provider = Build();
        var layout = provider.GetRequiredService<TitleLayout>();
        var changes = 0;
        layout.Changed += (_, _) => changes++;
        await layout.StartAsync(TestContext.Current.CancellationToken);
        await layout.ShowAsync(Cats(), TestContext.Current.CancellationToken);

        Assert.Equal(2, changes);
    }
}
