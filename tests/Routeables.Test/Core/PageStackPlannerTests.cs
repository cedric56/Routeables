using Routeables.Regions;

namespace Routeables.Tests.Core;

/// <summary>The steps that make the pile of pages of a navigation control follow the history of the router.</summary>
public class PageStackPlannerTests
{
    private static readonly object A = new(), B = new(), C = new(), D = new();

    private static string Show(IReadOnlyList<PageStackStep> steps, params object[] names)
    {
        string Name(object? o) => o == A ? "A" : o == B ? "B" : o == C ? "C" : o == D ? "D" : "?";
        return string.Join(" ", steps.Select(s => s.Kind switch
        {
            PageStackStepKind.PopTo => $"pop-to-{s.Depth}",
            PageStackStepKind.Push => $"push-{Name(s.Entry)}",
            _ => $"replace-{Name(s.Entry)}",
        }));
    }

    [Fact]
    public void The_same_pile_needs_no_step() => Assert.Equal("", Show(PageStackPlanner.Plan([A, B], [A, B])));

    [Fact]
    public void A_new_page_is_pushed() => Assert.Equal("push-C", Show(PageStackPlanner.Plan([A, B], [A, B, C])));

    [Fact]
    public void Going_back_pops_to_the_page_that_stays() => Assert.Equal("pop-to-2", Show(PageStackPlanner.Plan([A, B, C], [A, B])));

    [Fact]
    public void Going_back_to_the_root_pops_to_it() => Assert.Equal("pop-to-1", Show(PageStackPlanner.Plan([A, B, C], [A])));

    [Fact]
    public void Going_forward_pushes_the_page_again() => Assert.Equal("push-C", Show(PageStackPlanner.Plan([A, B], [A, B, C])));

    [Fact]
    public void A_different_page_on_top_replaces_it() => Assert.Equal("replace-D", Show(PageStackPlanner.Plan([A, B, C], [A, B, D])));

    [Fact]
    public void A_different_page_deeper_pops_then_pushes() => Assert.Equal("pop-to-1 push-C push-D", Show(PageStackPlanner.Plan([A, B, D], [A, C, D])));

    [Fact]
    public void A_different_root_is_replaced_after_the_others_are_popped() => Assert.Equal("pop-to-1 replace-C push-D", Show(PageStackPlanner.Plan([A, B], [C, D])));

    [Fact]
    public void A_single_different_root_is_replaced() => Assert.Equal("replace-B", Show(PageStackPlanner.Plan([A], [B])));

    [Fact]
    public void An_empty_control_gets_every_page_pushed() => Assert.Equal("push-A push-B", Show(PageStackPlanner.Plan([], [A, B])));

    [Fact]
    public void An_empty_history_leaves_the_root_alone() => Assert.Equal("pop-to-1", Show(PageStackPlanner.Plan([A, B], [])));
}
