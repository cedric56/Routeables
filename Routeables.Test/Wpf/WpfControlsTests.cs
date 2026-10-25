using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using NSubstitute;
using Routeables.Abstraction;
using Routeables.Routing;
using Routeables.Tests.Core;
using Routeables.Wpf;
using Rect = System.Windows.Rect;
using Size = System.Windows.Size;
using Visibility = System.Windows.Visibility;
using Window = System.Windows.Window;

namespace Routeables.Tests.Wpf;

[Collection(WpfCollection.Name)]
public class WpfControlsTests
{
    private static FakeMainViewModel Menu(params string[] names)
    {
        var main = new FakeMainViewModel();
        foreach (var name in names)
            main.Routeables.Add(new RouteableEntry<FakeViewModel>(name, route: $"/{name}", template: $"/{name}"));
        return main;
    }

    // A list that is not in a window: its items are made when it is measured.
    private static ListBox Shown(FakeMainViewModel main)
    {
        var list = new RouteableListBox { DataContext = main };
        list.Measure(new Size(400, 600));
        list.Arrange(new Rect(0, 0, 400, 600));
        list.UpdateLayout();
        return list;
    }

    private static ListBoxItem Item(ListBox list, int index) => (ListBoxItem)list.ItemContainerGenerator.ContainerFromIndex(index);

    [Fact]
    public async Task The_menu_has_one_item_per_entry()
    {
        var count = await WpfHarness.Run(() => Shown(Menu("Cats", "Dogs", "Bears")).Items.Count);

        Assert.Equal(3, count);
    }

    [Fact]
    public async Task Selecting_an_item_selects_the_entry_of_the_main_view_model()
    {
        var main = Menu("Cats", "Dogs");

        await WpfHarness.Run(() => Shown(main).SelectedItem = main.Routeables[1]);

        Assert.Same(main.Routeables[1], main.Routeable);
    }

    [Fact]
    public async Task A_locked_entry_is_a_disabled_item_and_is_enabled_again_when_unlocked()
    {
        var main = Menu("Cats", "Dogs");

        // The items of a list are made once it is shown: a window that is not activated holds it.
        var (before, locked, unlocked) = await WpfHarness.Run(() =>
        {
            var list = new RouteableListBox { DataContext = main };
            var window = new Window { Content = list, Width = 400, Height = 600, ShowActivated = false };
            try
            {
                window.Show();
                list.UpdateLayout();
                var first = (Item(list, 0).IsEnabled, Item(list, 1).IsEnabled);

                ((RouteableEntry)main.Routeables[1]).IsLock = true;
                list.UpdateLayout();
                var second = (Item(list, 0).IsEnabled, Item(list, 1).IsEnabled);

                ((RouteableEntry)main.Routeables[1]).IsLock = false;
                list.UpdateLayout();
                return (first, second, Item(list, 1).IsEnabled);
            }
            finally { window.Close(); }
        });

        Assert.Equal((true, true), before);
        Assert.Equal((true, false), locked);
        Assert.True(unlocked);
    }

    [Fact]
    public void The_converters_read_what_WPF_cannot_bind()
    {
        var culture = CultureInfo.InvariantCulture;

        Assert.Equal(false, new NotConverter().Convert(true, typeof(bool), null, culture));
        Assert.Equal(true, new NotConverter().Convert(false, typeof(bool), null, culture));

        Assert.Equal(Visibility.Collapsed, new TextToVisibilityConverter().Convert(null, typeof(Visibility), null, culture));
        Assert.Equal(Visibility.Collapsed, new TextToVisibilityConverter().Convert("  ", typeof(Visibility), null, culture));
        Assert.Equal(Visibility.Visible, new TextToVisibilityConverter().Convert("text", typeof(Visibility), null, culture));

        var entry = Substitute.For<IRouteableEntry>();
        entry.Icon.Returns("glyph");
        Assert.Equal("glyph", new IconConverter().Convert(entry, typeof(object), null, culture));
        Assert.Null(new IconConverter().Convert("not an entry", typeof(object), null, culture));
    }

    [Fact]
    public async Task A_view_is_not_locked_until_every_entry_of_its_menu_is()
    {
        var main = Menu("Cats", "Dogs");
        var args = Substitute.For<IRouteableArgs>();
        var ct = TestContext.Current.CancellationToken;   // read here: the dispatcher thread has no test context

        var (before, partly, all, released) = await WpfHarness.Run(async () =>
        {
            var view = new RouteableFor { Context = main };
            await view.InitializeAsync(args, ct);
            var first = view.IsLocked;

            ((RouteableEntry)main.Routeables[0]).IsLock = true;
            main.NotifyLockChanged();
            var second = view.IsLocked;

            ((RouteableEntry)main.Routeables[1]).IsLock = true;
            main.NotifyLockChanged();
            var third = view.IsLocked;

            ((RouteableEntry)main.Routeables[0]).IsLock = false;
            main.NotifyLockChanged();
            return (first, second, third, view.IsLocked);
        });

        Assert.False(before);
        Assert.False(partly);
        Assert.True(all);
        Assert.False(released);
    }

    [Fact]
    public async Task Initializing_a_view_again_does_not_subscribe_to_the_lock_twice()
    {
        var main = Menu("Cats");
        var args = Substitute.For<IRouteableArgs>();
        var ct = TestContext.Current.CancellationToken;   // read here: the dispatcher thread has no test context

        await WpfHarness.Run(async () =>
        {
            var view = new RouteableFor { Context = main };
            await view.InitializeAsync(args, ct);
            await view.InitializeAsync(args, ct);
            Assert.Equal(1, main.LockSubscribers);

            view.Dispose();
            Assert.Equal(0, main.LockSubscribers);
        });
    }

    [Fact]
    public async Task A_view_without_a_main_view_model_is_never_locked()
    {
        var ct = TestContext.Current.CancellationToken;

        var locked = await WpfHarness.Run(async () =>
        {
            var view = new RouteableFor { Context = new object() };
            await view.InitializeAsync(Substitute.For<IRouteableArgs>(), ct);
            return view.IsLocked;
        });

        Assert.False(locked);
    }

    [Fact]
    public async Task The_typed_view_gives_its_view_model()
    {
        var model = new FakeViewModel();

        var found = await WpfHarness.Run(() => new RouteableFor<FakeViewModel> { Context = model }.ViewModel);

        Assert.Same(model, found);
    }
}
