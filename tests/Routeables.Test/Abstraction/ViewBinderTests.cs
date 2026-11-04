using Routeables.Abstraction;
using Routeables.Routing;

namespace Routeables.Tests.Abstraction;

public sealed class PlainView { public object? DataContext { get; set; } }
public sealed class PlainVm;

/// <summary>The binder of a host gives its view model to a view that does not implement <see cref="IRouteableFor"/>.</summary>
public class ViewBinderTests
{
    private sealed class PlainViewBinder : IRouteableViewBinder
    {
        public IRouteableFor? Adapt(object view)
            => view is PlainView plain ? new RouteableContextAccessor(() => plain.DataContext, value => plain.DataContext = value) : null;
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static ServiceProvider Build(bool withBinder)
    {
        var services = new ServiceCollection();
        services.RegisterRouting();
        services.AddRegion("side");
        if (withBinder)
            services.AddSingleton<IRouteableViewBinder, PlainViewBinder>();
        services.RegisterRoute<PlainView, PlainVm>(route: "/plain");
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task A_plain_view_gets_its_view_model_from_the_binder()
    {
        using var provider = Build(withBinder: true);
        var router = provider.GetRequiredService<IRouter>();

        var result = await router.GoToAsync("/plain", cancellationToken: Ct);

        Assert.True(result.Success);
        var view = router.CurrentView as PlainView;
        Assert.NotNull(view);
        Assert.Same(router.GetCurrentViewModel<PlainVm>(), view.DataContext);
    }

    [Fact]
    public async Task Without_a_binder_a_plain_view_is_left_alone()
    {
        using var provider = Build(withBinder: false);
        var router = provider.GetRequiredService<IRouter>();

        var result = await router.GoToAsync("/plain", cancellationToken: Ct);

        Assert.True(result.Success);
        Assert.Null(((PlainView)router.CurrentView!).DataContext);
    }
}
