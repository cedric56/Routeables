namespace Routeables.Abstraction;

/// <summary>
/// Implemented by a view model that wants its values to come back after the system killed the process of the application.
/// The values are kept next to the address of the page (see <c>RouteableState</c> in <c>Routeables.Services</c>).
/// </summary>
/// <remarks>
/// Keep only what is small and can be written as text: a typed search, a draft, a selected tab. The state is restored after the view
/// model is built and initialized from its address, so <see cref="LoadState"/> overrides what the initialization chose.
/// </remarks>
public interface IRouteableState
{
    /// <summary>Writes the values to keep into <paramref name="state"/>; called when the application saves its pages.</summary>
    void SaveState(IDictionary<string, string> state);

    /// <summary>Reads back the values written by <see cref="SaveState"/>; called once the page is shown again.</summary>
    void LoadState(IReadOnlyDictionary<string, string> state);
}
