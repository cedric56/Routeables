using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;
using Routeables.Abstraction;
using Routeables.Routing;

namespace Routeables.Segments;

/// <summary>
/// A coordination made of ordered segments, in the manner of Prism:
/// <c>Orders?status=open/Detail?id=5</c>. Each segment is handled in turn by its router and receives only
/// its own query parameters.
/// <list type="bullet">
/// <item><c>Name?k=v&amp;k=w</c> pushes the segment registered under <c>Name</c>.</item>
/// <item><c>region:Name</c> hands the segment to the router of that region.</item>
/// <item><c>..</c> goes back one entry (<c>../Edit?id=5</c>: back, then push).</item>
/// <item>A leading <c>/</c> makes the coordination absolute: the history of every router involved is cleared first.</item>
/// </list>
/// </summary>
/// <param name="Segments">The segments, in coordination order. Never empty.</param>
/// <param name="Absolute">Whether the histories of the routers involved are cleared before the first segment.</param>
public sealed record RouteCoordination(IReadOnlyList<RouteSegment> Segments, bool Absolute = false)
{
    /// <summary>Starts a fluent description of a coordination.</summary>
    public static RouteCoordinationBuilder Create() => new();

    /// <summary>Parses a segment URI such as <c>/main:Orders?status=open/Detail?id=5</c>.</summary>
    /// <exception cref="FormatException">The URI is empty or malformed.</exception>
    public static RouteCoordination Parse(string uri)
        => TryParse(uri, out var coordination, out var error)
            ? coordination
            : throw error;

    /// <summary>Parses a segment URI; <paramref name="error"/> explains the failure when it returns <see langword="false"/>.</summary>
    public static bool TryParse(
        string? uri,
        [NotNullWhen(true)] out RouteCoordination? coordination,
        [NotNullWhen(false)] out Exception? error)
    {
        coordination = null;
        error = null;

        if (string.IsNullOrWhiteSpace(uri))
        {
            error = new FormatException("The route URI is empty.");
            return false;
        }

        var text = uri.Trim();
        var absolute = text.StartsWith('/');
        var pieces = text.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (pieces.Length == 0)
        {
            error = new FormatException("The route URI has no segment.");
            return false;
        }

        var segments = new List<RouteSegment>(pieces.Length);
        foreach (var piece in pieces)
        {
            var q = piece.IndexOf('?');
            var head = q < 0 ? piece : piece[..q];
            var query = q < 0 ? string.Empty : piece[(q + 1)..];

            string? region = null;
            var colon = head.IndexOf(':');
            if (colon >= 0)
            {
                region = Uri.UnescapeDataString(head[..colon]);
                head = head[(colon + 1)..];
                if (region.Length == 0)
                {
                    error = new FormatException($"Segment '{piece}' has an empty region.");
                    return false;
                }
            }

            var name = Uri.UnescapeDataString(head);
            if (name.Length == 0)
            {
                error = new Exception($"Segment '{piece}' has no name.");
                return false;
            }

            var parameters = ParseQuery(query);
            if (name == RouteSegment.BackName)
            {
                if (parameters.Count > 0)
                {
                    error = new FormatException("A back segment ('..') cannot carry parameters.");
                    return false;
                }
                segments.Add(new RouteSegment(name, region, RouteSegmentKind.Back));
            }
            else
            {
                segments.Add(new RouteSegment(name, region, RouteSegmentKind.Push, parameters));
            }
        }

        coordination = new RouteCoordination(segments, absolute);
        return true;
    }

    private static List<KeyValuePair<string, object?>> ParseQuery(string query)
    {
        var result = new List<KeyValuePair<string, object?>>();
        foreach (var part in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var eq = part.IndexOf('=');
            var key = Uri.UnescapeDataString(eq < 0 ? part : part[..eq]);
            if (key.Length == 0) continue;
            object? value = eq < 0 ? null : Uri.UnescapeDataString(part[(eq + 1)..]);
            result.Add(new(key, value));
        }
        return result;
    }

