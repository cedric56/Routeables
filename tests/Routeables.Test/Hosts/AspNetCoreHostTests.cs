using Routeables.Abstraction;
using Routeables.AspNetCore;

namespace Routeables.Tests.Hosts;

/// <summary>What the ASP.NET Core host does with a page address that a Razor view cannot take itself.</summary>
public class AspNetCoreHostTests
{
    public sealed class ThingView : IRouteableFor { public object? Context { get; set; } }

    /// <summary>The view model of a detail whose value comes by attribute (<c>{thing::}</c>), as <c>PandaDetailViewModel</c> of the samples.</summary>
    public sealed class ThingViewModel
    {
        public string? Name { get; private set; }

        [RouteableParameter]
        public string? Thing { get => Name; set => Name = value; }
    }

    /// <summary>A Razor view is built by MVC and has no property for the value: the host gives it to the view model's own [RouteableParameter].</summary>
    [Fact]
    public async Task A_value_by_attribute_reaches_the_property_of_the_view_model()
    {
        var services = new ServiceCollection();
        services.AddRouteablesAspNetCore(r => r.RegisterRoute<ThingView, ThingViewModel>(route: "/things/{thing::}"));
        using var root = services.BuildServiceProvider();
        using var scope = root.CreateScope();
        var router = scope.ServiceProvider.GetRequiredService<IRouter>();

        var result = await router.GoToAsync("/things/Red%20Panda", cancellationToken:TestContext.Current.CancellationToken);

        Assert.True(result.Success, result.Exception?.Message);
        Assert.Equal("Red Panda", Assert.IsType<ThingViewModel>(router.CurrentViewModel).Name);
    }
}
