using Routeables.Abstraction.Registrations;
using Routeables.Abstraction.Routing;
using Routeables.Blazor;
using Routeables.Routing;
using Routeables.Tests.Pages;

namespace Routeables.Tests.Razor;

/// <summary><see cref="RouteableMainLayoutBase"/> on its own, as a derived layout sees it: the current view, and a layout no renderer holds yet.</summary>
public class MainLayoutUnattachedTests
{
    private sealed class Bare : RouteableMainLayoutBase
    {
        public Bare(IServiceProvider provider) => Provider = provider;

        public object? View => CurrentView;

        public Task InitializeAsync() => OnInitializedAsync();
    }

    private static ServiceProvider Application(bool prerender)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.RegisterRouting();
        services.RegisterRoute<FakeViewFor, FakeViewModel>(ServiceLifetime.Transient, ServiceLifetime.Transient, "/first");
        services.AddTransient<FakeViewModel>();
        if (prerender)
            services.AddSingleton(new RouteablePrerenderOptions());
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task The_current_view_of_a_derived_layout_is_the_one_of_the_router()
    {
        await using var provider = Application(prerender: false);
        var layout = new Bare(provider);
        Assert.Null(layout.View);

        await provider.GetRequiredService<IRouter>().GoToAsync("/first", cancellationToken: TestContext.Current.CancellationToken);

        Assert.Same(provider.GetRequiredService<IRouter>().CurrentView, layout.View);
        Assert.NotNull(layout.View);
    }

    [Fact]
    public async Task A_layout_that_no_renderer_holds_is_not_taken_for_a_prerender()
    {
        await using var provider = Application(prerender: true);
        var layout = new Bare(provider);

        // No renderer, so no renderer name to read: the layout is not prerendering (it would start the application, which nothing here provides).
        await layout.InitializeAsync();

        Assert.Empty(provider.GetRequiredService<IRouter>().History.Entries);
    }
}
