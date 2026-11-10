using Microsoft.AspNetCore.Components;
using Routeables.Abstraction.Routing;
using Routeables.Services;
using System.Diagnostics.CodeAnalysis;

namespace Routeables.Blazor;

/// <summary>
/// What the history of the router keeps through the hand-over of the server prerender to the interactive circuit (or to WebAssembly), and
/// through the pause and resume of a circuit: the pages and what they saved (<see cref="RouteableState"/>) are handed over with
/// <see cref="PersistentComponentState"/>, so the user who reloads, or whose circuit is resumed, finds the Back button where it was.
/// Switched on by <see cref="RouteablePrerenderOptions.PersistHistory"/> (prerendering) or by <see cref="RouteableHistoryOptions"/>.
/// </summary>
[System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
public sealed class RouteableHistoryPersistence(PersistentComponentState state, IRouter router) : IDisposable
{
    /// <summary>The key of the state.</summary>
    public const string Key = "routeables-history";

    private PersistingComponentStateSubscription? _subscription;
    private string? _taken;

    /// <summary>Saves the history when the framework persists the state of the components.</summary>
    public void Register()
    {
        if (_subscription is not null)
            return;
        _subscription = state.RegisterOnPersisting(Persist);
    }

    /// <summary>Takes what was saved: once, at the start of the component. Nothing when the page was not prerendered or paused.</summary>
    public bool Take()
    {
        _taken = TryTake();
        return !string.IsNullOrEmpty(_taken);
    }

    /// <summary>Brings the pages that <see cref="Take"/> found back into the router.</summary>
    /// <returns><see langword="true"/> when the history was restored and the router shows its last page.</returns>
    public async Task<bool> RestoreAsync()
    {
        var text = _taken;
        _taken = null;
        if (string.IsNullOrEmpty(text))
            return false;

        var snapshots = RouteableState.DeserializeSnapshots(text);
        return snapshots.Count > 0 && await RouteableState.RestoreSnapshotsAsync(router, snapshots);
    }

    // The text only: a string is serialized without anything that trimming could remove.
    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Only a string is serialized.")]
    [UnconditionalSuppressMessage("AOT", "IL3050", Justification = "Only a string is serialized.")]
    private Task Persist()
    {
        var snapshots = RouteableState.CaptureSnapshots(router);
        if (snapshots.Count > 0)
            state.PersistAsJson(Key, RouteableState.Serialize(snapshots));
        return Task.CompletedTask;
    }

    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Only a string is deserialized.")]
    [UnconditionalSuppressMessage("AOT", "IL3050", Justification = "Only a string is deserialized.")]
    private string? TryTake() => state.TryTakeFromJson<string>(Key, out var text) ? text : null;

    /// <inheritdoc />
    public void Dispose()
    {
        _subscription?.Dispose();
        _subscription = null;
    }
}

/// <summary>
/// Registering it keeps the history of the router through the pause and resume of a circuit, and through a reload of a prerendered page,
/// with <see cref="PersistentComponentState"/>. With <see cref="RouteablePrerenderOptions"/> this is already on (see
/// <see cref="RouteablePrerenderOptions.PersistHistory"/>): register this one for an application that does not prerender.
/// </summary>
public sealed class RouteableHistoryOptions
{
}
