using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ImageCredits;

/// <summary>
/// Fills <c>THIRD-PARTY-NOTICES.md</c> from the sites it credits: the author and the license of each photograph (Wikimedia Commons), and the
/// Wikipedia article each description comes from. It only reads; it writes the notices file.
/// </summary>
public sealed partial class CreditsTool(HttpClient http, TextWriter log)
{
    private const string ToFill = "to fill";
    private const string ArticlesBegin = "<!-- articles:begin -->";
    private const string ArticlesEnd = "<!-- articles:end -->";

    [GeneratedRegex(@"^\| (?<subject>.*?) \| \[(?<label>.*?)\]\((?<url>https://commons\.wikimedia\.org/wiki/File:(?<file>.*?))\) \| (?<author>.*?) \| (?<license>.*?) \|\s*$")]
    private static partial Regex ImageRow();

    [GeneratedRegex("<[^>]+>")]
    private static partial Regex Tags();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Spaces();

    /// <summary>Reads the notices, asks Commons and Wikipedia, and returns the new text. <paramref name="refresh"/> also redoes the rows that are filled.</summary>
    public async Task<string> UpdateAsync(string notices, bool refresh, CancellationToken ct = default)
    {
        var lines = notices.Replace("\r\n", "\n").Split('\n').ToList();
        var rows = new List<(int Index, System.Text.RegularExpressions.Match Match)>();
        for (var i = 0; i < lines.Count; i++)
        {
            var m = ImageRow().Match(lines[i]);
            if (m.Success && (refresh || m.Groups["author"].Value == ToFill || m.Groups["license"].Value == ToFill))
                rows.Add((i, m));
        }

        var files = rows.Select(r => Uri.UnescapeDataString(r.Match.Groups["file"].Value)).Distinct().ToList();
        var credits = await CommonsAsync(files, ct);

        var missing = 0;
        foreach (var (index, m) in rows)
        {
            var file = Uri.UnescapeDataString(m.Groups["file"].Value);
            if (!credits.TryGetValue(file, out var credit))
            {
                log.WriteLine($"not found on Commons, left as it is: {file}");
                missing++;
                continue;
            }

            lines[index] = $"| {m.Groups["subject"].Value} | [{m.Groups["label"].Value}]({m.Groups["url"].Value}) | {credit.Author} | {credit.License} |";
        }

        log.WriteLine($"{rows.Count - missing} of {rows.Count} photographs filled.");

        // The articles: every subject of the image table, looked up on Wikipedia.
        var subjects = lines.Select(l => ImageRow().Match(l)).Where(m => m.Success).Select(m => m.Groups["subject"].Value).Distinct().ToList();
        if (subjects.Count > 0)
            await ReplaceArticlesAsync(lines, subjects, ct);

        return string.Join("\n", lines);
    }

    private sealed record Credit(string Author, string License);

    private async Task<Dictionary<string, Credit>> CommonsAsync(IReadOnlyList<string> files, CancellationToken ct)
    {
        var result = new Dictionary<string, Credit>();
        foreach (var batch in files.Chunk(40))
        {
            var titles = batch.Select(f => "File:" + f).ToList();
            var url = "https://commons.wikimedia.org/w/api.php?action=query&format=json&formatversion=2&redirects=1&prop=imageinfo&iiprop=extmetadata"
                    + "&iiextmetadatafilter=" + Uri.EscapeDataString("Artist|LicenseShortName|LicenseUrl")
                    + "&titles=" + Uri.EscapeDataString(string.Join("|", titles));
            using var doc = await GetJsonAsync(url, ct);
            var query = doc.RootElement.GetProperty("query");
            var pages = Pages(query);

            foreach (var file in batch)
            {
                var title = Resolve(query, "File:" + file);
                if (!pages.TryGetValue(title, out var page) || page.TryGetProperty("missing", out _)
                    || !page.TryGetProperty("imageinfo", out var info) || info.GetArrayLength() == 0
                    || !info[0].TryGetProperty("extmetadata", out var meta))
                    continue;

                var artist = Clean(Value(meta, "Artist"));
                var license = Value(meta, "LicenseShortName");
                var licenseUrl = Value(meta, "LicenseUrl");
                if (string.IsNullOrWhiteSpace(license))
                    continue;

                var licenseText = string.IsNullOrWhiteSpace(licenseUrl) ? Clean(license) : $"[{Clean(license)}]({licenseUrl.Trim()})";
                result[file] = new Credit(string.IsNullOrWhiteSpace(artist) ? "unknown (see the Commons page)" : artist, licenseText);
            }
        }

        return result;
    }

