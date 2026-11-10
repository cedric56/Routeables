using Xunit;

namespace Routeables.Tests.Templates;

/// <summary>
/// The <c>dotnet new</c> templates of <c>templates/</c> are not built by the CI: these checks keep what can be checked without building them
/// (each one is described, is packed, and its source name is the one the files use).
/// </summary>
public class TemplateFilesTests
{
    private static string Root()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "Routeables.slnx")))
            root = root.Parent;
        Assert.NotNull(root);
        return root!.FullName;
    }

    private static IEnumerable<string> Templates()
        => Directory.EnumerateDirectories(Path.Combine(Root(), "templates"))
            .Where(d => File.Exists(Path.Combine(d, "content", ".template.config", "template.json")));

    [Fact]
    public void Each_template_is_described_packed_and_uses_its_source_name()
    {
        var found = Templates().ToList();
        Assert.Contains(found, d => Path.GetFileName(d) == "avalonia");
        Assert.Contains(found, d => Path.GetFileName(d) == "blazor");

        var pack = File.ReadAllText(Path.Combine(Root(), "templates", "Routeables.Templates.csproj"));
        var shortNames = new List<string>();
        foreach (var dir in found)
        {
            var name = Path.GetFileName(dir);
            var json = File.ReadAllText(Path.Combine(dir, "content", ".template.config", "template.json"));
            var shortName = System.Text.RegularExpressions.Regex.Match(json, "\"shortName\"\\s*:\\s*\"([^\"]+)\"").Groups[1].Value;
            var sourceName = System.Text.RegularExpressions.Regex.Match(json, "\"sourceName\"\\s*:\\s*\"([^\"]+)\"").Groups[1].Value;

            Assert.False(string.IsNullOrEmpty(shortName), $"{name}: no shortName");
            Assert.False(string.IsNullOrEmpty(sourceName), $"{name}: no sourceName");
            shortNames.Add(shortName);
            Assert.Contains($"{name}\\**\\*", pack);   // packed: without this line the template is not in the package
            Assert.True(File.Exists(Path.Combine(dir, "content", sourceName + ".csproj")), $"{name}: {sourceName}.csproj is missing");
        }
        Assert.Equal(shortNames.Count, shortNames.Distinct().Count());
    }

    [Fact]
    public void The_Blazor_template_links_the_stylesheets_the_dialogs_need_and_registers_its_pages()
    {
        var content = Path.Combine(Root(), "templates", "blazor", "content");
        var app = File.ReadAllText(Path.Combine(content, "Components", "App.razor"));
        Assert.Contains("RouteablesApp.styles.css", app);                         // the scoped styles of Routeables.Blazor are in this bundle
        Assert.Contains("_content/Routeables.Blazor/theme.css", app);
        Assert.Contains("@using Microsoft.AspNetCore.Components.Web", app);

        var program = File.ReadAllText(Path.Combine(content, "Program.cs"));
        Assert.Contains("UseRouteablesServer", program);
        Assert.Contains("RegisterRoute<HomeView, HomeViewModel>", program);
        Assert.Contains("RegisterRoute<HelloView, HelloViewModel>", program);
        Assert.Contains("MapStaticAssets", program);
    }
}
