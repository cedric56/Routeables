using Microsoft.Extensions.DependencyInjection;
using Routeables.Abstraction;
using Routeables.Regions;
using Routeables.Routing;
using System.Windows.Input;
using XAminals.Common;
using XAnimals.Common;

namespace XAnimals.ViewModels;

/// <summary>One animal the board can show: the button that adds its page.</summary>
/// <param name="Name">The name of the page.</param>
/// <param name="Glyph">The icon of the page.</param>
/// <param name="AddCommand">Routes the page into the region of the board.</param>
public sealed record BoardChoice(string Name, string Glyph, ICommand AddCommand);

/// <summary>
/// The Board page: a region whose zone shows <b>all</b> its pages side by side, not only the current one (see
/// <c>RouteableRegionItems</c>). Pick animals and the pages open next to each other, <b>Remove</b> takes the last one away.
/// <para>
/// The view model never says where the pages go on screen: it routes entries that name the region, like the History page, and the
/// zone of the same name lays out the history of that region.
/// </para>
/// </summary>
[Routeable("board")]
public class BoardViewModel : ViewModelBase, IRouteable, IDisposable
{
    /// <summary>The identifier of the region (registered by <see cref="Extensions.AddBoardRegion"/>).</summary>
    public const string Region = "board";

    /// <summary>How many pages fit side by side.</summary>
    public const int MaxPages = 8;

    private const string Hint = "Pick an animal: its page opens next to the others.";

    private readonly IRouter _region;
    private readonly IServiceProvider _provider;
    private IReadOnlyList<BoardChoice>? _choices;
    private string _status = Hint;
    private bool _listening;

    public BoardViewModel(IServiceProvider provider)
    {
        _provider = provider;
        _region = provider.GetRouter(Region);
        RemoveCommand = new AsyncCommand(RemoveAsync);
        ClearCommand = new AsyncCommand(ClearAsync);
    }

    /// <summary>Takes the last page away.</summary>
    public ICommand RemoveCommand { get; }

    /// <summary>Takes every page away.</summary>
    public ICommand ClearCommand { get; }

    /// <summary>The animals the board can show, in the order of the menu.</summary>
    public IReadOnlyList<BoardChoice> Choices => _choices ??= RegionEntries.Pages(_provider)
        .Select(entry => new BoardChoice(entry.DisplayName, AnimalIcons.GlyphOf(entry.Path ?? string.Empty), new AsyncCommand(() => AddAsync(entry))))
        .ToList();

    /// <summary>What the board holds now, or what to do next.</summary>
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

    /// <summary>Routes the page of <paramref name="source"/> into the region: it opens after the ones already there.</summary>
    public async Task AddAsync(IRouteableEntry source)
    {
        if (_region.History.Count >= MaxPages)
        {
            Status = $"The board holds {MaxPages} pages: remove one first.";
            return;
        }

        // The entry names its region; the zone with that name shows the result. Nothing here says where.
        await _provider.GoToAsync(RegionEntries.Page(source, Region));
    }

    /// <summary>Takes the last page away and forgets it (the last page left is emptied with the region).</summary>
    public async Task RemoveAsync()
    {
        if (_region.History.Count > 1)
            await _region.PopAsync();
        else
            await _region.EraseHistoryAsync();
    }

    /// <summary>Takes every page away.</summary>
    public Task ClearAsync() => _region.EraseHistoryAsync();

    /// <summary>Listens to the region while this page is on screen, and opens two pages so that there is something to compare.</summary>
    public async Task OnEnteredAsync(IRouteableArgs context, CancellationToken ct = default)
    {
        Listen();
        var pages = RegionEntries.Pages(_provider);
        for (var i = 0; _region.History.Count < 2 && i < pages.Count; i++)
            await AddAsync(pages[i]);

        Refresh();
    }

    /// <summary>A page that is not on screen has no use for its region: it is emptied and its pages disposed.</summary>
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
        var names = _region.History.Entries
            .Take(_region.History.CurrentIndex + 1)
            .Select(entry => RegionEntries.NameOf(entry))
            .ToList();
        Status = names.Count == 0 ? Hint : $"{names.Count} on the board: {string.Join(" · ", names)}";
    }
}
