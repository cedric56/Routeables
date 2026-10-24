using Microsoft.Extensions.DependencyInjection;
using Routeables.Abstraction;
using Routeables.Routing;
using System.Windows.Input;
using XAminals.Common;
using XAnimals.Common;

namespace XAnimals.ViewModels;

/// <summary>One entry of the history, as the page draws it: a card.</summary>
/// <param name="Number">Its place in the history, from 1.</param>
/// <param name="Name">The page it holds.</param>
/// <param name="Glyph">The icon of that page.</param>
/// <param name="Caption">What the entry is for the cursor: where you are, or the button that reaches it.</param>
/// <param name="Badge">A lock, on the page that refuses to be left.</param>
/// <param name="IsCurrent">The page on screen.</param>
/// <param name="IsAhead">An entry Forward can reach (left with Back).</param>
public sealed record HistoryItem(int Number, string Name, string Glyph, string Caption, string Badge, bool IsCurrent, bool IsAhead)
{
    /// <summary>Whether an arrow joins this card to the one before it.</summary>
    public bool HasPrevious => Number > 1;
}

/// <summary>
/// The History page: the history of a router, the calls that move in it, and the view it shows, rendered.
/// <para>
/// The page does not drive the router that shows it: its own view is the current view of that router, and a frame that
/// displays the current view would then display itself. It drives a <b>region</b>, a second router with its own history (see
/// <c>AddRegion</c>), and pushes real pages of the application into it: the animal lists. The frame shows the current view
/// of that region, built and bound by its router like any other.
/// </para>
/// <para>
/// <b>Push</b> adds the next page after the current one, <b>Back</b> and <b>Forward</b> move the cursor and keep every page,
/// <b>Pop</b> goes back and forgets the page it left. <b>Block leaving</b> makes <see cref="AnimalsViewModel.CanLeaveAsync"/> of the
/// current page answer <see langword="false"/>: every call is then refused until the box is cleared.
/// </para>
/// </summary>
[Routeable("history")]
public class HistoryViewModel : ViewModelBase, IRouteable, IDisposable
{
    /// <summary>The identifier of the region (registered by <see cref="Extensions.AddHistoryRegion"/>).</summary>
    public const string Region = "history";

    private const string Hint = "Each button is one call on the router of the region.";

    private readonly IRouter _region;
    private readonly IServiceProvider _provider;
    private IReadOnlyList<HistoryItem> _entries = [];
    private bool _isRefused;
    private bool _listening;
    private string _status = Hint;

    public HistoryViewModel(IServiceProvider provider)
    {
        _region = provider.GetRouter(Region);
        _provider = provider;
        BackCommand = new AsyncCommand(BackAsync);
        ForwardCommand = new AsyncCommand(ForwardAsync);
        PopCommand = new AsyncCommand(PopAsync);
        PushCommand = new AsyncCommand(PushAsync);
    }

    public ICommand BackCommand { get; }
    public ICommand ForwardCommand { get; }
    public ICommand PopCommand { get; }
    public ICommand PushCommand { get; }

    /// <summary>The history of the region, oldest first, as cards: behind the cursor, the page on screen, and the pages Forward can reach.</summary>
    public IReadOnlyList<HistoryItem> Entries
    {
        get => _entries;
        private set
        {
            _entries = value;
            OnPropertyChanged();
        }
    }

    /// <summary>Whether <see cref="IRouter.GoBackAsync"/> (and <see cref="IRouter.PopAsync"/>) has an entry to go to.</summary>
    public bool CanGoBack => _region.CanGoBack;

    /// <summary>Whether <see cref="IRouter.ForwardAsync"/> has an entry to go to: only entries left with Back can be reached.</summary>
    public bool CanGoForward => _region.CanGoForward;

    private AnimalsViewModel? Page => _region.CurrentViewModel as AnimalsViewModel;

    private string? PageName => RegionEntries.NameOf(_region.History.Current);

    /// <summary>When set, the page on screen refuses to be left: its <see cref="AnimalsViewModel.CanLeaveAsync"/> returns false.</summary>
    public bool BlockLeaving
    {
        get => Page?.BlockLeaving ?? false;
        set
        {
            if (Page is not { } page || page.BlockLeaving == value) return;
            page.BlockLeaving = value;
            IsRefused = false;
            Status = Hint;
            Refresh();
        }
    }

