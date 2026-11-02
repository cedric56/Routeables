using Routeables.Abstraction;
using Routeables.Tests.Core;

namespace Routeables.Test.Abstraction;

public class RouteableKeysTests
{
    [Fact]
    public void Default_view_and_view_model_keys_differ()
    {
        var (viewKey, viewModelKey) = RouteableKeys.GetKeys(typeof(FakeViewModel));

        Assert.NotEqual(viewKey, viewModelKey);
        Assert.Contains(typeof(FakeViewModel).FullName!, viewKey);
    }

    [Fact]
    public void Only_a_view_key_leaves_the_view_model_key_at_its_default()
    {
        var keys = RouteableKeys.GetKeys(typeof(FakeViewModel), "view");

        Assert.Equal("view", keys.viewKey);
        Assert.NotEqual("view", keys.viewModelKey);
    }
}
