using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Routeables.Abstraction;
using Routeables.Abstraction.Errors;
using Routeables.Abstraction.Routing;

namespace Routeables.Services;

/// <summary>Where the history of the router is kept between two runs of the application.</summary>
public interface IRouteableHistoryStore
{
    /// <summary>What <see cref="Save"/> kept last, or <see langword="null"/> when nothing was kept (first run, unreadable file).</summary>
    string? Load();

    /// <summary>Keeps the history, replacing what was kept.</summary>
    /// <param name="text">The history as <see cref="RouteableState.Serialize(IEnumerable{RouteableSnapshot})"/> wrote it.</param>
    void Save(string text);
}

/// <summary>
/// <see cref="IRouteableHistoryStore"/> in a text file. Without a path the file is <c>routeables-history.txt</c> in a folder named after the
/// application, in the local application data of the user (the sandbox of the application on MAUI).
/// </summary>
public sealed class FileRouteableHistoryStore : IRouteableHistoryStore
{
    /// <summary>Creates the store.</summary>
    /// <param name="path">The file; <see langword="null"/> takes the default one.</param>
    public FileRouteableHistoryStore(string? path = null) => Path = path ?? DefaultPath();

    /// <summary>The file.</summary>
    public string Path { get; }

    /// <inheritdoc />
    public string? Load()
    {
        try { return File.Exists(Path) ? File.ReadAllText(Path) : null; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return null; }
    }

    /// <inheritdoc />
    public void Save(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        EnsureFolder();

        // Write aside, then move over the old file: a run that stops in the middle of a write keeps the previous history.
        // The name is unique, so two instances of the application (or two saves) never write the same temporary file.
        var temporary = $"{Path}.{Guid.NewGuid():N}.tmp";
        try
        {
            var options = new FileStreamOptions { Mode = FileMode.CreateNew, Access = FileAccess.Write, Share = FileShare.None };
            if (!OperatingSystem.IsWindows() && !OperatingSystem.IsBrowser())
                options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
            using (var stream = new FileStream(temporary, options))
            using (var writer = new StreamWriter(stream, new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false)))
                writer.Write(text);
            File.Move(temporary, Path, overwrite: true);
        }
        catch
        {
            try { File.Delete(temporary); } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { /* nothing more to do */ }
            throw;
        }
    }

    // One user's history is not for the other users of the machine. On Unix a newly created folder is for its owner (an existing folder's
    // mode is its creator's choice), and the file is created with its final mode, so it is never wider, even briefly, as it would be if the
    // mode were set after the write. The umask can only remove rights, never add them.
    private void EnsureFolder()
    {
        var folder = System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(Path));
        if (string.IsNullOrEmpty(folder))
            return;

        // The platform test is written in the condition itself, which is what the platform analyzer (CA1416) recognizes as a guard.
        if (!OperatingSystem.IsWindows() && !OperatingSystem.IsBrowser())
            Directory.CreateDirectory(folder, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        else
            Directory.CreateDirectory(folder);
    }

    private static string DefaultPath()
    {
        var name = System.Reflection.Assembly.GetEntryAssembly()?.GetName().Name ?? "Routeables";
        return System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), name, "routeables-history.txt");
    }
}