    /// <summary>A closed lock while the guard refuses, an open one otherwise.</summary>
    public string GuardGlyph => BlockLeaving ? "🔒" : "🔓";

    public string GuardText => BlockLeaving
        ? $"{PageName}: CanLeaveAsync returns false, it cannot be left."
        : $"{PageName}: CanLeaveAsync returns true, it can be left.";

    /// <summary>Whether the last call was refused or failed (the page then shows the message as a warning).</summary>
    public bool IsRefused
    {
        get => _isRefused;
        private set
        {
            if (_isRefused == value) return;
            _isRefused = value;
            OnPropertyChanged();
        }
    }

    /// <summary>What the last call said when it was refused.</summary>
    public string Status
    {
        get => _status;
        private set
        {
            if (_status == value) return;
            _status = value;
            OnPropertyChanged();
        }
    }

    /// <summary>Moves the cursor back one entry; <see cref="ForwardAsync"/> returns to the page left.</summary>
    public Task BackAsync() => RunAsync("Back", _region.GoBackAsync());

    /// <summary>Moves the cursor forward one entry (only after a Back).</summary>
    public Task ForwardAsync() => RunAsync("Forward", _region.ForwardAsync());

    /// <summary>Goes back one entry and forgets the one it left: it cannot be reached with Forward.</summary>
    public Task PopAsync() => RunAsync("Pop", _region.PopAsync());

    /// <summary>Adds the next page after the current one (the entries after the current one are forgotten).</summary>
    public Task PushAsync()
    {
        var menu = RegionEntries.Pages(_provider);
        var source = menu[(_region.History.CurrentIndex + 1) % menu.Count];

        // The entry names its region; the RouteableRegion of the page with that name shows the result. Nothing here says where.
        return RunAsync("Push", _provider.GoToAsync(RegionEntries.Page(source, Region)));
    }

    private async Task RunAsync(string action, Task<RouteableResult> navigation)
    {
        var from = PageName;
        var result = await navigation;
        if (result.Success)
        {
            IsRefused = false;
            Status = Hint;
            return;
        }

        IsRefused = true;
        Status = result switch
        {
            { Exception: { } error } => $"{action} failed: {error.Message}",
            _ when BlockLeaving => $"{action} refused: CanLeaveAsync of {from} returned false.",
            _ => $"{action}: there is no entry in that direction."
        };
    }

    /// <summary>Listens to the region while this page is on screen, and shows a first page if the region is empty.</summary>
    public async Task OnEnteredAsync(IRouteableArgs context, CancellationToken ct = default)
    {
        Listen();
        if (_region.History.Count == 0)
            await PushAsync();

        Refresh();
    }

    /// <summary>
    /// A page that is not on screen has nothing to update, and the pages of its region are not kept for nobody: the region
    /// is emptied (its pages are disposed) and shows a first page again the next time.
    /// </summary>
    public async Task OnLeftAsync(IRouteableArgs context, CancellationToken ct = default)
    {
        Unlisten();
        await _region.EraseHistoryAsync(ct);
    }

    public void Dispose() => Unlisten();

    private void Listen()
    {
        if (_listening) return;
        _listening = true;
        _region.History.Changed += OnHistoryChanged;
    }

    private void Unlisten()
    {
        if (!_listening) return;
        _listening = false;
        _region.History.Changed -= OnHistoryChanged;
    }

    // Raised while the router holds its gate: it only updates the bindings.
    private void OnHistoryChanged(object? sender, EventArgs e) => Refresh();

    private void Refresh()
    {
        var history = _region.History;
        var current = history.CurrentIndex;
        Entries = history.Entries
            .Select((entry, index) =>
            {
                var isCurrent = index == current;
                var caption = isCurrent ? "you are here" : index < current ? "Back" : "Forward";
                return new HistoryItem(
                    index + 1,
                    RegionEntries.NameOf(entry) ?? string.Empty,
                    RegionEntries.GlyphOf(entry),
                    caption,
                    entry.ViewModel is AnimalsViewModel { BlockLeaving: true } ? "🔒" : string.Empty,
                    isCurrent,
                    index > current);
            })
            .ToList();

        OnPropertyChanged(nameof(BlockLeaving));
        OnPropertyChanged(nameof(GuardGlyph));
        OnPropertyChanged(nameof(GuardText));
        OnPropertyChanged(nameof(CanGoBack));
        OnPropertyChanged(nameof(CanGoForward));
    }
}
