using System.Windows;
using System.Windows.Threading;
using Application = System.Windows.Application;
using Dispatcher = System.Windows.Threading.Dispatcher;

namespace Routeables.Tests.Wpf;

/// <summary>
/// WPF wants one application and one thread for its elements. The tests share both: a thread of its own (STA) that runs a dispatcher,
/// on which the application is created once. Everything that touches an element goes through <see cref="Run(Action)"/>.
/// </summary>
internal static class WpfHarness
{
    private static readonly Lazy<Dispatcher> Ui = new(Start, LazyThreadSafetyMode.ExecutionAndPublication);

    // The application exists from the start of the test run: a test that begins on another thread (a request made from the pool) finds it.
    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void StartApplication() => _ = Ui.Value;

    private static Dispatcher Start()
    {
        Dispatcher? dispatcher = null;
        Exception? failure = null;
        using var ready = new ManualResetEventSlim();

        var thread = new Thread(() =>
        {
            try
            {
                // An application of this process already exists (this assembly loaded twice, as a runner may do, or another component made
                // one): use its dispatcher, WPF allows one application per AppDomain.
                if (Application.Current is { } existing)
                {
                    dispatcher = existing.Dispatcher;
                    return;
                }

                // The application is not shut down when its last window closes: the next test needs it.
                _ = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
                dispatcher = Dispatcher.CurrentDispatcher;
            }
            catch (Exception ex)
            {
                // Handed to the caller: an unhandled exception on this thread would end the test process.
                failure = ex;
                return;
            }
            finally
            {
                ready.Set();
            }

            Dispatcher.Run();
        })
        { Name = "Routeables WPF tests", IsBackground = true };

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        ready.Wait();
        return dispatcher ?? throw new InvalidOperationException("The WPF application of the tests could not be started.", failure);
    }

    // An exception is handed to the test that asked, never to the dispatcher: an unhandled one would end the thread of every test.
    public static Task Run(Action action) => Run(() => { action(); return true; });

    public static Task<T> Run<T>(Func<T> action)
    {
        var done = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        Ui.Value.BeginInvoke(new Action(() =>
        {
            try { done.SetResult(action()); }
            catch (Exception ex) { done.SetException(ex); }
        }));
        return done.Task;
    }

    public static Task<T> Run<T>(Func<Task<T>> action)
    {
        var done = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        Ui.Value.BeginInvoke(new Action(async () =>
        {
            try { done.SetResult(await action()); }
            catch (Exception ex) { done.SetException(ex); }
        }));
        return done.Task;
    }

    public static Task Run(Func<Task> action)
    {
        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Ui.Value.BeginInvoke(new Action(async () =>
        {
            try { await action(); done.SetResult(); }
            catch (Exception ex) { done.SetException(ex); }
        }));
        return done.Task;
    }

    /// <summary>Lets the dispatcher run what was posted to it before this call.</summary>
    public static Task Flush() => Run(() => { });

    /// <summary>
    /// Lets the dispatcher finish what is pending down to loading and rendering: <c>Loaded</c> and <c>Unloaded</c> are raised at a lower
    /// priority than the work <see cref="Flush"/> waits for.
    /// </summary>
    public static Task Idle()
    {
        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Ui.Value.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() => done.SetResult()));
        return done.Task;
    }

    /// <summary>Waits, without blocking the dispatcher, until the condition (evaluated on it) is true; fails the test after five seconds.</summary>
    public static async Task WaitUntil(Func<bool> condition, string because, CancellationToken ct, Func<string>? details = null)
    {
        for (var i = 0; i < 250; i++)
        {
            if (await Run(condition))
                return;

            await Task.Delay(20, ct);
        }

        var state = details is null ? string.Empty : Environment.NewLine + await Run(details);
        Xunit.Assert.Fail("Timed out after 5 seconds waiting for: " + because + state);
    }
}

/// <summary>The WPF tests share the application and the static container (<c>ServiceLocator</c>): they do not run side by side.</summary>
[CollectionDefinition(Name)]
public sealed class WpfCollection
{
    public const string Name = "Wpf";
}
