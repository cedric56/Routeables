namespace Routeables.Shared;

/// <summary>
/// Where a route or an address is cut into its path and its query. This file is compiled into every assembly that reads one (the contracts, the
/// source generator, the analyzer): there is one rule, so they cannot disagree. Written for netstandard2.0, because the generator is.
/// </summary>
internal static class AddressRules
{
    /// <summary>
    /// The start of the query of an <b>address</b>: the first question mark that does not end an optional placeholder (<c>{tab?}</c>,
    /// <c>{tab:int?}</c>); -1 for none. A brace that nothing closes is a character of a hand-typed address, not the start of a placeholder.
    /// </summary>
    /// <param name="text">The address.</param>
    /// <param name="length">How much of <paramref name="text"/> is read (the fragment is left out); -1 for all of it.</param>
    internal static int QueryStart(string text, int length = -1)
    {
        if (length < 0 || length > text.Length)
            length = text.Length;
        if (length == 0)
            return -1;

        var depth = 0;
        var lastClose = text.LastIndexOf('}', length - 1, length); // looked for once: thousands of "{" must not cost a search each
        for (var i = 0; i < length; i++)
        {
            if (text[i] == '{' && i < lastClose) depth++;
            else if (text[i] == '}' && depth > 0) depth--;
            // Inside braces a question mark is the "optional" mark only when the placeholder ends right after it: in "/notes/a{b?tab=x}" it starts the query.
            else if (text[i] == '?' && (depth == 0 || (i + 1 < length && text[i + 1] != '}'))) return i;
        }
        return -1;
    }

    /// <summary>
    /// The start of the query of a <b>template</b>: the first question mark outside the braces of its placeholders; -1 for none. A closing brace
    /// that nothing opened is a character of the template, so it does not hide the query.
    /// </summary>
    /// <param name="template">The template.</param>
    internal static int TemplateQueryStart(string template)
    {
        var depth = 0;
        for (var i = 0; i < template.Length; i++)
        {
            if (template[i] == '{') depth++;
            else if (template[i] == '}' && depth > 0) depth--;
            else if (template[i] == '?' && depth == 0) return i;
        }
        return -1;
    }
}
