using Routeables.Abstraction;
using XAminals.Models;

namespace XAnimals.ViewModels;

/// <summary>What a pet card says: the animal and the icon of its species.</summary>
/// <param name="Animal">The animal that was adopted.</param>
/// <param name="Glyph">The icon of its species.</param>
public sealed record PetInfo(Animal Animal, string Glyph);

/// <summary>
/// The card of an adopted animal, shown in the adoption zone of the Adopt a pet page: its photo, its name, where it lives. The same
/// view and view model serve every pet; the router builds one for each entry, from the <see cref="PetInfo"/> it carries (matched to
/// the constructor by type, <c>ForViewModel(info)</c>). It does not know where it is shown.
/// </summary>
[Routeable("pet")]
public class PetViewModel(PetInfo info)
{
    public Animal Animal { get; } = info.Animal;

    public string Glyph { get; } = info.Glyph;

    public string Name => Animal.Name;

    public string Location => Animal.Location;
}
