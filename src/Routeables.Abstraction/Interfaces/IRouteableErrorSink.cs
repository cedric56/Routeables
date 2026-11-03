using System.Diagnostics;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.DependencyInjection;

namespace Routeables.Abstraction;

/// <summary>
/// Receives the errors Routeables deliberately does not let escape: an event handler that throws, a view model whose
/// <c>Dispose</c> throws while its entry leaves the history, a coordination that ends in failure and whose result nobody may
/// read. They must never fail or mask a route change, but they must not
/// vanish either. Register one (logging, telemetry) to see them, or set <see cref="RouteableErrors.Default"/> for the errors reported
/// without a container; without a sink they go to <see cref="Trace"/>.
/// </summary>
public interface IRouteableErrorSink
{
    /// <param name="source">Where the error was caught, e.g. <c>Router.Routed</c> or <c>RouterLifecycle.Destroy</c>.</param>
    /// <param name="exception">The error that was not propagated.</param>
    void Report(string source, Exception exception);
}

/// <summary>
/// Sends a swallowed error to the <see cref="IRouteableErrorSink"/> registered in the container, then to <see cref="Default"/>,
/// then to <see cref="Trace"/>.
/// </summary>
public static class RouteableErrors
{
    private static volatile IRouteableErrorSink? _default;

    /// <summary>
    /// The sink for the errors reported where no container is at hand (the callbacks run by <c>Guard</c> from the UI, the follower of a
    /// region, a view model base...) and for those reported when the container has no <see cref="IRouteableErrorSink"/>. Set it once
    /// at startup, for instance to a sink that writes to Serilog; <see langword="null"/> (the default) reports to <see cref="Trace"/>.
    /// A sink registered in the container still wins for the errors that have a container.
    /// </summary>
    public static IRouteableErrorSink? Default
    {
        get => _default;
        set => _default = value;
    }

    /// <summary>Never throws: reporting an error must not become another one.</summary>
    public static void Report(IServiceProvider? provider, string source, Exception exception)
    {
        try
        {
            var sink = provider?.GetService<IRouteableErrorSink>() ?? _default;
            if (sink is not null)
                sink.Report(source, exception);
            else
                Trace.TraceWarning($"Routeables: {source}: {exception}");
        }
        catch
        {
            // A sink that fails is not reported again.
        }
    }

    /// <summary>
    /// <see cref="Report(IServiceProvider?, string, Exception)"/> with a source the compiler writes: <c>Owner.Member</c>, where the member is
    /// the method that caught the error. No name is typed by hand, so a renamed method renames its source.
    /// </summary>
    /// <param name="provider">Where to find the <see cref="IRouteableErrorSink"/>.</param>
    /// <param name="exception">The error that was not propagated.</param>
    /// <param name="owner">The type that caught it (<c>typeof(RouterLifecycle)</c>; use this overload for a static class).</param>
    /// <param name="detail">What the error is about (a route, a type name); added after the source in quotes.</param>
    /// <param name="member">Filled by the compiler with the name of the calling method.</param>
    public static void Report(IServiceProvider? provider, Exception exception, Type owner, string? detail = null, [CallerMemberName] string member = "")
    {
        var name = owner.Name;
        var tick = name.IndexOf('`');
        if (tick >= 0)
            name = name[..tick];

        Report(provider, detail is null ? $"{name}.{member}" : $"{name}.{member} '{detail}'", exception);
    }

    /// <summary><see cref="Report(IServiceProvider?, Exception, Type, string?, string)"/> for the type <typeparamref name="TOwner"/>.</summary>
    public static void Report<TOwner>(IServiceProvider? provider, Exception exception, string? detail = null, [CallerMemberName] string member = "")
        => Report(provider, exception, typeof(TOwner), detail, member);

    /// <summary>
    /// Runs a callback that belongs to the application (a dialog's <c>Opened</c>, a toast's <c>OnClick</c>...) from a place where an
    /// exception would end the program: an event handler of the UI. An exception is reported, as <c>Report</c> does, and
    /// not propagated. Does nothing for a <see langword="null"/> callback.
    /// </summary>
    /// <param name="provider">Where to find the <see cref="IRouteableErrorSink"/>; <see langword="null"/> reports to <see cref="Default"/>, or to <see cref="Trace"/> without one.</param>
    /// <param name="source">Where the callback was invoked, as in <c>Report</c>.</param>
    /// <param name="callback">The callback to run.</param>
    public static void Guard(IServiceProvider? provider, string source, Action? callback)
    {
        if (callback is null)
            return;

        try { callback(); }
        catch (Exception ex) { Report(provider, source, ex); }
    }

    /// <summary><see cref="Guard(IServiceProvider?, string, Action?)"/> for a callback with one argument.</summary>
    public static void Guard<T>(IServiceProvider? provider, string source, Action<T>? callback, T argument)
        => Guard(provider, source, callback is null ? null : () => callback(argument));

    /// <summary>
    /// <see cref="Guard(IServiceProvider?, string, Action?)"/> for a callback that answers a question (a dialog's <c>Closing</c>
    /// returns <see langword="true"/> to keep it open).
    /// </summary>
    /// <returns>The answer of the callback; <paramref name="fallback"/> when there is none or it threw.</returns>
    public static bool Guard(IServiceProvider? provider, string source, Func<bool>? callback, bool fallback = false)
    {
        if (callback is null)
            return fallback;

        try { return callback(); }
        catch (Exception ex)
        {
            Report(provider, source, ex);
            return fallback;
        }
    }

    /// <summary><see cref="Guard(IServiceProvider?, string, Func{bool}?, bool)"/> for a callback with one argument.</summary>
    public static bool Guard<T>(IServiceProvider? provider, string source, Func<T, bool>? callback, T argument, bool fallback = false)
        => Guard(provider, source, callback is null ? null : () => callback(argument), fallback);
}
