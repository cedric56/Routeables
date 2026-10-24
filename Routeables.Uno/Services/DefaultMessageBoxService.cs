using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Routeables.Abstraction;
using Routeables.Services;

namespace Routeables.Uno.Services;

/// <summary>Draws the dialogs with <c>ContentDialog</c>, in the window the user is working in.</summary>
public class DefaultMessageBoxService : IMessageBoxService
{
    /// <inheritdoc />
    public async Task<string?> ShowMessageBoxAsync(string title, string message, string buttons, XamlRoot? root = null)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(buttons);

        var owner = Resolve(root);
        string? chosen = null;
        var handle = new RouteableDialogHandle();

        var texts = RouteableButtons.Split(buttons);
        var controls = texts
            .Select((text, index) => DialogCard.CreateButton(text, () =>
            {
                chosen = text;
                handle.Complete();
                return true;
            }, isDefault: index == 0))
            .ToList();

        // Escape leaves without a choice.
        handle.TryDismiss = () => true;

        var card = DialogCard.Create([DialogCard.ToBody(message)], controls);
        await DialogPresenter.ShowAsync(owner, title, card, handle);
        return chosen;
    }

    /// <inheritdoc />
    public async Task<RouteableContentDialogResult> ShowAsync(RouteableContentDialog @params, XamlRoot? root = null)
    {
        ArgumentNullException.ThrowIfNull(@params);

        var owner = Resolve(root);
        var handle = new RouteableDialogHandle();
        var session = new RouteableContentDialogSession(@params, handle.Complete);

        // Lets the caller close the dialog later, from any thread.
        session.AllowCloseFromCode(action =>
        {
            if (RouteableWindows.ActiveRoot?.DispatcherQueue is { HasThreadAccess: false } queue)
                queue.TryEnqueue(() => action());
            else
                action();
        });
        handle.TryDismiss = session.TryDismiss;
        handle.Opened = session.Opened;

        var cleanup = new List<Action>();
        var buttons = session.Buttons
            .Select(b => DialogCard.CreateCommandButton(b.Text, b.IsEnabled, b.Command, b.CommandParameter, b.Press, cleanup, b.IsDefault))
            .ToList();

        var body = DialogCard.ToBody(@params.Content);
        var scroller = body is null ? null : DialogCard.Scrollable(body);
        var card = DialogCard.Create([scroller], buttons);

        session.Opening();
        try
        {
            await DialogPresenter.ShowAsync(owner, @params.Title, card, handle, @params.FullSizeDesired);
        }
        finally
        {
            // The caller's control (often a view) must not keep this dialog as its parent, or it could not be shown again.
            foreach (var action in cleanup) action();
            if (scroller is not null) scroller.Content = null;
            session.Release();
        }

        return session.Result;
    }

    /// <inheritdoc />
    public async Task<RouteableTaskDialogResult> ShowAsync(RouteableTaskDialog @params, XamlRoot? root = null, params RouteableTaskDialogResult[] results)
    {
        ArgumentNullException.ThrowIfNull(@params);

        var owner = Resolve(root);
        var handle = new RouteableDialogHandle();
        var session = new RouteableTaskDialogSession(@params, results, handle.Complete);

        handle.TryDismiss = session.TryDismiss;
        handle.Opened = session.Opened;

        var buttons = session.Buttons
            .Select(b => DialogCard.CreateButton(b.Text, b.Press, isDefault: b.IsDefault))
            .ToList();

        var parts = new List<UIElement?>();
        var release = new List<Action>();

        var texts = new StackPanel { Spacing = 4 };
        if (!string.IsNullOrWhiteSpace(@params.Header))
            texts.Children.Add(DialogCard.Header(@params.Header));

        if (!string.IsNullOrWhiteSpace(@params.SubHeader))
            texts.Children.Add(new TextBlock { Text = @params.SubHeader, TextWrapping = TextWrapping.Wrap, Opacity = 0.8 });

        var iconSource = DialogCard.ToIcon(@params.IconSource);
        if (iconSource is not null)
        {
            var header = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
            header.Children.Add(iconSource);
            header.Children.Add(texts);
            parts.Add(header);
            release.Add(header.Children.Clear);
        }
        else if (texts.Children.Count > 0)
        {
            parts.Add(texts);
        }

        var body = DialogCard.ToBody(@params.Content);
        var scroller = body is null ? null : DialogCard.Scrollable(body);
        if (scroller is not null)
            parts.Add(scroller);

        if (@params.ShowProgressBar)
            parts.Add(new ProgressBar { IsIndeterminate = true });

        var footer = DialogCard.ToBody(@params.Footer);
        Expander? expander = null;
        if (footer is not null && @params.ShowsFooter)
        {
            if (@params.IsFooterCollapsible)
            {
                expander = new Expander { Header = "Details", IsExpanded = @params.IsFooterExpanded, Content = footer };
                parts.Add(expander);
            }
            else
            {
                var holder = new ContentControl { Content = footer };
                parts.Add(holder);
                release.Add(() => holder.Content = null);
            }
        }
        if (expander is { } footerExpander) release.Add(() => footerExpander.Content = null);
        if (scroller is { } bodyScroller) release.Add(() => bodyScroller.Content = null);

        var card = DialogCard.Create(parts, buttons);

        session.Opening();
        try
        {
            await DialogPresenter.ShowAsync(owner, @params.Title, card, handle);
        }
        finally
        {
            // The caller's controls must not keep this dialog as their parent, or they could not be shown again.
            foreach (var action in release) action();
            session.Release();
        }

        return session.Result;
    }

    /// <inheritdoc />
    public bool IsDisplayable(XamlRoot root)
    {
        ArgumentNullException.ThrowIfNull(root);

        return root.Content?.Visibility == Visibility.Visible;
    }

    private static XamlRoot Resolve(XamlRoot? root) =>
        root
        ?? RouteableWindows.ActiveXamlRoot
        ?? throw new InvalidOperationException("No active window is available to show the dialog. Open the window with Application.OpenMainWindow(...), or attach it with Window.AttachRouteables() once it has content.");
}
