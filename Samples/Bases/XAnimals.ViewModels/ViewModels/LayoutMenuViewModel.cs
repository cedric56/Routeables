using Routeables.Abstraction;
using System.Windows.Input;
using XAminals.Common;
using XAnimals.Common;

namespace XAnimals.ViewModels;

/// <summary>One page of the left panel; <paramref name="Select"/> shows it in the content.</summary>
public sealed record LayoutMenuItem(string Name, string Glyph, bool IsCurrent, ICommand Select);

/// <summary>
/// The left panel of the Regions page: it chooses what the content shows. It asks the <see cref="LayoutController"/>, which sends
/// the page to the content region by name: the panel does not know where the content is, nor that a header and a footer follow.
/// </summary>
[Routeable("layout-menu")]
public class LayoutMenuViewModel : ViewModelBase, IDisposable
{
    private readonly LayoutController _controller;
    private IReadOnlyList<LayoutMenuItem> _items = [];

    public LayoutMenuViewModel(LayoutController controller)
    {
        _controller = controller;
        BackCommand = new AsyncCommand(() => _controller.BackAsync());
        ForwardCommand = new AsyncCommand(() => _controller.ForwardAsync());
        _controller.Changed += OnChanged;
        Refresh();
    }

    public ICommand BackCommand { get; }
    public ICommand ForwardCommand { get; }

    /// <summary>The animal pages, the one the content shows marked.</summary>
    public IReadOnlyList<LayoutMenuItem> Items
    {
        get => _items;
        private set
        {
            _items = value;
            OnPropertyChanged();
        }
    }

    public bool CanGoBack => _controller.CanGoBack;

    public bool CanGoForward => _controller.CanGoForward;

    private void OnChanged(object? sender, EventArgs e) => Refresh();

    private void Refresh()
    {
        var current = _controller.Current;
        Items = _controller.Pages
            .Select(page => new LayoutMenuItem(
                page.DisplayName,
                AnimalIcons.GlyphOf(page.Path ?? string.Empty),
                page.DisplayName == current,
                new AsyncCommand(() => _controller.ShowPageAsync(page))))
            .ToList();
        OnPropertyChanged(nameof(CanGoBack));
        OnPropertyChanged(nameof(CanGoForward));
    }

    public void Dispose() => _controller.Changed -= OnChanged;
}
