using Routeables.Abstraction;
using Routeables.Abstraction.Bases;
using Routeables.Abstraction.Routing;
using Routeables.Routing;
using Routeables.Services;
using Routeables.Abstraction.Registrations;
using Routeables.Tests.Pages;
namespace Routeables.Tests.Services;

public class RouteableHistoryKeeperTests
{

    private sealed class MemoryStore(string? kept = null) : IRouteableHistoryStore
    {
        public string? Kept { get; set; } = kept;
        public int Saves { get; private set; }
        public bool Fails { get; set; }

        public string? Load() => Kept;

        public void Save(string text)
        {
            if (Fails) throw new IOException("read-only");
            Kept = text;
            Saves++;
        }
    }

    private static ServiceProvider Build(IRouteableHistoryStore store, IRouteableHistoryProtector? protector = null)
    {
        var services = new ServiceCollection();
        services.RegisterRouting();
        services.RegisterRoute<KeptFirstView, KeptFirstVm>(ServiceLifetime.Transient, ServiceLifetime.Transient, "/kept-first");
        services.RegisterRoute<KeptSecondView, KeptSecondVm>(ServiceLifetime.Transient, ServiceLifetime.Transient, "/kept-second");
        services.AddTransient<KeptFirstVm>();
        services.AddTransient<KeptSecondVm>();
        services.AddSingleton(store);
        services.AddRouteableHistory(path: null, protector: protector);
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

    private static byte[] Key(byte fill) => Enumerable.Repeat(fill, 32).ToArray();

    [Fact]
    public async Task A_protected_history_is_not_kept_as_plain_text_and_comes_back_with_the_same_key()
    {
        var store = new MemoryStore();
        var protector = new AesRouteableHistoryProtector(Key(7));
        using (var provider = Build(store, protector))
        {
            provider.StartRouteableHistory();
            var router = provider.GetRequiredService<IRouter>();
            await router.GoToAsync("/kept-first", cancellationToken: TestContext.Current.CancellationToken);
            await router.GoToAsync("/kept-second", cancellationToken: TestContext.Current.CancellationToken);
        }

        Assert.NotNull(store.Kept);
        Assert.DoesNotContain("kept-second", store.Kept);
        Assert.Equal(["/kept-first", "/kept-second"], RouteableState.Deserialize(protector.Unprotect(store.Kept!)));

        using var next = Build(new MemoryStore(store.Kept), new AesRouteableHistoryProtector(Key(7)));
        next.StartRouteableHistory();
        var again = next.GetRequiredService<IRouter>();
        await again.GoToAsync("/kept-first", cancellationToken: TestContext.Current.CancellationToken);
        await UntilAsync(() => again.CurrentViewModel is KeptSecondVm);
    }

    [Fact]
    public async Task A_history_is_put_back_when_the_first_page_comes_after_the_wait_that_was_planned()
    {
        var previous = RouteableHistoryKeeper.FirstPageSlice;
        RouteableHistoryKeeper.FirstPageSlice = TimeSpan.FromMilliseconds(100);
        try
        {
            var store = new MemoryStore(RouteableState.Serialize(["/kept-first", "/kept-second"]));
            using var provider = Build(store);
            provider.StartRouteableHistory();
            var router = provider.GetRequiredService<IRouter>();
            await Task.Delay(500, TestContext.Current.CancellationToken);      // several waits end with no page

            await router.GoToAsync("/kept-first", cancellationToken: TestContext.Current.CancellationToken);

            await UntilAsync(() => router.CurrentViewModel is KeptSecondVm);
            Assert.Equal(2, router.History.Count);
        }
        finally
        {
            RouteableHistoryKeeper.FirstPageSlice = previous;
        }
    }

    [Fact]
    public async Task A_history_that_cannot_be_unprotected_is_left_out_and_replaced()
    {
        var kept = new AesRouteableHistoryProtector(Key(1)).Protect(RouteableState.Serialize(["/kept-first", "/kept-second"]));
        var store = new MemoryStore(kept);
        using var provider = Build(store, new AesRouteableHistoryProtector(Key(2)));
        provider.StartRouteableHistory();
        var router = provider.GetRequiredService<IRouter>();

        await router.GoToAsync("/kept-first", cancellationToken: TestContext.Current.CancellationToken);

        Assert.IsType<KeptFirstVm>(router.CurrentViewModel);
        await UntilAsync(() => store.Saves > 0);
        Assert.NotEqual(kept, store.Kept);
    }

    [Fact]
    public void The_protector_refuses_a_text_that_was_changed_a_wrong_key_and_a_bad_key_size()
    {
        var protector = new AesRouteableHistoryProtector(Key(3));
        var text = protector.Protect("secret");

        Assert.Equal("secret", protector.Unprotect(text));
        Assert.NotEqual(text, protector.Protect("secret"));   // a new nonce each time
        Assert.Null(protector.Unprotect(text[..^4] + "AAAA"));
        Assert.Null(protector.Unprotect("secret"));
        Assert.Null(new AesRouteableHistoryProtector(Key(4)).Unprotect(text));
        Assert.Throws<ArgumentException>(() => new AesRouteableHistoryProtector(new byte[5]));
    }

    [Fact]
    public void The_folder_of_the_history_that_is_created_is_for_its_owner_only_on_unix()
    {
        if (OperatingSystem.IsWindows())
            return;
        var folder = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        try
        {
            new FileRouteableHistoryStore(Path.Combine(folder, "h.txt")).Save("x");
            Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute, File.GetUnixFileMode(folder));
            Assert.Equal("x", File.ReadAllText(Path.Combine(folder, "h.txt")));
        }
        finally
        {
            try { Directory.Delete(folder, true); } catch (IOException) { }
        }
    }