    /// <summary>
    /// The URI form of this coordination; <see cref="Parse"/> reads it back (parameter values become strings).
    /// Arguments set through <see cref="RouteSegment.Configure"/> are not part of it.
    /// </summary>
    public override string ToString()
    {
        var sb = new StringBuilder();
        if (Absolute) sb.Append('/');

        for (var i = 0; i < Segments.Count; i++)
        {
            if (i > 0) sb.Append('/');
            var segment = Segments[i];
            if (segment.Region is not null)
                sb.Append(Uri.EscapeDataString(segment.Region)).Append(':');
            sb.Append(segment.Kind == RouteSegmentKind.Back ? RouteSegment.BackName : Uri.EscapeDataString(segment.Name));

            var first = true;
            foreach (var (key, value) in segment.Parameters ?? [])
            {
                sb.Append(first ? '?' : '&');
                first = false;
                sb.Append(Uri.EscapeDataString(key));
                if (value is not null)
                    sb.Append('=').Append(Uri.EscapeDataString(Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty));
            }
        }
        return sb.ToString();
    }
}

/// <summary>Fluent builder for <see cref="RouteCoordination"/>; see <see cref="RouteCoordination.Create"/>.</summary>
public sealed class RouteCoordinationBuilder
{
    private readonly List<RouteSegment> _segments = [];
    private bool _absolute;

    /// <summary>Clears the history of every router involved before the first segment.</summary>
    public RouteCoordinationBuilder Absolute()
    {
        _absolute = true;
        return this;
    }

    /// <summary>Adds the segment registered under <paramref name="name"/>.</summary>
    /// <param name="name">The registered segment name.</param>
    /// <param name="region">The region whose router handles it; <see langword="null"/> for the default router.</param>
    /// <param name="parameters">Parameters for this segment only.</param>
    public RouteCoordinationBuilder Segment(string name, string? region = null, params (string Key, object? Value)[] parameters)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        _segments.Add(new RouteSegment(
            name, region, RouteSegmentKind.Push,
            parameters.Select(p => new KeyValuePair<string, object?>(p.Key, p.Value)).ToList()));
        return this;
    }

    /// <summary>
    /// Adds the segment registered under <paramref name="name"/> and fills its arguments directly, exactly as with
    /// <c>RouteableArgs</c>: <c>args =&gt; args.ForViewModel(new RouteableArgument("open", "status", true)).ForView(...)</c>.
    /// </summary>
    /// <param name="name">The registered segment name.</param>
    /// <param name="configure">Fills the segment's <see cref="IRouteableArgs"/>.</param>
    /// <param name="region">The region whose router handles it; <see langword="null"/> for the default router.</param>
    public RouteCoordinationBuilder Segment(string name, Action<IRouteableArgs> configure, string? region = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(configure);
        _segments.Add(new RouteSegment(name, region, RouteSegmentKind.Push, null, configure));
        return this;
    }

    /// <summary>Adds the segment registered for <typeparamref name="TViewModel"/> and fills its arguments directly.</summary>
    public RouteCoordinationBuilder Segment<TViewModel>(Action<IRouteableArgs> configure, string? region = null)
        => Segment(typeof(TViewModel).Name, configure, region);

    /// <summary>Adds the segment registered for <typeparamref name="TViewModel"/> (named after the type).</summary>
    public RouteCoordinationBuilder Segment<TViewModel>(string? region = null, params (string Key, object? Value)[] parameters)
        => Segment(typeof(TViewModel).Name, region, parameters);

    /// <summary>Goes back one entry on the router of <paramref name="region"/> (the default router when <see langword="null"/>).</summary>
    public RouteCoordinationBuilder Back(string? region = null)
    {
        _segments.Add(new RouteSegment(RouteSegment.BackName, region, RouteSegmentKind.Back));
        return this;
    }

    /// <summary>Creates the coordination.</summary>
    /// <exception cref="InvalidOperationException">No segment was added.</exception>
    public RouteCoordination Build()
        => _segments.Count == 0
            ? throw new InvalidOperationException("A coordination needs at least one segment.")
            : new RouteCoordination(_segments.ToArray(), _absolute);
}