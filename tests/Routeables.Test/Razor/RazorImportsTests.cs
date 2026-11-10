using Xunit;

namespace Routeables.Tests.Razor;

/// <summary>
/// A component of Routeables.Blazor that handles an event (<c>@onclick</c>) must import <c>Microsoft.AspNetCore.Components.Web</c>:
/// without it Razor does not know the event and writes <c>@onclick</c> as an attribute, which the browser refuses
/// ("'@onclick' is not a valid attribute name") and the circuit ends. Nothing in the build tells.
/// </summary>
public class RazorImportsTests
{
    [Fact]
    public void Every_component_with_an_event_imports_the_Web_namespace()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "Routeables.slnx")))
            root = root.Parent;
        Assert.NotNull(root);

        var imports = Path.Combine(root!.FullName, "src", "Routeables.Blazor", "_Imports.razor");
        var shared = File.Exists(imports) && File.ReadAllText(imports).Contains("Microsoft.AspNetCore.Components.Web");

        var missing = Directory.EnumerateFiles(Path.Combine(root.FullName, "src", "Routeables.Blazor"), "*.razor", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") && !f.EndsWith("_Imports.razor"))
            .Select(f => (File: f, Text: File.ReadAllText(f)))
            .Where(f => System.Text.RegularExpressions.Regex.IsMatch(f.Text, @"@on[a-z]+(:[a-z]+)?="))
            .Where(f => !shared && !f.Text.Contains("@using Microsoft.AspNetCore.Components.Web"))
            .Select(f => Path.GetFileName(f.File))
            .ToList();

        Assert.True(missing.Count == 0, "Without @using Microsoft.AspNetCore.Components.Web: " + string.Join(", ", missing));
    }
}
