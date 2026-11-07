using Routeables.Abstraction;
using Routeables.Core;
using Routeables.Locking;
using Routeables.Routing;
using System.Collections.ObjectModel;

namespace Routeables.Tests.Core;

public class MainViewModelBaseTests
{
    private sealed class PageA;
    private sealed class PageB;

    // The whole of a main view model: only the entries.
    private sealed class Menu(IRouter router, bool initialize = false) : MainViewModelBase(router, initialize)
    {
        public void ViewModelBecameCurrent(object viewModel) => OnViewModelChanged(viewModel);

        protected override ObservableCollection<IMenuEntry> GetEntries() =>
        [
            new MenuEntry<PageA>("A", route: "/a", template: "/a"),
            new MenuEntry<PageB>("B", route: "/b", template: "/b"),
        ];

        protected override void NotifyPropertyChanged(string? propertyName)
        {

        }
    }

    private static Router CreateRouter()
    {
        var services = new ServiceCollection();
        services.AddLocking();
        return new Router(new RouterHistory(), new RouterGate(), new RouterLifecycle(), services.BuildServiceProvider());
    }

    [Fact]
    public void Offers_the_entries_without_any_other_code()
    {
        var sut = new Menu(CreateRouter());

        Assert.Equal(["A", "B"], sut.Entries.Select(r => r.DisplayName));
        Assert.Equal("Application", sut.CurrentTitle);
    }

    [Fact]
    public void A_view_model_that_is_in_no_entry_leaves_the_selection_as_it_was()
    {
        var sut = new Menu(CreateRouter());
        sut.ViewModelBecameCurrent(new PageA());

        sut.ViewModelBecameCurrent(new object());

        Assert.Same(sut.Entries[0], sut.SelectedEntry);
    }

    public sealed class StayView : IRouteableFor { public object? Context { get; set; } }
    public sealed class StayVm : IRouteable
    {
        // A page with unsaved changes: it answers at once, so the menu is still inside the setter when the refusal is known.
        public Task<bool> CanLeaveAsync(CancellationToken ct = default) => Task.FromResult(false);
        public Task OnEnteringAsync(IRouteableArgs args, CancellationToken ct = default) => Task.CompletedTask;
        public Task OnLeavingAsync(IRouteableArgs args, CancellationToken ct = default) => Task.CompletedTask;
    }
    public sealed class OtherView : IRouteableFor { public object? Context { get; set; } }
    public sealed class OtherVm;

    private sealed class GuardedMenu(IRouter router) : MainViewModelBase(router, initialize: false)
    {
        public bool InSetter { get; set; }
        public int AnnouncedInSetter { get; private set; }
        public int AnnouncedAfterSetter { get; private set; }

        protected override ObservableCollection<IMenuEntry> GetEntries() =>
        [
            new MenuEntry<StayVm>("Stay", route: "/stay", template: "/stay"),
            new MenuEntry<OtherVm>("Other", route: "/other", template: "/other"),
        ];

        protected override void NotifyPropertyChanged(string? propertyName)
        {
            if (propertyName != nameof(SelectedEntry)) return;
            if (InSetter) AnnouncedInSetter++; else AnnouncedAfterSetter++;
        }
    }

    [Fact]
    public async Task The_selected_entry_is_announced_again_after_the_setter_when_the_page_refuses_to_be_left()
    {
        var ct = TestContext.Current.CancellationToken;
        var services = new ServiceCollection();
        services.RegisterRouting();
        services.RegisterRoute<StayView, StayVm>(route: "/stay");
        services.RegisterRoute<OtherView, OtherVm>(route: "/other");
        using var provider = services.BuildServiceProvider();
        var sut = new GuardedMenu(provider.GetRequiredService<IRouter>());
        var stay = sut.Entries[0];
        var router = provider.GetRequiredService<IRouter>();
        sut.SelectedEntry = stay;
        while (router.CurrentViewModel is not StayVm)
            await Task.Delay(10, ct);
        await Task.Delay(50, ct);   // the selection that opened the page has ended
        Assert.Same(stay, sut.SelectedEntry);
        var before = sut.AnnouncedAfterSetter;

        sut.InSetter = true;
        sut.SelectedEntry = sut.Entries[1];   // what a menu does when the user clicks an entry
        sut.InSetter = false;

        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (sut.AnnouncedAfterSetter == before && DateTime.UtcNow < deadline)
            await Task.Delay(10, ct);

        Assert.Same(stay, sut.SelectedEntry);                      // the page is still shown, so is its entry
        Assert.IsType<StayVm>(router.CurrentViewModel);
        Assert.True(sut.AnnouncedAfterSetter > before);        // and the control is told once the setter has returned
    }
}
