using Routeables.Services;

namespace Routeables.Wpf.Services;

/// <summary>Shows a message with buttons for <c>IRouteableDialogService</c>, through the <see cref="IMessageBoxService"/>.</summary>
public class RouteableDialogService(IMessageBoxService service) : RouteableDialogServiceBase
{
    /// <inheritdoc />
    public override Task<int> ShowMessageAsync(string message, string title, string buttonText)
    {
        return UiThread.InvokeAsync(async () =>
        {
            var splits = RouteableButtons.Split(buttonText);
            var result = await service.ShowMessageBoxAsync(title, message, buttonText, RouteableWindows.Active);
            return result is not null ? IndexOf(splits, result) : -1;
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