    [Fact]
    public void A_history_that_is_saved_again_keeps_its_mode_and_leaves_no_temporary_file()
    {
        if (OperatingSystem.IsWindows())
            return;
        var folder = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var path = Path.Combine(folder, "h.txt");
        try
        {
            var store = new FileRouteableHistoryStore(path);
            store.Save("first");
            store.Save("second \u00e9");

            Assert.Equal("second \u00e9", store.Load());
            Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(path));
            Assert.Single(Directory.GetFiles(folder));
        }
        finally
        {
            try { Directory.Delete(folder, true); } catch (IOException) { }
        }
    }

    [Fact]
    public void The_file_of_the_history_is_for_its_owner_only_on_unix()
    {
        if (OperatingSystem.IsWindows())
            return;
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "h.txt");
        try
        {
            new FileRouteableHistoryStore(path).Save("x");
            Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(path));
        }
        finally
        {
            try { Directory.Delete(Path.GetDirectoryName(path)!, true); } catch (IOException) { }
        }
    }

    [Fact]
    public async Task Each_route_change_is_kept_in_the_store()
    {
        var store = new MemoryStore();
        using var provider = Build(store);
        provider.StartRouteableHistory();
        var router = provider.GetRequiredService<IRouter>();
        var ct = TestContext.Current.CancellationToken;

        await router.GoToAsync("/kept-first", cancellationToken: ct);
        await router.GoToAsync("/kept-second", cancellationToken: ct);

        Assert.Equal(["/kept-first", "/kept-second"], RouteableState.Deserialize(store.Kept));
    }

    [Fact]
    public async Task The_history_kept_by_the_previous_run_is_put_back_once_the_first_page_is_shown()
    {
        var store = new MemoryStore(RouteableState.Serialize(["/kept-first", "/kept-second"]));
        using var provider = Build(store);
        provider.StartRouteableHistory();
        var router = provider.GetRequiredService<IRouter>();

        await router.GoToAsync("/kept-first", cancellationToken: TestContext.Current.CancellationToken);

        await UntilAsync(() => router.CurrentViewModel is KeptSecondVm);
        Assert.Equal(2, router.History.Count);
    }

    [Fact]
    public async Task The_first_page_of_the_new_run_does_not_replace_the_history_that_is_waiting()
    {
        var waiting = RouteableState.Serialize(["/kept-first", "/kept-second"]);
        var store = new MemoryStore(waiting);
        using var provider = Build(store);
        provider.StartRouteableHistory();
        var router = provider.GetRequiredService<IRouter>();

        await router.GoToAsync("/kept-first", cancellationToken: TestContext.Current.CancellationToken);

        // Until the pages are back, nothing is written; once they are, the whole history is.
        await UntilAsync(() => router.CurrentViewModel is KeptSecondVm);
        await UntilAsync(() => store.Kept == waiting && store.Saves <= 1);
        Assert.Equal(["/kept-first", "/kept-second"], RouteableState.Deserialize(store.Kept));
    }

    [Fact]
    public async Task A_store_that_cannot_write_does_not_fail_a_route_change()
    {
        var store = new MemoryStore { Fails = true };
        using var provider = Build(store);
        provider.StartRouteableHistory();
        var router = provider.GetRequiredService<IRouter>();

        var result = await router.GoToAsync("/kept-first", cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.Null(store.Kept);
    }

    [Fact]
    public void Without_the_registration_starting_does_nothing()
    {
        var services = new ServiceCollection();
        services.RegisterRouting();
        using var provider = services.BuildServiceProvider();

        Assert.Same(provider, provider.StartRouteableHistory());
        Assert.Null(provider.GetService<RouteableHistoryKeeper>());
    }

    [Fact]
    public async Task Starting_twice_keeps_one_subscription()
    {
        var store = new MemoryStore();
        using var provider = Build(store);
        provider.StartRouteableHistory();
        provider.StartRouteableHistory();

        await provider.GetRequiredService<IRouter>().GoToAsync("/kept-first", cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(1, store.Saves);
    }

    [Fact]
    public void The_file_store_writes_reads_and_replaces_its_file()
    {
        var folder = Path.Combine(Path.GetTempPath(), "routeables-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new FileRouteableHistoryStore(Path.Combine(folder, "sub", "history.txt"));

            Assert.Null(store.Load());
            store.Save("one");
            store.Save("two");

            Assert.Equal("two", store.Load());
            Assert.False(File.Exists(store.Path + ".tmp"));
        }
        finally
        {
            if (Directory.Exists(folder)) Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void The_default_file_is_named_after_the_application()
        => Assert.EndsWith("routeables-history.txt", new FileRouteableHistoryStore().Path, StringComparison.Ordinal);
}
