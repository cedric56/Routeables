using Routeables.Abstraction;
using System.Windows.Input;
using Routeables.Segments;
using XAminals.Common;
using XAnimals.Common;

namespace XAnimals.ViewModels;

/// <summary>
/// The Regions page: an application of its own, with four zones, Header, Footer, LeftPanel and Content, each a region (a router
/// with its own history) that a <c>RouteableRegion</c> of the same name receives. This view model only starts and stops them:
/// the left panel chooses the page of the content, the content is an animal list that navigates its own region (a list opens its
/// detail inside the content, and Back returns to it), the bars say where it is. None of them is assigned to a zone: each is
/// sent to a region by name.
/// <para>
/// The credits button shows what one navigation made of segments does: the logo in the content, the author in the footer, what
/// the application is in the header and the copyright in the left panel, each segment handed to the router of its region.
/// </para>
/// </summary>
public class RegionsViewModel : ViewModelBase, IRouteable
{
    private readonly LayoutController _controller;
    private readonly IRouteCoordinator _coordinator;

    public RegionsViewModel(LayoutController controller, IRouteCoordinator coordinator)
    {
        _controller = controller;
        _coordinator = coordinator;
        CreditsCommand = new AsyncCommand(ToggleCreditsAsync);
    }

    /// <summary>The names of the regions the view declares, in layout order.</summary>
    public IReadOnlyList<string> Regions => _controller.Regions;

    /// <summary>Shows the credits, or brings the application back.</summary>
    public ICommand CreditsCommand { get; }

    private bool _isLeftCollapsed;

    /// <summary>The left panel is reduced to its button: the content takes the place it frees. The panel keeps its page.</summary>
    public bool IsLeftCollapsed
    {
        get => _isLeftCollapsed;
        private set { if (_isLeftCollapsed != value) { _isLeftCollapsed = value; OnPropertyChanged(); } }
    }

    /// <summary>Reduces the left panel, or brings it back.</summary>
    public void ToggleLeft() => IsLeftCollapsed = !IsLeftCollapsed;

    /// <summary>What the button does now.</summary>
    public string CreditsText => _controller.IsCredits ? "↩ Back" : "ⓘ Credits";

    /// <summary>Shows the credits with one navigation of segments; when they are shown, starts the application again.</summary>
    public async Task ToggleCreditsAsync()
    {
        if (_controller.IsCredits)
            await _controller.StartAsync();
        else
            await _controller.ShowCreditsAsync(_coordinator);

        OnPropertyChanged(nameof(CreditsText));
    }

    /// <summary>Starts the four regions when the page is shown.</summary>
    public async Task OnEnteredAsync(IRouteableArgs context, CancellationToken ct = default)
    {
        await _controller.StartAsync(ct);
        OnPropertyChanged(nameof(CreditsText));
    }

    /// <summary>A page that is not on screen has no use for its regions: they are emptied and their pages disposed.</summary>
    public Task OnLeftAsync(IRouteableArgs context, CancellationToken ct = default) => _controller.ClearAsync(ct);
}
