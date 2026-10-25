namespace Routeables.Abstraction;

/// <summary>
/// Represents the outcome of a routing or routing operation, indicating success, cancellation, or failure.
/// </summary>
public sealed class RouteableResult
{
    /// <summary>
    /// Gets a value indicating whether the routing operation completed successfully.
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// Gets a value indicating whether the routing operation was cancelled.
    /// </summary>
    public bool Cancelled { get; init; }

    /// <summary>
    /// Gets the error message associated with a failed routing operation, if any.
    /// </summary>
    public Exception? Exception { get; init; }

    /// <summary>
    /// Creates and returns a successful <see cref="RouteableResult"/> instance.
    /// </summary>
    /// <returns>A new successful routeable result.</returns>
    public static RouteableResult Ok() => new() { Success = true };

    /// <summary>
    /// Creates and returns a cancelled <see cref="RouteableResult"/> instance.
    /// </summary>
    /// <returns>A new cancelled routeable result.</returns>
    public static RouteableResult Cancel() => new() { Cancelled = true };

    /// <summary>
    /// Creates and returns a failed <see cref="RouteableResult"/> instance that carries the specified exception.
    /// </summary>
    /// <param name="ex">The exception that caused the failure.</param>
    /// <returns>A new failed routeable result.</returns>
    public static RouteableResult Fail(Exception ex) => new() { Exception = ex };
}