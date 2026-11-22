using Routeables.Abstraction.Routing;
using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;

namespace Routeables.Routing;

// Choosing the most specific template that matches an address.
public static partial class RouteParser
{
    /// <summary>
    /// The registered route that opens <paramref name="uri"/>. When several templates match, the most specific wins, whatever their
    /// registration order: segment by segment from the left, a literal beats a typed parameter (<c>{id:int}</c>, a registered type),
    /// which beats an untyped one (<c>{id}</c>), which beats an optional one (<c>{tab?}</c>). <c>/bears/new</c> is therefore
    /// <c>bears/new</c>, not <c>bears/{id}</c>. When one template is the start of the other, the shorter wins (<c>/products</c> over
    /// <c>/products/{tab?}</c>). Equally specific templates keep the order of registration.
    /// </summary>
    /// <returns>The entry, or one whose <c>Template</c> is <see langword="null"/> when no template matches.</returns>
    internal static (string Template, Type? ViewModelType) BestMatch(
        IEnumerable<(string Template, Type? ViewModelType)> entries, string uri)
    {
        var best = default((string Template, Type? ViewModelType));
        int[]? bestRank = null;
        int? bestQuery = null;

        void Consider((string Template, Type? ViewModelType) entry)
        {
            if (!TryMatch(entry.Template, uri))
                return;

            var rank = Specificity(entry.Template);
            var order = bestRank is null ? 1 : CompareRanks(rank, bestRank);
            var queryScore = 0;
            if (order == 0)
            {
                // Same path: the template that uses more of the address's query is the better match (/search?{q} beats /search for /search?q=1).
                queryScore = QueryScore(entry.Template, uri);
                bestQuery ??= QueryScore(best.Template!, uri);
                if (queryScore > bestQuery)
                    order = 1;
            }

            if (order > 0)
            {
                best = entry;
                bestRank = rank;
                bestQuery = order == 1 && queryScore > 0 ? queryScore : null;
            }
        }

        // The application's table never changes once built (adding a route creates a new collection), so it is indexed by its first literal
        // segment. Only templates that can open the address are tried, in registration order, which also breaks ties.
        if (entries is ReadOnlyCollection<(string Template, Type? ViewModelType)> table && table.Count >= IndexFrom)
        {
            var index = Indexes.GetValue(table, t => new FirstSegmentIndex(t));
            if (index.Count != table.Count)
                index = new FirstSegmentIndex(table); // the collection was modified behind our back: do not trust the index

            foreach (var at in index.Candidates(FirstSegment(uri)))
                Consider(table[at]);
            return best;
        }

        foreach (var entry in entries)
            Consider(entry);
        return best;
    }

    // How many query entries of the address a template uses: its fixed entries ("mode=grid") plus the placeholders that get a value.
    private static int QueryScore(string template, string uri)
    {
        var parsed = ParseTemplate(template);
        if (parsed.QueryParts.Length == 0)
            return 0;

        uri = RouteAddress.WithoutFragment(uri);
        var (_, uQuery) = Split(uri, template: false);
        IReadOnlyDictionary<string, string> query = uQuery.Length == 0 ? NoQuery : ParseQuery(uQuery);
        var score = 0;
        for (var q = 0; q < parsed.QueryParts.Length; q++)
        {
            var m = parsed.QueryMatches[q];
            if (!m.Success)
            {
                if (parsed.QueryParts[q].IndexOf('=') > 0)
                    score++;
            }
            else if (query.TryGetValue(m.Groups["name"].Value, out var raw) && !string.IsNullOrEmpty(raw))
                score++;
        }
        return score;
    }

    private const int IndexFrom = 8; // below this size, trying every template is as fast as an index lookup
    private static readonly ConditionalWeakTable<ReadOnlyCollection<(string Template, Type? ViewModelType)>, FirstSegmentIndex> Indexes = new();

    /// <summary>The first segment of an address as the matching reads it: the fragment and the query left out, empty segments skipped, unescaped.</summary>
    private static string FirstSegment(string uri)
    {
        // Cut where the parser cuts: the fragment first, then the query as RouteAddress finds it (a "?" inside braces is not always the query's).
        var hash = uri.IndexOf('#');
        var length = hash < 0 ? uri.Length : hash;
        var end = RouteAddress.QueryStart(uri, length);
        var path = uri.AsSpan(0, end < 0 ? length : end);
        path = path.TrimStart('/');
        var slash = path.IndexOf('/');
        var first = slash < 0 ? path : path[..slash];
        return first.IndexOf('%') < 0 ? first.ToString() : Uri.UnescapeDataString(first.ToString());
    }

    /// <summary>
    /// The registered templates by their first segment: the ones that start with a literal under that literal, the ones that start
    /// with a placeholder (or have no segment: "/") apart, because they can open any address.
    /// </summary>
    private sealed class FirstSegmentIndex
    {
        private readonly Dictionary<string, int[]> _byLiteral = new(StringComparer.OrdinalIgnoreCase);
        private readonly int[] _open;
        public int Count { get; }

        public FirstSegmentIndex(ReadOnlyCollection<(string Template, Type? ViewModelType)> table)
        {
            Count = table.Count;
            var literal = new Dictionary<string, List<int>>(StringComparer.OrdinalIgnoreCase);
            var open = new List<int>();
            for (var i = 0; i < table.Count; i++)
            {
                var parsed = ParseTemplate(table[i].Template);
                if (parsed.Segments.Length > 0 && !parsed.SegmentMatches[0].Success)
                {
                    // The address is looked up unescaped (see FirstSegment), so the literal is unescaped too ("my%20page" is "my page").
                    var first = parsed.Segments[0].Contains('%') ? Uri.UnescapeDataString(parsed.Segments[0]) : parsed.Segments[0];
                    if (!literal.TryGetValue(first, out var list))
                        literal[first] = list = [];
                    list.Add(i);
                }
                else
                    open.Add(i);
            }
            foreach (var (key, list) in literal)
                _byLiteral[key] = [.. list];
            _open = [.. open];
        }

        /// <summary>The positions of the templates that can match an address starting with <paramref name="first"/>, in registration order.</summary>
        public IEnumerable<int> Candidates(string first)
        {
            if (first.Length == 0 || !_byLiteral.TryGetValue(first, out var literal))
                return _open;
            if (_open.Length == 0)
                return literal;

            // Both lists are sorted by index: merging them keeps the registration order.
            var merged = new int[literal.Length + _open.Length];
            int a = 0, b = 0, k = 0;
            while (a < literal.Length && b < _open.Length)
                merged[k++] = literal[a] < _open[b] ? literal[a++] : _open[b++];
            while (a < literal.Length) merged[k++] = literal[a++];
            while (b < _open.Length) merged[k++] = _open[b++];
            return merged;
        }
    }
}
