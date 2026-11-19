using Routeables.Abstraction;
using Routeables.Abstraction.Bases;
using Routeables.Abstraction.Lifecycle;
using Routeables.Abstraction.Registrations;
using Routeables.Abstraction.Routing;
using Routeables.Abstraction.States;
using Routeables.Routing;
using Routeables.Services;
using Routeables.Tests.Pages;

namespace Routeables.Tests.Services;

/// <summary>At start, the history of the last run is put back first and the address that opened the application is followed after it.</summary>
public class StartupOrderTests
{

    /// <summary>A page that takes its time to be entered: the restoration is still running when a link could be followed.</summary>
    public sealed class SoOldVm : IRouteable
    {
        public async Task OnEnteringAsync(IRouteableArgs args, CancellationToken cancellationToken = default) => await Task.Delay(300, cancellationToken);
    }

    private sealed class MemoryStore(string kept) : IRouteableHistoryStore
    {
        public string? Load() => kept;
        public void Save(string text) { }
    }

    [Fact]
    public async Task The_page_of_a_link_is_not_erased_by_the_restoration_of_the_history()
    {
        var ct = TestContext.Current.CancellationToken;
        var kept = RouteableState.Serialize([
            new RouteableSnapshot("/so-old", new Dictionary<string, string>()),
            new RouteableSnapshot("/so-old", new Dictionary<string, string>())]);
        var services = new ServiceCollection();
        services.RegisterRouting();
        services.RegisterRoute<SoHomeView, SoHomeVm>(ServiceLifetime.Transient, ServiceLifetime.Transient, "/so-home");
        services.RegisterRoute<SoOldView, SoOldVm>(ServiceLifetime.Transient, ServiceLifetime.Transient, "/so-old");
        services.RegisterRoute<SoLinkView, SoLinkVm>(ServiceLifetime.Transient, ServiceLifetime.Transient, "/so-link");
        services.AddTransient<SoHomeVm>();
        services.AddTransient<SoOldVm>();
        services.AddTransient<SoLinkVm>();
        services.AddSingleton<IRouteableHistoryStore>(new MemoryStore(kept));
        services.AddRouteableHistory();
        using var provider = services.BuildServiceProvider();
        var router = provider.GetRequiredService<IRouter>();

        var inbox = new RouteableLinkInbox(["so"]);
        inbox.Push(new Uri("so://so-link"));
        provider.StartRouteableHistory();
        inbox.Attach(provider);
        await router.GoToAsync("/so-home", cancellationToken: ct);

        var until = DateTime.UtcNow + TimeSpan.FromSeconds(15);
        while (DateTime.UtcNow < until && !(router.CurrentViewModel is SoLinkVm && router.History.Count == 3))
            await Task.Delay(50, ct);

        // The restored pages, then the page of the link on top of them.
        Assert.IsType<SoLinkVm>(router.CurrentViewModel);
        Assert.Equal(3, router.History.Count);
    }
}
