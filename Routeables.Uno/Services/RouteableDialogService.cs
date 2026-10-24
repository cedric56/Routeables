using Routeables.Services;

namespace Routeables.Uno.Services;

/// <summary>Message boxes, drawn by an <see cref="IMessageBoxService"/>. The indexes returned follow the order of the buttons: Yes, No, then Cancel or Abort; -1 when the dialog was dismissed.</summary>
public class RouteableDialogService(IMessageBoxService service) : RouteableDialogServiceBase
{
    /// <inheritdoc />
    public override Task<int> ShowMessageAsync(string message, string title, string buttonText)
    {
        return UiThread.RunAsync(async () =>
        {
            var words = RouteableButtons.Split(buttonText);
            var result = await service.ShowMessageBoxAsync(title, message, buttonText);
            return result is not null ? IndexOf(words, result) : -1;
        });
    }

    private static int IndexOf(IReadOnlyList<string> words, string word)
    {
        for (var i = 0; i < words.Count; i++)
            if (words[i] == word)
                return i;
        return -1;
    }
}
