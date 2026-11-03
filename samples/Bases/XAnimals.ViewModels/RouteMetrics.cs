using System.Diagnostics.Metrics;
using Routeables.Abstraction;

namespace XAnimals.ViewModels;

/// <summary>
/// Listens to the measures the router publishes (<see cref="RouteableDiagnostics.Name"/>, the histogram <c>routeables.route.duration</c>) and
/// keeps the last ones. An application would send them to OpenTelemetry; here the Features page shows them.
/// </summary>
public static class RouteMetrics
{
    private const int Keep = 12;

    private static readonly object Gate = new();
    private static readonly Queue<string> Lines = new();
    private static MeterListener? listener;

    /// <summary>Starts listening; called once by <c>RegisterViewModels</c>.</summary>
    public static void Start()
    {
        lock (Gate)
        {
            if (listener is not null)
                return;

            var created = new MeterListener();
            created.InstrumentPublished = (instrument, source) =>
            {
                if (instrument.Meter.Name == RouteableDiagnostics.Name && instrument.Name == "routeables.route.duration")
                    source.EnableMeasurementEvents(instrument);
            };
            created.SetMeasurementEventCallback<double>(OnMeasurement);
            created.Start();
            listener = created;
        }
    }

    /// <summary>The last route changes, the oldest first: <c>Push  Success  0.4 ms</c>.</summary>
    public static IReadOnlyList<string> Last()
    {
        lock (Gate)
            return [.. Lines];
    }

    private static void OnMeasurement(Instrument instrument, double milliseconds, ReadOnlySpan<KeyValuePair<string, object?>> tags, object? state)
    {
        string? kind = null, outcome = null;
        foreach (var tag in tags)
        {
            switch (tag.Key)
            {
                case "routeables.kind": kind = tag.Value?.ToString(); break;
                case "routeables.outcome": outcome = tag.Value?.ToString(); break;
            }
        }

        lock (Gate)
        {
            Lines.Enqueue($"{kind}  {outcome}  {milliseconds:0.0} ms");
            while (Lines.Count > Keep)
                Lines.Dequeue();
        }
    }
}
