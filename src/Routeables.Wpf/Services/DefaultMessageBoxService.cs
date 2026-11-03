using System.Windows;
using System.Windows.Controls;
using Routeables.Services;

namespace Routeables.Wpf.Services;

/// <summary>
/// Shows the dialogs of the host in owned windows, with plain WPF controls. What each button returns, and whether a
/// dismissal can be vetoed, is decided by the sessions of <c>Routeables.Services</c>, shared by every host.
/// </summary>
/// <param name="icon">The icon of the dialog windows (a pack URI or a file); the one of the owner window when not set.</param>
public class DefaultMessageBoxService(string? icon = null) : IMessageBoxService
{
    /// <inheritdoc />
    public async Task<string?> ShowMessageBoxAsync(string title, string message, string buttons, Window? owner = null)
    {
        var window = Resolve(owner);
        var labels = RouteableButtons.Split(buttons ?? string.Empty).ToList();
        if (labels.Count == 0)
            labels.Add("Ok");

        string? result = null;
        var handle = new RouteableDialogHandle { TryDismiss = () => true };

        var buttonControls = labels
            .Select((label, index) => DialogCard.CreateButton(label, () =>
            {
                result = label;
                handle.Complete();
            }, isDefault: index == 0))
            .ToList();

        var body = DialogCard.Scrollable(DialogCard.ToBody(message ?? string.Empty)!);
        var card = DialogCard.Create([body], buttonControls);

        await DialogPresenter.ShowAsync(window, icon, title, card, handle);
        return result;
    }

    /// <inheritdoc />
    public async Task<RouteableContentDialogResult> ShowAsync(RouteableContentDialog @params, Window? owner = null)
    {
        ArgumentNullException.ThrowIfNull(@params);

        var window = Resolve(owner);
        var handle = new RouteableDialogHandle();
        var session = new RouteableContentDialogSession(@params, handle.Complete);

        // Lets the caller close the dialog later, from any thread.
        session.AllowCloseFromCode(UiThread.Post);
        handle.TryDismiss = session.TryDismiss;
        handle.Opened = session.Opened;

        var cleanup = new List<Action>();
        var buttons = session.Buttons
            .Select(b => DialogCard.CreateCommandButton(b.Text, b.IsEnabled, b.Command, b.CommandParameter, () => b.Press(), cleanup, b.IsDefault))
            .ToList();

        var body = DialogCard.ToBody(@params.Content);
        var scroller = body is null ? null : DialogCard.Scrollable(body);
        var card = DialogCard.Create([scroller], buttons);
        if (@params.FullSizeDesired && card is FrameworkElement element)
        {
            element.MaxWidth = double.PositiveInfinity;
            element.MinWidth = 640;
        }

        session.Opening();
        try
        {
            await DialogPresenter.ShowAsync(window, icon, @params.Title, card, handle);
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
    public async Task<RouteableTaskDialogResult> ShowAsync(RouteableTaskDialog @params, Window? owner = null, params RouteableTaskDialogResult[] results)
    {
        ArgumentNullException.ThrowIfNull(@params);

        var window = Resolve(owner);
        var handle = new RouteableDialogHandle();
        var session = new RouteableTaskDialogSession(@params, results, handle.Complete);

        handle.TryDismiss = session.TryDismiss;
        handle.Opened = session.Opened;

        var buttons = session.Buttons
            .Select(b => DialogCard.CreateButton(b.Text, () => b.Press(), isDefault: b.IsDefault))
            .ToList();

        var parts = new List<UIElement?>();
        var release = new List<Action>();

        var texts = new StackPanel();
        if (!string.IsNullOrWhiteSpace(@params.Header))
        {
            texts.Children.Add(new TextBlock
            {
                Text = @params.Header,
                FontSize = 20,
                FontWeight = FontWeights.SemiBold,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 4)
            });
        }

        if (!string.IsNullOrWhiteSpace(@params.SubHeader))
            texts.Children.Add(new TextBlock { Text = @params.SubHeader, TextWrapping = TextWrapping.Wrap, Opacity = 0.8 });

        var iconSource = DialogCard.ToIcon(@params.IconSource);
        if (iconSource is not null)
        {
            if (iconSource is FrameworkElement iconElement)
                iconElement.Margin = new Thickness(0, 0, 12, 0);

            var header = new StackPanel { Orientation = Orientation.Horizontal };
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
            parts.Add(new ProgressBar { IsIndeterminate = true, Height = 8 });

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
            await DialogPresenter.ShowAsync(window, icon, @params.Title, card, handle);
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
    public bool IsDisplayable(Window owner)
    {
        ArgumentNullException.ThrowIfNull(owner);

        return owner.IsVisible;
    }

    private static Window Resolve(Window? owner) =>
        owner
        ?? RouteableWindows.Active
        ?? throw new InvalidOperationException("No active window is available to show the dialog. Open it with Application.OpenMainWindow or attach it with Window.AttachRouteables.");
}
