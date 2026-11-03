using Routeables.Abstraction;
using Routeables.Regions;
using Routeables.Routing;
using System.Windows.Input;
using Xaminals.Data;
using XAminals.Models;
using XAminals.Common;
using XAnimals.Common;

namespace XAnimals.ViewModels;

/// <summary>An animal the visitor can adopt: the button that moves it into the adoption zone.</summary>
/// <param name="Name">Its name.</param>
/// <param name="Glyph">The icon of its species.</param>
/// <param name="AdoptCommand">Adopts it: its card appears in the adoption zone.</param>
public sealed record AdoptChoice(string Name, string Glyph, ICommand AdoptCommand)
{
    /// <summary>The text of the button: the icon of the species and the name.</summary>
    public string Title => $"{Glyph} {Name}";
}

/// <summary>
/// The Adopt a pet page: a list of animals to choose from and an adoption zone. Click an animal and its card appears in the zone,
/// next to the pets already adopted; <b>Release</b> sends the last one back.
/// <para>
/// The adoption zone is a region shown by a <c>RouteableRegionItems</c>, which lays out <b>every</b> page of the region, not only the
/// current one. Adopting routes an entry that names the region (<see cref="PetViewModel"/> with the animal as argument): the view model
/// never says where the card goes on screen.
/// </para>
/// </summary>
[Routeable("adopt")]
public class AdoptViewModel : ViewModelBase, IRouteable, IDisposable
{
    /// <summary>The identifier of the region (registered by <see cref="Extensions.AddAdoptRegion"/>).</summary>
    public const string Region = "adopt";

    private const string Hint = "No pet yet: click an animal to adopt it.";

    private readonly IRouter _region;
    private readonly IServiceProvider _provider;
    private string _status = Hint;
    private bool _listening;

    public AdoptViewModel(IServiceProvider provider)
    {
        _provider = provider;
        _region = provider.GetRouter(Region);
        Choices = Candidates()
            .Select(c => new AdoptChoice(c.Animal.Name, c.Glyph, new AsyncCommand(() => AdoptAsync(c))))
            .ToList();
        ReleaseCommand = new AsyncCommand(ReleaseAsync);
        ReleaseAllCommand = new AsyncCommand(ReleaseAllAsync);
    }

    /// <summary>The animals that can be adopted: the first two of each species.</summary>
    public IReadOnlyList<AdoptChoice> Choices { get; }

    /// <summary>Sends the last pet back.</summary>
    public ICommand ReleaseCommand { get; }

    /// <summary>Sends every pet back.</summary>
    public ICommand ReleaseAllCommand { get; }

    /// <summary>Who lives in the home now, or what to do next.</summary>
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

    private static IEnumerable<PetInfo> Candidates()
        => Pair(AnimalIcons.Cat, CatData.Cats)
            .Concat(Pair(AnimalIcons.Dog, DogData.Dogs))
            .Concat(Pair(AnimalIcons.Monkey, MonkeyData.Monkeys))
            .Concat(Pair(AnimalIcons.Panda, PandaData.Pandas))
            .Concat(Pair(AnimalIcons.Bear, BearData.Bears))
            .Concat(Pair(AnimalIcons.Elephant, ElephantData.Elephants));

    private static IEnumerable<PetInfo> Pair(string icon, IList<Animal> animals)
        => animals.Take(2).Select(animal => new PetInfo(animal, AnimalIcons.GlyphOf(icon)));

    private bool IsAdopted(Animal animal)
        => _region.History.Entries.Any(entry => entry.ViewModel is PetViewModel pet && pet.Animal == animal);

    /// <summary>Adopts <paramref name="pet"/>: its card opens in the adoption zone, after the ones already there.</summary>
    public async Task AdoptAsync(PetInfo pet)
    {
        if (IsAdopted(pet.Animal))
        {
            Status = $"{pet.Animal.Name} is already yours.";
            return;
        }

        // The entry names its region; the zone with that name shows the card. Nothing here says where.
        var entry = new RouteableEntry<PetViewModel>("Pet", null, route: "pet", template: "pet")
        {
            Region = Region,
            Arguments = RouteableArgs.Create().ForViewModel(pet)
        };
        await _provider.GoToAsync(entry);
    }

    /// <summary>Sends the last pet back and forgets its card (the last one left empties the region).</summary>
    public async Task ReleaseAsync()
    {
        if (_region.History.Count > 1)
            await _region.PopAsync();
        else
            await _region.EraseHistoryAsync();
    }

    /// <summary>Sends every pet back.</summary>
    public Task ReleaseAllAsync() => _region.EraseHistoryAsync();

    /// <summary>Listens to the region while this page is on screen.</summary>
    public Task OnEnteredAsync(IRouteableArgs context, CancellationToken ct = default)
    {
        Listen();
        Refresh();
        return Task.CompletedTask;
    }

    /// <summary>A page that is not on screen has no use for its region: it is emptied and its cards disposed.</summary>
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
            .Select(entry => (entry.ViewModel as PetViewModel)?.Name)
            .OfType<string>()
            .ToList();
        Status = names.Count == 0 ? Hint : $"Your pets: {string.Join(" · ", names)}";
    }
}
