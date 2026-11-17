namespace Routeables;

/// <summary>
/// The few words the library writes on screen itself: the buttons of the message boxes (<c>Yes</c>, <c>No</c>, <c>Cancel</c>, <c>OK</c>...) and the title of
/// an application whose menu has no entry selected. They are English by default; an application that is translated sets them once, at start:
/// <code>
/// RouteableTexts.ButtonTranslator = word => word switch { "Yes" => "Oui", "No" => "Non", "Cancel" => "Annuler", _ => null };
/// RouteableTexts.ApplicationTitle = "Mon application";
/// </code>
/// The words that a message box <i>returns</i> stay the English ones (<c>"Yes"</c> is what <c>ShowMessageBoxAsync</c> gives back whatever the button says), so the code
/// that reads them does not change with the language.
/// </summary>
public static class RouteableTexts
{
    private static volatile string _applicationTitle = "Application";
    private static volatile Func<string, string?>? _buttonTranslator;

    /// <summary>The title shown when nothing says another one (a menu with no entry selected). <c>"Application"</c> by default; a blank value restores it.</summary>
    public static string ApplicationTitle
    {
        get => _applicationTitle;
        set => _applicationTitle = string.IsNullOrWhiteSpace(value) ? "Application" : value;
    }

    /// <summary>
    /// Gives the text of a button from its English word (<c>Yes</c>, <c>No</c>, <c>Cancel</c>, <c>Abort</c>, <c>Ok</c>, <c>Close</c>...);
    /// return <see langword="null"/> or an empty text to keep the word. <see langword="null"/> (the default) keeps every word.
    /// </summary>
    public static Func<string, string?>? ButtonTranslator
    {
        get => _buttonTranslator;
        set => _buttonTranslator = value;
    }

    /// <summary>The text to write on the button of <paramref name="word"/>: the translation when there is one, the word otherwise.</summary>
    /// <param name="word">The English word of the button, as the message box was asked for it.</param>
    public static string Button(string word)
    {
        ArgumentNullException.ThrowIfNull(word);
        string? translated = null;
        try { translated = _buttonTranslator?.Invoke(word); }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // A translator that fails must not take the dialog down: the word is shown.
            System.Diagnostics.Trace.TraceWarning($"Routeables: RouteableTexts.ButtonTranslator({word}): {ex.Message}");
        }

        return string.IsNullOrWhiteSpace(translated) ? word : translated;
    }
}
