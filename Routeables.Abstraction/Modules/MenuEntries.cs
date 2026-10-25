namespace Routeables.Abstraction;

/// <summary>The entries a module adds to the menu, and where; see <see cref="Extensions.AddMenuEntries"/>.</summary>
/// <param name="Build">Builds the entries of one main view model.</param>
/// <param name="Index">The place of the first entry in the menu, or <see langword="null"/> for the end.</param>
internal sealed record MenuEntries(Func<IEnumerable<IRouteableEntry>> Build, int? Index = null)
{
    /// <summary>Builds the entries and puts them in the menu of <paramref name="main"/>, at <see cref="Index"/> or at the end.</summary>
    public void AddTo(IMainRouteables main) => AddTo(main, Build());

    /// <summary>Puts entries already built in the menu of <paramref name="main"/>, at <see cref="Index"/> or at the end.</summary>
    public void AddTo(IMainRouteables main, IEnumerable<IRouteableEntry> entries)
    {
        var routeables = main.Routeables;
        var at = Index is { } index ? Math.Clamp(index, 0, routeables.Count) : routeables.Count;
        foreach (var entry in entries)
            routeables.Insert(at++, entry);
    }
}
