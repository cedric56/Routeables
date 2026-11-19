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
/// a draft of a form, an identifier): it is written as plain text unless a protector is given to <see cref="RouteableHistoryExtensions.AddRouteableHistory(IServiceCollection, string?, IRouteableHistoryProtector?)"/>.
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

        // One user's history is not for the other users of the machine. On Unix, a newly created folder is for its owner (the mode of an existing
        // folder is whoever created it's choice), and the file is created with its final mode, so there is no moment when it is wider, as there
        // would be if the mode were set after the write. The umask can only remove rights, never add them.
        var folder = System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(Path));
        if (!string.IsNullOrEmpty(folder))
        {
            // The platform test is written in the condition itself, which is what the platform analyzer (CA1416) recognizes as a guard.
            if (!OperatingSystem.IsWindows() && !OperatingSystem.IsBrowser())
                Directory.CreateDirectory(folder, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            else
                Directory.CreateDirectory(folder);
        }

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
/// Switched on by <see cref="RouteableHistoryExtensions.AddRouteableHistory(IServiceCollection, string?)"/>; the host starts it when it builds its container.
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
    private long _ticket; // taken before a history is read
    private long _savedTicket; // the ticket of the history written last

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

        // Write nothing before the pages are back: the first page of the new run must not replace the history that is waiting.
        if (kept.Count == 0)
        {
            _active = true;
            return;
        }

        // The addresses that opened the application (RouteableLinkInbox) wait until the pages are back: restoring starts by erasing the history.
        RouteableStartup.BeginRestore(router);
        RouteableHelper.FireAndForget(() => Task.Run(() => RestoreAsync(router, kept)), provider, "RouteableHistoryKeeper.Restore");
    }

    // How long one wait for the first page lasts. The wait restarts until the keeper is disposed. Changed by tests only.
    internal static TimeSpan FirstPageSlice = TimeSpan.FromSeconds(30);

    private async Task RestoreAsync(IRouter router, IReadOnlyList<RouteableSnapshot> kept)
    {
        // The keeper may be disposed before this runs: its source is disposed with it, and a disposed source gives no token.
        CancellationToken stop;
        try { stop = _stopped.Token; }
        catch (ObjectDisposedException)
        {
            // The restore was announced (BeginRestore): the addresses waiting for it must not wait forever.
            RouteableStartup.EndRestore(router);
            return;
        }

        try
        {
            // Restore the saved history once the first page is shown, however late that is (a sign-in window, a splash screen outside the router).
            // Giving up after a delay would let the next navigation overwrite it with a single page.
            while (!await RouteableLinkInbox.WaitForFirstPageAsync(router, stop, FirstPageSlice))
            {
                if (_disposed || stop.IsCancellationRequested)
                    return;
            }

            if (_disposed)
                return;

            async Task Restore()
            {
                if (await RouteableState.RestoreSnapshotsAsync(router, kept))
                    RouteableMenu.Moved(provider, router);
                else
                    SaveNow(); // what was kept cannot be shown: the page that is there replaces it, or the same failure would come back at every start
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
            // A keeper disposed while it restored stays off: it must not write the history again.
            if (!_disposed)
                _active = true;
            RouteableStartup.EndRestore(router);
            try
            {
                // The history belongs to the UI thread: read it there, as the navigations that save it do.
                if (_disposed)
                {
                    // A keeper that is over writes nothing.
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

    // An erased history raises no navigation: without this, the erased pages would come back at the next start.
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

    // A history that cannot be unprotected (another key, a modified file) is left out: the application starts with no history.
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

            // Two saves can overlap (a navigation and a Flush from another thread): the one that read the history last must be written last. The history
            // is read outside the lock (a view model saves its state there, which is user code), so each read first takes a number, and a read older than
            // what is already written is dropped.
            var ticket = Interlocked.Increment(ref _ticket);
            var snapshots = RouteableState.CaptureSnapshots(router);
            // An empty history is written only when it was erased: the first page of a new run is not there yet when the keeper starts.
            if (snapshots.Count == 0 && !allowEmpty)
                return;

            var text = snapshots.Count == 0 ? string.Empty : RouteableState.Serialize(snapshots);
            lock (_gate)
            {
                if (ticket < _savedTicket)
                    return;
                // Nothing to write, but this read is now the newest one looked at: an older, different read must not be written after it.
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
            // A history that cannot be written (a read-only disk) must not make a navigation fail.
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
        catch (ObjectDisposedException) { } // already stopped
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
    public static IServiceCollection AddRouteableHistory(this IServiceCollection services, string? path = null)
        => services.AddRouteableHistory(path, protector: null);

    /// <summary>
    /// Keeps the history of the router between two runs of the application, encrypted by <paramref name="protector"/>
    /// (see <see cref="AesRouteableHistoryProtector"/>).
    /// </summary>
    /// <param name="services">The service collection of the application.</param>
    /// <param name="path">The file that holds the history; <see langword="null"/> takes <see cref="FileRouteableHistoryStore"/>'s default.</param>
    /// <param name="protector">Encrypts what is kept; <see langword="null"/> keeps plain text, which is only fit for histories that hold no private data.</param>
    public static IServiceCollection AddRouteableHistory(this IServiceCollection services, string? path, IRouteableHistoryProtector? protector)
    {
        ArgumentNullException.ThrowIfNull(services);
        if (protector is not null)
            services.TryAddSingleton(protector);
        services.TryAddSingleton<IRouteableHistoryStore>(_ => new FileRouteableHistoryStore(path));
        services.TryAddSingleton(sp => new RouteableHistoryKeeper(sp, sp.GetRequiredService<IRouteableHistoryStore>(), sp.GetService<IRouteableHistoryProtector>()));
        return services;
    }

    /// <summary>Starts the keeper when <see cref="AddRouteableHistory(IServiceCollection, string?)"/> registered it; does nothing otherwise. A host calls it once its container is built.</summary>
    public static IServiceProvider StartRouteableHistory(this IServiceProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);
        provider.GetService<RouteableHistoryKeeper>()?.Start();
        return provider;
    }
}
