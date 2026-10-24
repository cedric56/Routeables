using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.Loader;

namespace Routeables.Abstraction;

/// <summary>
/// Loads a module and what is next to it. An assembly the application already has (Routeables, the UI framework) comes from the
/// default context, so that the types are the same on both sides; any other is looked for with the .deps.json of the module, then next
/// to the module.
/// </summary>
[RequiresUnreferencedCode("Loads assemblies the trimmer cannot see.")]
internal sealed class ModuleLoadContext(string path) : AssemblyLoadContext(Path.GetFileNameWithoutExtension(path), isCollectible: false)
{
    private readonly AssemblyDependencyResolver _resolver = new(path);
    private readonly string _directory = Path.GetDirectoryName(Path.GetFullPath(path)) ?? string.Empty;

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        try
        {
            return Default.LoadFromAssemblyName(assemblyName);          // shared with the application
        }
        catch (FileNotFoundException)
        {
        }

        var found = _resolver.ResolveAssemblyToPath(assemblyName);
        if (found is null && assemblyName.Name is { } simple)
        {
            var beside = Path.Combine(_directory, simple + ".dll");
            if (File.Exists(beside))
                found = beside;
        }

        return found is null ? null : LoadFromAssemblyPath(found);
    }
}
