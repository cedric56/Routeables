using Routeables.Abstraction;
using Routeables.Abstraction.Routing;
using Routeables.Regions;
using Routeables.Routing;

namespace Routeables.Tests.Abstraction;

/// <summary>The whole of a layout a user writes: what it starts with and what the others show when the primary moves.</summary>
public sealed class TitleLayout(IServiceProvider provider) : RegionLayout(provider, "content", "header", "footer")
{
    public List<string> Titles { get; } = [];
    public int Moves { get; private set; }

    protected override Task OnStartAsync(CancellationToken cancellationToken)
        => ShowAsync(new RouteableMenuEntry<RegionCatsViewModel>("/cats", null, "/cats", "/cats"), cancellationToken);

    protected override async Task OnPrimaryChangedAsync()
    {
        Moves++;
        var title = PrimaryRouter.History.Current!.ViewModel.GetType().Name;
        Titles.Add(title);
        await FollowAsync("header", title, () => new RouteableMenuEntry<RegionDogsViewModel>("/dogs", null, "/dogs", "/dogs"));
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

    private static RouteableMenuEntry<RegionCatsViewModel> Cats() => new("/cats", null, "/cats", "/cats");

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

        await layout.ShowAsync(new RouteableMenuEntry<RegionDogsViewModel>("/dogs", null, "/dogs", "/dogs"), TestContext.Current.CancellationToken);
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

/// <summary>A layout whose follower cannot be built the first time.</summary>
public sealed class FlakyLayout(IServiceProvider provider) : RegionLayout(provider, "content", "header")
{
    public int Builds { get; private set; }
    public bool Fail { get; set; } = true;
    public List<Exception> Failures { get; } = [];

    protected override Task OnStartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    protected override async Task OnPrimaryChangedAsync()
        => await FollowAsync("header", "key", () =>
        {
            Builds++;
            return Fail ? throw new InvalidOperationException("no header") : new RouteableMenuEntry<RegionDogsViewModel>("/dogs", null, "/dogs", "/dogs");
        });
}

public class FlakyLayoutTests
{
    [Fact]
    public async Task A_follower_that_could_not_be_updated_is_tried_again_for_the_same_key()
    {
        using var provider = RegionTestServices.Build(services => services.AddRegionLayout<FlakyLayout>("content", "header"));
        var layout = provider.GetRequiredService<FlakyLayout>();
        layout.Failed += (_, ex) => layout.Failures.Add(ex);
        await layout.StartAsync(TestContext.Current.CancellationToken);

        await layout.ShowAsync(new RouteableMenuEntry<RegionCatsViewModel>("/cats", null, "/cats", "/cats"), TestContext.Current.CancellationToken);
        await layout.SettledAsync();
        Assert.True(layout.Failures.Count > 0);
        Assert.Equal(0, provider.GetRouter("header").History.Count);

        layout.Fail = false;
        await layout.ShowAsync(new RouteableMenuEntry<RegionCatsViewModel>("/cats", null, "/cats", "/cats"), TestContext.Current.CancellationToken);
        await layout.SettledAsync();

        Assert.Equal(1, provider.GetRouter("header").History.Count);
    }
}
