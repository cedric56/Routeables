using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Routeables.Abstraction;
using Routeables.Abstraction.Errors;
using Routeables.Abstraction.Routing;

namespace Routeables.Services;

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
/// never in the source code or next to the file. AES-GCM is not available in a browser (Blazor WebAssembly): there, reading gives no history and
/// writing is reported as a failure to save, and the history of a Blazor application is kept by <c>PersistentComponentState</c> instead.
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
        catch (Exception ex) when (ex is FormatException or System.Security.Cryptography.CryptographicException or PlatformNotSupportedException)
        {
            return null;
        }
    }
}
