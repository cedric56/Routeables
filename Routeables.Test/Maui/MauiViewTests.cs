using Routeables.Maui;
using Routeables.Routing;

namespace Routeables.Tests.Maui;

public class MauiViewTests
{
    [Fact]
    public async Task Context_maps_to_the_MAUI_binding_context()
    {
        var view = new RouteableFor();
        var viewModel = new object();

        view.Context = viewModel;

        Assert.Same(viewModel, view.BindingContext);
        Assert.Same(viewModel, view.Context);
        Assert.True(await view.CanLeaveAsync(ct: TestContext.Current.CancellationToken));
        await view.InitializeAsync(RouteableArgs.Create(), ct: TestContext.Current.CancellationToken);
        await view.OnEnteringAsync(RouteableArgs.Create(), ct: TestContext.Current.CancellationToken);
        await view.OnLeavingAsync(RouteableArgs.Create(), ct: TestContext.Current.CancellationToken);
    }
}
