namespace Routeables.Regions;

/// <summary>One step that brings the pile of pages of a navigation control to the history of the router.</summary>
/// <param name="Kind">What to do.</param>
/// <param name="Depth">For <see cref="PageStackStepKind.PopTo"/>: how many pages stay.</param>
/// <param name="Entry">For <see cref="PageStackStepKind.Push"/> and <see cref="PageStackStepKind.Replace"/>: the entry to show.</param>
public readonly record struct PageStackStep(PageStackStepKind Kind, int Depth = 0, object? Entry = null);

/// <summary>The kinds of <see cref="PageStackStep"/>.</summary>
public enum PageStackStepKind
{
    /// <summary>Removes the pages above the first <see cref="PageStackStep.Depth"/> ones.</summary>
    PopTo,

    /// <summary>Puts a page on top of the pile.</summary>
    Push,

    /// <summary>Replaces the page on top of the pile.</summary>
    Replace,
}

/// <summary>
/// The steps that turn the pile of pages a navigation control shows (<c>NavigationPage</c> of Avalonia) into the pile the history of the router
/// asks for: the entries from the first to the current one. The history is the truth: whatever the control did on its own is undone.
/// No UI here; the platform runs the steps one after the other, waiting for each transition to end.
/// </summary>
public static class PageStackPlanner
{
    /// <summary>Plans the steps from <paramref name="actual"/> to <paramref name="wanted"/>.</summary>
    /// <param name="actual">What the control holds now, bottom first; compared by reference.</param>
    /// <param name="wanted">What the history asks for, bottom first; compared by reference.</param>
    /// <returns>The steps, possibly none.</returns>
    public static IReadOnlyList<PageStackStep> Plan(IReadOnlyList<object> actual, IReadOnlyList<object> wanted)
    {
        ArgumentNullException.ThrowIfNull(actual);
        ArgumentNullException.ThrowIfNull(wanted);

        var common = 0;
        while (common < actual.Count && common < wanted.Count && ReferenceEquals(actual[common], wanted[common]))
            common++;

        if (common == actual.Count && common == wanted.Count)
            return [];

        var steps = new List<PageStackStep>();
        var from = common;

        if (actual.Count == 0)
        {
            // Nothing shown yet.
        }
        else if (common == 0)
        {
            // The root is not the same page: the control keeps one page at least, which is replaced.
            if (actual.Count > 1)
                steps.Add(new PageStackStep(PageStackStepKind.PopTo, 1));
            if (wanted.Count > 0)
            {
                steps.Add(new PageStackStep(PageStackStepKind.Replace, Entry: wanted[0]));
                from = 1;
            }
        }
        else if (actual.Count == common + 1 && wanted.Count > common)
        {
            steps.Add(new PageStackStep(PageStackStepKind.Replace, Entry: wanted[common]));
            from = common + 1;
        }
        else if (actual.Count > common)
        {
            steps.Add(new PageStackStep(PageStackStepKind.PopTo, common));
        }

        for (var i = from; i < wanted.Count; i++)
            steps.Add(new PageStackStep(PageStackStepKind.Push, Entry: wanted[i]));

        return steps;
    }
}
