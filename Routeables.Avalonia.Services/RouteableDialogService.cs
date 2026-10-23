using Avalonia.Threading;
using Routeables.Services;

namespace Routeables.Avalonia.Services;

/// <summary>Message boxes, drawn by an <see cref="IMessageBoxService"/>. The indexes returned follow the order of the buttons: Yes, No, then Cancel or Abort; -1 when the dialog was dismissed.</summary>
public class RouteableDialogService(IMessageBoxService service) : RouteableDialogServiceBase
{
    /// <inheritdoc />
    public override Task<int> ShowMessageAsync(string message, string title, string buttonText)
    {
        return Dispatcher.UIThread.Invoke(async () =>
        {
            var splits = RouteableButtons.Split(buttonText);
            var result = await service.ShowMessageBoxAsync(title, message, buttonText, TopLevelStateManager.Default.GetActive());
            var index = result is not null ? IndexOf(splits, result) : -1;
            return index;
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
