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
/// Protects the history before it is kept, and reads it back. The history holds the addresses of the pages and what the pages saved (<see cref="Abstraction.States.PersistAttribute"/>:
/// a draft of a form, an identifier): it is written as plain text unless a protector is given to <see cref="RouteableHistoryExtensions.AddRouteableHistory"/>.
/// </summary>
public interface IRouteableHistoryProtector
{
    /// <summary>Turns the history into what is kept (an encrypted text, say).</summary>
    string Protect(string text);

    /// <summary>Gives the history back; <see langword="null"/> when it cannot be read (another key, a file that was changed): the application then starts with no history.</summary>
    string? Unprotect(string text);
}

/// <summary>
/// <see cref="IRouteableHistoryProtector"/> with AES-GCM: the history is encrypted and cannot be changed without the key being noticed. The key
/// is the one of the application: keep it in the secure storage of the platform (the keychain, the keystore, <c>SecureStorage</c> on MAUI),
/// never in the source code or next to the file.
/// </summary>
public sealed class AesRouteableHistoryProtector : IRouteableHistoryProtector
{
    private const string Prefix = "rh1:";
    private const int NonceSize = 12;
    private const int TagSize = 16;

    private readonly byte[] _key;

    /// <summary>Creates the protector.</summary>
    /// <param name="key">16, 24 or 32 bytes (a copy is kept).</param>
    public AesRouteableHistoryProtector(byte[] key)
    {
        ArgumentNullException.ThrowIfNull(key);
        if (key.Length is not (16 or 24 or 32))
            throw new ArgumentException("The key must be 16, 24 or 32 bytes long.", nameof(key));
        _key = (byte[])key.Clone();
    }

    /// <inheritdoc />
    public string Protect(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var plain = System.Text.Encoding.UTF8.GetBytes(text);
        var packed = new byte[NonceSize + TagSize + plain.Length];
        var nonce = packed.AsSpan(0, NonceSize);
        var tag = packed.AsSpan(NonceSize, TagSize);
        System.Security.Cryptography.RandomNumberGenerator.Fill(nonce);
        using var aes = new System.Security.Cryptography.AesGcm(_key, TagSize);
        aes.Encrypt(nonce, plain, packed.AsSpan(NonceSize + TagSize), tag);
        return Prefix + Convert.ToBase64String(packed);
    }

    /// <inheritdoc />
    public string? Unprotect(string text)
    {
        if (text is null || !text.StartsWith(Prefix, StringComparison.Ordinal))
            return null;
        try
        {
            var packed = Convert.FromBase64String(text[Prefix.Length..]);
            if (packed.Length < NonceSize + TagSize)
                return null;
            var plain = new byte[packed.Length - NonceSize - TagSize];
            using var aes = new System.Security.Cryptography.AesGcm(_key, TagSize);
            aes.Decrypt(packed.AsSpan(0, NonceSize), packed.AsSpan(NonceSize + TagSize), packed.AsSpan(NonceSize, TagSize), plain);
            return System.Text.Encoding.UTF8.GetString(plain);
        }
        catch (Exception ex) when (ex is FormatException or System.Security.Cryptography.CryptographicException)
        {
            return null;
        }
    }
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

        var folder = System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(Path));
        if (!string.IsNullOrEmpty(folder))
            Directory.CreateDirectory(folder);

        // Written aside and moved over the old file: a run that stops in the middle of a write keeps the previous history.
        // The name is unique: two instances of the application (or two saves) never write the same temporary file.
        var temporary = $"{Path}.{Guid.NewGuid():N}.tmp";
        try
        {
            File.WriteAllText(temporary, text);
            // The history of one user is not for the others of the machine (Unix: the mode is set before the file takes the place of the old one).
            if (!OperatingSystem.IsWindows() && !OperatingSystem.IsBrowser())
                File.SetUnixFileMode(temporary, UnixFileMode.UserRead | UnixFileMode.UserWrite);
            File.Move(temporary, Path, overwrite: true);
        }
        catch
        {
            try { File.Delete(temporary); } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { /* nothing more to do */ }
            throw;
        }
    }

    private static string DefaultPath()
    {
        var name = System.Reflection.Assembly.GetEntryAssembly()?.GetName().Name ?? "Routeables";
        return System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), name, "routeables-history.txt");
    }
}

