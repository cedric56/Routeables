using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Threading;
using Routeables.Abstraction.Bases;
using Routeables.Avalonia;
using Routeables.Routing;
using System.Collections.ObjectModel;
using System.ComponentModel;
using Xunit;

namespace Routeables.Test.Avalonia;

/// <summary>The menu control: it lists the entries of its view model, follows its selected entry, and puts the selection back when a page refuses to be left.</summary>
[Collection("Avalonia headless")]
public sealed class HeadlessMenuTests(HeadlessApp app)
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    /// <summary>A menu view model that can refuse a selection, as one does when the page shown cannot be left.</summary>
    private sealed class MenuVm : IRouteableMenuViewModel, INotifyPropertyChanged
    {
        private IRouteableMenuEntry? _selected;

        public ObservableCollection<IRouteableMenuEntry> Entries { get; } = [];

        public bool Refuse { get; set; }

        public IRouteableMenuEntry? SelectedEntry
        {
            get => _selected;
            set
            {
                _selected = Refuse && _selected is not null ? _selected : value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedEntry)));
            }
        }

        public object? CurrentView { get; set; }

        public event EventHandler<object?>? CurrentViewChanged { add { } remove { } }

        public event EventHandler? LockChanged { add { } remove { } }

        public string? CurrentTitle => null;

        public event PropertyChangedEventHandler? PropertyChanged;
    }

    private static async Task UntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.Add(Timeout);
        while (true)
        {
            Dispatcher.UIThread.RunJobs();
            if (condition()) return;
            if (DateTime.UtcNow > deadline) throw new TimeoutException("The condition was not met in 5 seconds.");
            await Task.Delay(10);
        }
    }

    private static (Window Window, RouteableMenu Menu, MenuVm Vm, RouteableMenuEntry First, RouteableMenuEntry Second) Build()
    {
        var first = new RouteableMenuEntry("/one", "/one", "One");
        var second = new RouteableMenuEntry("/two", "/two", "Two");
        var vm = new MenuVm();
        vm.Entries.Add(first);
        vm.Entries.Add(second);
        var menu = new RouteableMenu { DataContext = vm };
        var window = new Window { Width = 300, Height = 300, Content = menu };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return (window, menu, vm, first, second);
    }

    [Fact]
    public Task The_menu_lists_the_entries_of_its_view_model() => app.RunAsync(async () =>
    {
        var (window, menu, vm, _, _) = Build();
        await UntilAsync(() => menu.ItemCount == 2);

        var third = new RouteableMenuEntry("/three", "/three", "Three");
        vm.Entries.Add(third);
        await UntilAsync(() => menu.ItemCount == 3);

        window.Close();
    });

    [Fact]
    public Task The_menu_selects_the_entry_that_the_view_model_selects() => app.RunAsync(async () =>
    {
        var (window, menu, vm, _, second) = Build();
        await UntilAsync(() => menu.ItemCount == 2);

        vm.SelectedEntry = second;
        await UntilAsync(() => ReferenceEquals(menu.SelectedItem, second));

        window.Close();
    });

    [Fact]
    public Task A_selection_the_view_model_refuses_is_taken_off_the_list() => app.RunAsync(async () =>
    {
        var (window, menu, vm, first, second) = Build();
        await UntilAsync(() => menu.ItemCount == 2);
        vm.SelectedEntry = first;
        await UntilAsync(() => ReferenceEquals(menu.SelectedItem, first));

        vm.Refuse = true;
        menu.SelectedItem = second;
        await UntilAsync(() => ReferenceEquals(menu.SelectedItem, first));
        Assert.Same(first, vm.SelectedEntry);

        window.Close();
    });

    [Fact]
    public Task A_menu_without_a_view_model_stops_following_the_old_one() => app.RunAsync(async () =>
    {
        var (window, menu, vm, first, second) = Build();
        HeadlessApp.Step = "build";
        await UntilAsync(() => menu.ItemCount == 2);
        HeadlessApp.Step = "listed";
        vm.SelectedEntry = first;
        await UntilAsync(() => ReferenceEquals(menu.SelectedItem, first));
        HeadlessApp.Step = "selected";

        menu.DataContext = null;
        HeadlessApp.Step = "nulled";
        Dispatcher.UIThread.RunJobs();
        vm.SelectedEntry = second;
        HeadlessApp.Step = "changed";
        Dispatcher.UIThread.RunJobs();
        HeadlessApp.Step = "ran2";
        await Task.Delay(50);
        HeadlessApp.Step = "delayed";
        Dispatcher.UIThread.RunJobs();

        Assert.NotSame(second, menu.SelectedItem);
        HeadlessApp.Step = "asserted";

        window.Close();
    });

    [Fact]
    public Task A_locked_entry_cannot_be_picked() => app.RunAsync(async () =>
    {
        var (window, menu, _, first, second) = Build();
        await UntilAsync(() => menu.ItemCount == 2 && menu.ContainerFromIndex(1) is not null);

        second.IsLock = true;
        await UntilAsync(() => menu.ContainerFromIndex(1) is { IsEnabled: false });
        Assert.True(menu.ContainerFromIndex(0)!.IsEnabled);

        first.IsLock = true;
        await UntilAsync(() => menu.ContainerFromIndex(0) is { IsEnabled: false });

        window.Close();
    });

    [Fact]
    public Task The_menu_has_an_automation_peer_that_follows_its_entries() => app.RunAsync(async () =>
    {
        var (window, menu, vm, _, _) = Build();
        await UntilAsync(() => menu.ItemCount == 2);

        var peer = ControlAutomationPeer.CreatePeerForElement(menu);
        Assert.IsAssignableFrom<SelectingItemsControlAutomationPeer>(peer);

        vm.Entries.Add(new RouteableMenuEntry("/three", "/three", "Three"));
        await UntilAsync(() => menu.ItemCount == 3);
        Dispatcher.UIThread.RunJobs();
        Assert.NotNull(peer.GetChildren());

        window.Close();
    });
}
