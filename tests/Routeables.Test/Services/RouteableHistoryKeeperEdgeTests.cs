using Routeables.Abstraction.Bases;
using Routeables.Abstraction.Registrations;
using Routeables.Abstraction.Routing;
using Routeables.Abstraction.States;
using Routeables.Routing;
using Routeables.Services;
using Routeables.Tests.Pages;
namespace Routeables.Tests.Services;

/// <summary>The history that is kept between two runs when what was kept cannot be read, cannot be shown, or cannot be written.</summary>
public class RouteableHistoryKeeperEdgeTests
{
    private sealed class Store(string? kept = null, bool failOnLoad = false) : IRouteableHistoryStore
    {
        public string? Kept { get; set; } = kept;

        public string? Load() => failOnLoad ? throw new InvalidOperationException("the store is gone") : Kept;

        public void Save(string text) => Kept = text;
    }

    private static ServiceProvider Build(IRouteableHistoryStore store)
    {
        var services = new ServiceCollection();
        services.RegisterRouting();
        services.RegisterRoute<FakeViewFor, FakeViewModel>(ServiceLifetime.Transient, ServiceLifetime.Transient, "/edge-first");
        services.AddTransient<FakeViewModel>();
        services.RegisterRoute<DraftView, DraftVm>(route: "/edge-draft");
        services.AddSingleton(store);
        services.AddRouteableHistory(path: null);
        return services.BuildServiceProvider();
    }

    private static async Task UntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!condition())
        {
            if (DateTime.UtcNow > deadline) throw new TimeoutException("The condition was not met in 5 seconds.");
            await Task.Delay(10, TestContext.Current.CancellationToken);
        }
    }

    [Theory]
    [InlineData("plain text")]                                   // not one of ours: no prefix
    [InlineData("rh1:not base64 !")]                             // a prefix, and then nothing that is text of this format
    [InlineData("rh1:AAAA")]                                     // a valid base64 text that is too short to hold a nonce and a tag
    public void The_protector_gives_nothing_back_for_a_text_that_it_did_not_write(string text)
    {
        var protector = new AesRouteableHistoryProtector(new byte[32]);

        Assert.Null(protector.Unprotect(text));
    }

    [Fact]
    public async Task A_history_that_cannot_be_read_is_reported_and_the_application_starts_without_it()
    {
        using var provider = Build(new Store(failOnLoad: true));
        provider.StartRouteableHistory();
        var router = provider.GetRequiredService<IRouter>();

        var result = await router.GoToAsync("/edge-first", cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.Equal(1, router.History.Count);
    }

    [Fact]
    public async Task A_history_whose_pages_no_longer_exist_is_replaced_by_the_page_that_is_shown()
    {
        var store = new Store(RouteableState.Serialize(["/removed-in-this-version"]));
        using var provider = Build(store);
        provider.StartRouteableHistory();
        var router = provider.GetRequiredService<IRouter>();

        await router.GoToAsync("/edge-first", cancellationToken: TestContext.Current.CancellationToken);

        // Kept as it was, the same failure would come back at every start.
        await UntilAsync(() => RouteableState.Deserialize(store.Kept).SequenceEqual(["/edge-first"]));
    }

    [Fact]
    public void A_file_that_cannot_be_replaced_fails_the_save_and_leaves_no_temporary_file()
    {
        var folder = Path.Combine(Path.GetTempPath(), "routeables-edge-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            // A folder where the file should be: the move over it fails, after the text was written aside.
            var path = Path.Combine(folder, "history.txt");
            Directory.CreateDirectory(path);
            var store = new FileRouteableHistoryStore(path);

            Assert.ThrowsAny<Exception>(() => store.Save("text"));

            Assert.Empty(Directory.GetFiles(folder, "*.tmp"));
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void A_missing_file_has_no_history()
    {
        var store = new FileRouteableHistoryStore(Path.Combine(Path.GetTempPath(), "routeables-edge-" + Guid.NewGuid().ToString("N"), "none.txt"));

        Assert.Null(store.Load());
    }

    public sealed class DraftView : IRouteableFor { public object? Context { get; set; } }

    public sealed class DraftVm : IRouteableState
    {
        public string Draft { get; set; } = "";

        public void SaveState(IDictionary<string, string> state) => state["draft"] = Draft;

        public void LoadState(IReadOnlyDictionary<string, string> state) => Draft = state["draft"];
    }

    [Fact]
    public async Task Flush_writes_what_changed_since_the_last_route_change()
    {
        var store = new Store();
        using var provider = Build(store);
        provider.StartRouteableHistory();
        var router = provider.GetRequiredService<IRouter>();
        await router.GoToAsync("/edge-draft", cancellationToken: TestContext.Current.CancellationToken);

        // What the user typed is no route change: nothing wrote it, and the application is going to sleep.
        ((DraftVm)router.CurrentViewModel!).Draft = "typed";
        provider.GetRequiredService<RouteableHistoryKeeper>().Flush();

        var kept = Assert.Single(RouteableState.DeserializeSnapshots(store.Kept));
        Assert.Equal("typed", kept.State["draft"]);
    }

    [Fact]
    public async Task Flush_writes_nothing_when_the_history_is_what_was_already_written()
    {
        var store = new Store();
        using var provider = Build(store);
        provider.StartRouteableHistory();
        var router = provider.GetRequiredService<IRouter>();
        await router.GoToAsync("/edge-first", cancellationToken: TestContext.Current.CancellationToken);
        store.Kept = null;                              // whatever happened to the store, the keeper knows what it wrote

        provider.GetRequiredService<RouteableHistoryKeeper>().Flush();

        Assert.Null(store.Kept);
    }

    [Fact]
    public void Flush_before_the_previous_history_is_back_writes_nothing()
    {
        var waiting = RouteableState.Serialize(["/edge-first", "/edge-second"]);
        var store = new Store(waiting);
        using var provider = Build(store);
        provider.StartRouteableHistory();

        provider.GetRequiredService<RouteableHistoryKeeper>().Flush();

        Assert.Equal(waiting, store.Kept);
    }

    [Fact]
    public async Task An_erased_history_is_kept_as_empty_so_that_the_pages_do_not_come_back_at_the_next_start()
    {
        var store = new Store();
        using var provider = Build(store);
        provider.StartRouteableHistory();
        var router = provider.GetRequiredService<IRouter>();
        await router.GoToAsync("/edge-first", cancellationToken: TestContext.Current.CancellationToken);
        Assert.NotEmpty(store.Kept!);

        await router.EraseHistoryAsync(TestContext.Current.CancellationToken);

        await UntilAsync(() => store.Kept == string.Empty);
    }

    [Fact]
    public void A_file_that_another_process_holds_has_no_history_for_now()
    {
        var folder = Path.Combine(Path.GetTempPath(), "routeables-edge-" + Guid.NewGuid().ToString("N"));
        var path = Path.Combine(folder, "history.txt");
        try
        {
            var store = new FileRouteableHistoryStore(path);
            store.Save("kept");

            // Held with no sharing: reading it fails with an IOException (a lock on Windows, the lock .NET takes on Unix).
            using (new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                Assert.Null(store.Load());

            Assert.Equal("kept", store.Load());
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }
}