/// <summary>
/// Keeps the history of the router between two runs of the application, on every host that has a container of its own (WPF, Avalonia, WinUI,
/// Uno, WinUI 3, MAUI): the pages shown, with what they saved (<see cref="RouteableState"/>), are written to an
/// <see cref="IRouteableHistoryStore"/> after each route change and put back when the application starts again, once its first page is shown.
/// Switched on by <see cref="RouteableHistoryExtensions.AddRouteableHistory"/>; the host starts it when it builds its container.
/// </summary>
/// <remarks>
/// Blazor keeps the history of a circuit with <c>PersistentComponentState</c> instead (see <c>RouteableHistoryOptions</c>), and the ASP.NET Core
/// host keeps a scope per visitor in memory.
/// </remarks>
public sealed class RouteableHistoryKeeper(IServiceProvider provider, IRouteableHistoryStore store, IRouteableHistoryProtector? protector) : IDisposable
{
    /// <summary>Creates the keeper with a history that is not protected (the constructor of version 2.1.0).</summary>
    /// <param name="provider">The container of the application.</param>
    /// <param name="store">Where the history is kept.</param>
    public RouteableHistoryKeeper(IServiceProvider provider, IRouteableHistoryStore store) : this(provider, store, null)
    {
    }

    private readonly object _gate = new();
    private IRouter? _router;
    private volatile bool _active;
    private volatile bool _disposed;
    private readonly CancellationTokenSource _stopped = new();
    private string? _last;
    private long _ticket;       // taken before a history is read
    private long _savedTicket;  // the one of the history that was written last

    /// <summary>Starts keeping the history: reads what was kept, and writes the history after each route change from then on. Safe to call twice.</summary>
    public void Start()
    {
        IRouter? router;
        lock (_gate)
        {
            if (_router is not null || provider.GetService<IRouter>() is not { } found)
                return;
            _router = router = found;
        }

        IReadOnlyList<RouteableSnapshot> kept = [];
        try { kept = RouteableState.DeserializeSnapshots(Unprotect(store.Load())); }
        catch (Exception ex) { RouteableErrors.Report<RouteableHistoryKeeper>(provider, ex, "Load"); }

        router.Routed += OnRouted;
        router.History.Changed += OnHistoryChanged;

        // Nothing is written before the pages are back: the first page of the new run must not replace the history that is waiting.
        if (kept.Count == 0)
        {
            _active = true;
            return;
        }

        // The addresses that opened the application (RouteableLinkInbox) wait until the pages are back: restoring starts by erasing the history.
        RouteableStartup.BeginRestore(router);
        RouteableHelper.FireAndForget(() => Task.Run(() => RestoreAsync(router, kept)), provider, "RouteableHistoryKeeper.Restore");
    }

    private async Task RestoreAsync(IRouter router, IReadOnlyList<RouteableSnapshot> kept)
    {
        // The keeper may be disposed before this runs: its source is disposed with it, and a disposed source gives no token.
        CancellationToken stop;
        try { stop = _stopped.Token; }
        catch (ObjectDisposedException) { return; }

        try
        {
            if (!await RouteableLinkInbox.WaitForFirstPageAsync(router, stop) || _disposed)
                return;

            async Task Restore()
            {
                if (await RouteableState.RestoreSnapshotsAsync(router, kept))
                    RouteableMenu.Moved(provider, router);
                else
                    SaveNow();   // what was kept cannot be shown: the page that is there replaces it, or the same failure comes back at every start
            }

            if (provider.GetService<IRouteableDispatcher>() is { } dispatcher && !dispatcher.CheckAccess())
                await dispatcher.InvokeAsync(Restore);
            else
                await Restore();
        }
        catch (Exception ex)
        {
            RouteableErrors.Report<RouteableHistoryKeeper>(provider, ex, "Restore");
        }
        finally
        {
            // A keeper that was disposed while it restored stays off: it must not write the history again.
            if (!_disposed)
                _active = true;
            RouteableStartup.EndRestore(router);
            try
            {
                // The history belongs to the thread of the interface: read it there, as the route changes that save it do.
                if (_disposed)
                {
                    // nothing is written by a keeper that is over
                }
                else if (provider.GetService<IRouteableDispatcher>() is { } dispatcher && !dispatcher.CheckAccess())
                    await dispatcher.InvokeAsync(SaveNow);
                else
                    SaveNow();
            }
            catch (Exception ex)
            {
                RouteableErrors.Report<RouteableHistoryKeeper>(provider, ex, "Save");
            }
        }
    }