    private async Task ReplaceArticlesAsync(List<string> lines, IReadOnlyList<string> subjects, CancellationToken ct)
    {
        var table = new List<string> { "| Subject | Article | Authors |", "|---|---|---|" };
        foreach (var batch in subjects.Chunk(40))
        {
            var url = "https://en.wikipedia.org/w/api.php?action=query&format=json&formatversion=2&redirects=1&prop=info%7Cpageprops&inprop=url&ppprop=disambiguation"
                    + "&titles=" + Uri.EscapeDataString(string.Join("|", batch));
            using var doc = await GetJsonAsync(url, ct);
            var query = doc.RootElement.GetProperty("query");
            var pages = Pages(query);

            foreach (var subject in batch)
            {
                var title = Resolve(query, subject);
                if (!pages.TryGetValue(title, out var page) || page.TryGetProperty("missing", out _))
                {
                    log.WriteLine($"no Wikipedia article called \"{subject}\": write its row by hand.");
                    table.Add($"| {subject} | to fill | to fill |");
                    continue;
                }

                var full = page.GetProperty("fullurl").GetString()!;
                var name = page.GetProperty("title").GetString()!;

                if (page.TryGetProperty("pageprops", out var props) && props.TryGetProperty("disambiguation", out _))
                {
                    log.WriteLine($"\"{subject}\" leads to a disambiguation page: put the article it is really taken from.");
                    table.Add($"| {subject} | to fill (\"{name}\" is a [disambiguation page]({full})) | to fill |");
                    continue;
                }

                var history = $"https://en.wikipedia.org/w/index.php?title={Uri.EscapeDataString(name.Replace(' ', '_'))}&action=history";
                table.Add($"| {subject} | [{name}]({full}) | [history]({history}) |");
            }
        }

        var begin = lines.FindIndex(l => l.Trim() == ArticlesBegin);
        var end = lines.FindIndex(l => l.Trim() == ArticlesEnd);
        if (begin < 0 || end < begin)
        {
            log.WriteLine($"the notices have no {ArticlesBegin} ... {ArticlesEnd} block: the articles were not written.");
            return;
        }

        lines.RemoveRange(begin + 1, end - begin - 1);
        lines.InsertRange(begin + 1, table);
    }

    private async Task<JsonDocument> GetJsonAsync(string url, CancellationToken ct)
    {
        using var response = await http.GetAsync(url, ct);
        response.EnsureSuccessStatusCode();
        return await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
    }

    private static Dictionary<string, JsonElement> Pages(JsonElement query)
    {
        var pages = new Dictionary<string, JsonElement>();
        if (query.TryGetProperty("pages", out var array))
            foreach (var page in array.EnumerateArray())
                pages[page.GetProperty("title").GetString()!] = page;
        return pages;
    }

    // The title the API answers under: normalized (underscores, first letter), then followed through redirects.
    private static string Resolve(JsonElement query, string title)
    {
        title = Map(query, "normalized", title);
        return Map(query, "redirects", title);
    }

    private static string Map(JsonElement query, string name, string title)
    {
        if (query.TryGetProperty(name, out var list))
            foreach (var item in list.EnumerateArray())
                if (item.GetProperty("from").GetString() == title)
                    return item.GetProperty("to").GetString()!;
        return title;
    }

    private static string Value(JsonElement meta, string key)
        => meta.TryGetProperty(key, out var item) && item.TryGetProperty("value", out var value) ? value.GetString() ?? "" : "";

    // The author comes as HTML (a link, a name, sometimes a paragraph): the text of it, on one line, without the characters of a table.
    private static string Clean(string html)
    {
        var text = WebUtility.HtmlDecode(Tags().Replace(html, " "));
        text = Spaces().Replace(text, " ").Trim().Replace("|", "/");
        return text.Length > 120 ? text[..117].TrimEnd() + "..." : text;
    }
}
