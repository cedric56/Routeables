namespace Routeables.Services;

/// <summary>
/// Runs work on the thread that owns the user interface, so a view model can change what the screen shows from any thread (a
/// timer, a download, a message of a service) without knowing the platform: the dispatcher of WPF, Avalonia, Uno and MAUI, the
/// synchronization context of a Blazor circuit. Every host registers it; ask for it in the constructor of a view model.
/// </summary>
public interface IRouteableDispatcher
{
    /// <summary>
    /// Whether the calling thread is the one that owns the user interface: <see langword="true"/> means the work can be done now.
    /// </summary>
    /// <returns><see langword="true"/> on the thread of the interface.</returns>
    bool CheckAccess();

    /// <summary>
    /// Queues <paramref name="action"/> on the thread of the interface and returns at once, without waiting for it. An exception
    /// it throws is not returned: use <see cref="InvokeAsync(Action)"/> to observe it.
    /// </summary>
    /// <param name="action">The work.</param>
    void Post(Action action);

    /// <summary>
    /// Runs <paramref name="action"/> on the thread of the interface. On that thread it runs now; on another it is queued, and the
    /// task ends when it has run, carrying its exception if it threw.
    /// </summary>
    /// <param name="action">The work.</param>
    /// <returns>A task that ends when the work has run.</returns>
    Task InvokeAsync(Action action);

    /// <summary>
    /// Runs <paramref name="function"/> on the thread of the interface and returns what it returns (see <see cref="InvokeAsync(Action)"/>).
    /// </summary>
    /// <typeparam name="T">The type of the result.</typeparam>
    /// <param name="function">The work.</param>
    /// <returns>A task that ends with the result of the work.</returns>
    Task<T> InvokeAsync<T>(Func<T> function);

    /// <summary>
    /// Runs <paramref name="function"/> on the thread of the interface; the task ends when the task it returns has ended, whatever
    /// thread it ended on.
    /// </summary>
    /// <param name="function">The asynchronous work.</param>
    /// <returns>A task that ends when the work has ended.</returns>
    Task InvokeAsync(Func<Task> function);
}
