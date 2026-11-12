namespace Routeables.Native;

/// <summary>What <see cref="NativeDialogs.DisplayThreeButtonsAsync{T}"/> did: the dialog was <see cref="Shown"/> and the user chose
/// <see cref="Value"/>, or the platform has no native dialog for this content and nothing was shown.</summary>
/// <typeparam name="T">The type of the result of a button.</typeparam>
/// <param name="Shown">Whether a native dialog was shown.</param>
/// <param name="Value">The result of the button chosen; <see langword="default"/> when nothing was shown.</param>
public readonly record struct NativeDialogResult<T>(bool Shown, T? Value);

/// <summary>
/// The dialogs of the platform, with no UI framework: an Android alert dialog, a Windows App SDK <c>ContentDialog</c>, an iOS / Mac Catalyst
/// alert controller or form sheet. Used by .NET MAUI, WinUI 3 and Uno Platform, which each fall back to a dialog of their own where this one
/// has nothing to show (the plain <c>net10.0</c> target, or a content that is not native to the platform).
/// </summary>
public static partial class NativeDialogs
{
    /// <summary>Whether this build has native dialogs: Android, iOS, Mac Catalyst and Windows do, the plain <c>net10.0</c> target does not.</summary>
    public static bool IsSupported =>
#if ANDROID || IOS || MACCATALYST || WINDOWS
        true;
#else
        false;
#endif

#if WINDOWS
    // WinUI shows one ContentDialog at a time per window and a second ShowAsync throws. The gate is one for the application, not one for each window:
    // with several windows a dialog waits for the one of another window, which is slower but never fails.
    private static readonly SemaphoreSlim WindowsDialogGate = new(1, 1);
#endif

    /// <summary>
    /// Runs <paramref name="show"/> once the dialog that is open on Windows is closed, so that two dialogs are never shown together (WinUI throws for
    /// the second one). Elsewhere the platform queues or stacks the dialogs itself: <paramref name="show"/> runs at once. The three-button dialog of
    /// this class is behind the same gate: do not call it from <paramref name="show"/>.
    /// </summary>
    /// <typeparam name="T">The result of the dialog.</typeparam>
    /// <param name="show">Shows the dialog.</param>
    public static async Task<T> OneAtATimeAsync<T>(Func<Task<T>> show)
    {
        ArgumentNullException.ThrowIfNull(show);
#if WINDOWS
        await WindowsDialogGate.WaitAsync();
        try { return await show(); }
        finally { WindowsDialogGate.Release(); }
#else
        return await show();
#endif
    }
}
