using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Infrastructure;
using Microsoft.AspNetCore.Components.RenderTree;
using Microsoft.Extensions.Logging.Abstractions;
using Routeables.Abstraction;
using Routeables.Blazor;
using Routeables.Routing;
using Dispatcher = Microsoft.AspNetCore.Components.Dispatcher;

namespace Routeables.Tests.Razor;

/// <summary>The history handed from a prerendered page (or a paused circuit) to the one that takes over: <see cref="RouteableHistoryPersistence"/>.</summary>
public class RouteableHistoryPersistenceTests
{
    // The store between the two sides: what the first one persisted, the second one restores.
    private sealed class MemoryStore : IPersistentComponentStateStore
    {
        private IDictionary<string, byte[]> _state = new Dictionary<string, byte[]>();

        public Task<IDictionary<string, byte[]>> GetPersistedStateAsync() => Task.FromResult(_state);

        public Task PersistStateAsync(IReadOnlyDictionary<string, byte[]> state)
        {
            _state = new Dictionary<string, byte[]>(state);
            return Task.CompletedTask;
        }

        public bool SupportsRenderMode(IComponentRenderMode? renderMode) => true;
    }

#pragma warning disable BL0006 // Do not use RenderTree types
    private sealed class TestRenderer(IServiceProvider provider) : Renderer(provider, NullLoggerFactory.Instance)
    {
        public override Dispatcher Dispatcher { get; } = Dispatcher.CreateDefault();
        protected override void HandleException(Exception exception) => throw exception;
        protected override Task UpdateDisplayAsync(in RenderBatch renderBatch) => Task.CompletedTask;
    }
#pragma warning restore BL0006 // Do not use RenderTree types

    public sealed class PageView : IRouteableFor { public object? Context { get; set; } }
    public sealed class PageVm(int id) { public int Id { get; } = id; }

    private static ServiceProvider Application()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.RegisterRouting();
        services.RegisterRouteableArgsFactory(() => RouteableArgs.Create());
        services.RegisterRoute<PageView, PageVm>(route: "/pages/{id:int}");
        return services.BuildServiceProvider();
    }

    private static ComponentStatePersistenceManager Manager() => new(NullLogger<ComponentStatePersistenceManager>.Instance);

    [Fact]
    public async Task The_history_of_the_prerender_is_found_again_by_the_page_that_takes_over()
    {
        var ct = TestContext.Current.CancellationToken;
        var store = new MemoryStore();

        // The prerender: two pages, then the state is persisted.
        await using var first = Application();
        var firstRouter = first.GetRequiredService<IRouter>();
        await firstRouter.GoToAsync("/pages/1", cancellationToken: ct);
        await firstRouter.GoToAsync("/pages/2", cancellationToken: ct);
        var persisting = Manager();
        using var saving = new RouteableHistoryPersistence(persisting.State, firstRouter);
        saving.Register();
        await using (var renderer = new TestRenderer(first))
            await persisting.PersistStateAsync(store, renderer);

        // The circuit that takes over: an empty router, the state restored, then the history brought back.
        await using var second = Application();
        var secondRouter = second.GetRequiredService<IRouter>();
        var restoring = Manager();
        await restoring.RestoreStateAsync(store);
        using var taking = new RouteableHistoryPersistence(restoring.State, secondRouter);

        Assert.True(taking.Take());
        Assert.True(await taking.RestoreAsync());

        Assert.Equal(2, secondRouter.History.Entries.Count);
        Assert.Equal(2, Assert.IsType<PageVm>(secondRouter.CurrentViewModel).Id);
        Assert.True(secondRouter.CanGoBack);
    }

    [Fact]
    public async Task Without_saved_state_there_is_nothing_to_take()
    {
        await using var provider = Application();
        var manager = Manager();
        await manager.RestoreStateAsync(new MemoryStore());
        using var persistence = new RouteableHistoryPersistence(manager.State, provider.GetRequiredService<IRouter>());

        Assert.False(persistence.Take());
        Assert.False(await persistence.RestoreAsync());
    }

    [Fact]
    public async Task An_empty_history_saves_nothing()
    {
        var store = new MemoryStore();
        await using var provider = Application();
        var manager = Manager();
        using var persistence = new RouteableHistoryPersistence(manager.State, provider.GetRequiredService<IRouter>());
        persistence.Register();
        await using (var renderer = new TestRenderer(provider))
            await manager.PersistStateAsync(store, renderer);
        Assert.Empty(await store.GetPersistedStateAsync());
    }
}
