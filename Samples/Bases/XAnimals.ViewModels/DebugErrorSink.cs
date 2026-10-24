using System.Diagnostics;
using Routeables.Abstraction;

namespace XAnimals.ViewModels;

/// <summary>
/// For the samples, in a Debug build: an error that Routeables does not let escape (an injection that fails, a handler that
/// throws…) is written to the debug output with its source, and stops the debugger on the line when one is attached.
/// Without a sink these errors go to <see cref="Trace"/> only, and a view model whose property was not injected just shows nothing.
/// </summary>
public sealed class DebugErrorSink : IRouteableErrorSink
{
    public void Report(string source, Exception exception)
    {
        Debug.WriteLine($"Routeables ERROR in {source}: {exception}");
        if (Debugger.IsAttached)
            Debugger.Break();
    }
}
