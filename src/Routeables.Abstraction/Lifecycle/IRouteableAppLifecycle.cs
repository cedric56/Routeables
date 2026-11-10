namespace Routeables.Abstraction.Lifecycle;

/// <summary>
/// Implemented by a view model (or a view) that wants to know when the application goes to sleep or comes back: it is
/// <b>not</b> about route changes (see <see cref="IRouteable"/>). Only the page that is shown, in the main router and in each region, is told.
/// </summary>
/// <remarks>
/// Sleep is the moment to write what must survive the process being killed (a draft, the position in a list): mark the properties with
/// <c>[Persist]</c> and the history is saved right after <see cref="OnSleepAsync"/>. The application leaving (window closed, process ending)
/// is a sleep too: there is no separate exit event, because a mobile system gives no notice that it will kill the process.
/// The host raises these events (MAUI, WPF, Avalonia, WinUI, Uno); other hosts call <c>RouteableAppLifecycle.SleepAsync()</c> / <c>ResumeAsync()</c> themselves.
/// </remarks>
public interface IRouteableAppLifecycle
{
    /// <summary>The application goes to the background, is suspended or is closing. Keep it short: the system gives little time.</summary>
    Task OnSleepAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    /// <summary>The application is back in the foreground.</summary>
    Task OnResumeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
}
