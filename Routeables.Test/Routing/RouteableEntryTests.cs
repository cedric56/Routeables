using Routeables.Abstraction;
using Routeables.Routing;

namespace Routeables.Tests.Routing;

public class RouteableEntryTests
{
    [Fact]
    public void Locking_an_entry_tells_the_bindings_about_the_entry_and_its_inverse()
    {
        var view = new RouteableEntry("/a", "/a", "A");
        var changed = new List<string?>();
        view.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        view.IsLock = true;

        Assert.Equal([nameof(RouteableEntry.IsLock), nameof(RouteableEntry.InverseIsLock)], changed);
        Assert.True(view.IsLock);
        Assert.False(view.InverseIsLock);
    }

    [Fact]
    public void Setting_the_same_lock_state_again_changes_nothing()
    {
        var view = new RouteableEntry("/a", "/a", "A");
        var changed = new List<string?>();
        view.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        view.IsLock = false;
        view.IsLock = true;
        view.IsLock = true;

        Assert.Equal(2, changed.Count);
    }

    [Fact]
    public void A_typed_entry_knows_its_view_model_type_and_an_untyped_one_does_not()
    {
        Assert.Equal(typeof(string), new RouteableEntry<string>("S").ViewModelType);
        Assert.Null(new RouteableEntry("/a", "/a", "A").ViewModelType);
    }

    [Fact]
    public void Copy_of_a_typed_entry_is_a_typed_entry_without_the_instances()
    {
        RouteableEntry source = new RouteableEntry<string>("S", "icon.svg", route: "/s", template: "/s/{x}", description: "d")
        {
            Region = "side",
            ViewModel = "vm",
            View = "view",
        };

        var copy = source.Copy();

        Assert.IsType<RouteableEntry<string>>(copy);
        Assert.Equal(typeof(string), copy.ViewModelType);
        Assert.Equal(("S", "icon.svg", "/s", "/s/{x}", "d"), (copy.DisplayName, copy.Path, copy.Route, copy.Template, copy.Description));
        Assert.Null(copy.Region);
        Assert.Null(copy.ViewModel);
        Assert.Null(copy.View);
        Assert.NotSame(source, copy);
    }

    [Fact]
    public void Copy_of_an_untyped_entry_stays_untyped()
    {
        var copy = new RouteableEntry("/a", "/a/{x}", "A", "p") { Description = "d" }.Copy();

        Assert.Equal(typeof(RouteableEntry), copy.GetType());
        Assert.Equal(("A", "p", "/a", "/a/{x}", "d"), (copy.DisplayName, copy.Path, copy.Route, copy.Template, copy.Description));
    }
}
