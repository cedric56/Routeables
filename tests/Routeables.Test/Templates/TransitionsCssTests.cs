namespace Routeables.Tests.Templates;

/// <summary>
/// The animations of the transitions are written twice, because a static web asset belongs to one package: <c>transitions.css</c> in
/// Routeables.AspNetCore and the end of <c>theme.css</c> in Routeables.Blazor. The two must say the same: this keeps them from drifting.
/// </summary>
public class TransitionsCssTests
{
    private static string Root()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "Routeables.slnx")))
            root = root.Parent;
        Assert.NotNull(root);
        return root!.FullName;
    }

    private static string Normalize(string css) => string.Join('\n', css.Replace("\r\n", "\n").Split('\n').Select(l => l.TrimEnd())).Trim();

    [Fact]
    public void The_transitions_of_Blazor_and_AspNetCore_are_the_same()
    {
        var transitions = Normalize(File.ReadAllText(Path.Combine(Root(), "src", "Routeables.AspNetCore", "wwwroot", "transitions.css")));
        var theme = Normalize(File.ReadAllText(Path.Combine(Root(), "src", "Routeables.Blazor", "wwwroot", "theme.css")));

        Assert.EndsWith(transitions, theme);
    }
}