    private void OnRouted(object? sender, RouteableRoutedEventArgs e)
    {
        if (_active && e.Succeeded)
            SaveNow();
    }

    // An erased history raises no route change: without this, the pages that were erased would come back at the next start.
    private void OnHistoryChanged(object? sender, EventArgs e)
    {
        if (_active && _router is { } router && router.History.Count == 0)
            SaveNow(allowEmpty: true);
    }

    /// <summary>Writes the history now, without waiting for the next route change (the application is going to sleep). Does nothing before <see cref="Start"/> has put the previous history back.</summary>
    public void Flush()
    {
        if (_active) SaveNow();
    }

    // A history that cannot be unprotected (another key, a file that was changed) is left out: the application starts with no history.
    private string? Unprotect(string? kept)
    {
        if (protector is null || string.IsNullOrEmpty(kept))
            return kept;
        return protector.Unprotect(kept);
    }

    private void SaveNow() => SaveNow(allowEmpty: false);

    private void SaveNow(bool allowEmpty)
    {
        try
        {
            if (_router is not { } router)
                return;

            // Two saves may overlap (a route change and a Flush from another thread): the one that read the history last must be the one that is
            // written last. The history is read outside the lock (a view model saves its state there: user code), so each read takes a number
            // first, and a read that is older than what is already written is dropped.
            var ticket = Interlocked.Increment(ref _ticket);
            var snapshots = RouteableState.CaptureSnapshots(router);
            // An empty history is written only when it was erased: the first page of a new run is not yet there when the keeper starts.
            if (snapshots.Count == 0 && !allowEmpty)
                return;

            var text = snapshots.Count == 0 ? string.Empty : RouteableState.Serialize(snapshots);
            lock (_gate)
            {
                if (ticket < _savedTicket)
                    return;
                // Nothing to write, but this read is now the newest one that was looked at: an older read, that differs, must not be written after it.
                if (text == _last)
                {
                    _savedTicket = ticket;
                    return;
                }
                store.Save(protector is null || text.Length == 0 ? text : protector.Protect(text));
                _last = text;
                _savedTicket = ticket;
            }
        }
        catch (Exception ex)
        {
            // A history that cannot be written (a read-only disk) must not fail a route change.
            RouteableErrors.Report<RouteableHistoryKeeper>(provider, ex, "Save");
        }
    }

    /// <summary>Stops keeping the history.</summary>
    public void Dispose()
    {
        _disposed = true;
        _active = false;
        try
        {
            _stopped.Cancel();
            _stopped.Dispose();
        }
        catch (ObjectDisposedException) { }   // already stopped
        if (_router is { } router)
            router.Routed -= OnRouted;
        if (_router is { } erased)
            erased.History.Changed -= OnHistoryChanged;
    }
}

/// <summary>Registration of <see cref="RouteableHistoryKeeper"/>.</summary>
public static class RouteableHistoryExtensions
{
    /// <summary>
    /// Keeps the history of the router between two runs of the application (see <see cref="RouteableHistoryKeeper"/>). Call it with the other
    /// registrations of the application; the host starts it when it builds its container.
    /// </summary>
    /// <param name="services">The service collection of the application.</param>
    /// <param name="path">The file that holds the history; <see langword="null"/> takes <see cref="FileRouteableHistoryStore"/>'s default.</param>
    /// <param name="protector">Encrypts what is kept (see <see cref="AesRouteableHistoryProtector"/>); <see langword="null"/> keeps plain text, which is only fit for histories that hold no private data.</param>
    public static IServiceCollection AddRouteableHistory(this IServiceCollection services, string? path = null, IRouteableHistoryProtector? protector = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        if (protector is not null)
            services.TryAddSingleton(protector);
        services.TryAddSingleton<IRouteableHistoryStore>(_ => new FileRouteableHistoryStore(path));
        services.TryAddSingleton(sp => new RouteableHistoryKeeper(sp, sp.GetRequiredService<IRouteableHistoryStore>(), sp.GetService<IRouteableHistoryProtector>()));
        return services;
    }

    /// <summary>Starts the keeper when <see cref="AddRouteableHistory"/> registered it; does nothing otherwise. A host calls it once its container is built.</summary>
    public static IServiceProvider StartRouteableHistory(this IServiceProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);
        provider.GetService<RouteableHistoryKeeper>()?.Start();
        return provider;
    }
}
