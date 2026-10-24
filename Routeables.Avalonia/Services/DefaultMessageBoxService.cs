using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Routeables.Abstraction;
using Routeables.Services;
using Routeables.Avalonia;

namespace Routeables.Avalonia.Services;

/// <summary>
/// The default <see cref="IMessageBoxService"/>: message boxes, content dialogs and task dialogs drawn with plain
/// Avalonia controls, without MessageBox.Avalonia or FluentAvalonia. On desktop they are modal windows owned by the
/// active <see cref="TopLevel"/>; where there is a single view (Android, iOS, Browser) they are drawn in the
/// top level's overlay layer.
/// </summary>
public class DefaultMessageBoxService(string icon) : IMessageBoxService
{
    /// <inheritdoc />
    public async Task<string?> ShowMessageBoxAsync(string title, string message, string buttons, TopLevel? topLevel = null)
    {
        var owner = Resolve(topLevel);
        var labels = (buttons ?? string.Empty).SplitOnCapitals().ToList();
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

        await DialogPresenter.ShowAsync(owner, icon, title, card, handle);
        return result;
    }

    /// <inheritdoc />
    public async Task<RouteableContentDialogResult> ShowAsync(RouteableContentDialog @params, TopLevel? topLevel = null)
    {
        ArgumentNullException.ThrowIfNull(@params);

        var owner = Resolve(topLevel);
        var handle = new RouteableDialogHandle();
        var session = new RouteableContentDialogSession(@params, handle.Complete);

        // Lets the caller close the dialog later, from any thread.
        session.AllowCloseFromCode(action =>
        {
            if (Dispatcher.UIThread.CheckAccess()) action();
            else Dispatcher.UIThread.Post(action);
        });
        handle.TryDismiss = session.TryDismiss;
        handle.Opened = session.Opened;

        var cleanup = new List<Action>();
        var buttons = session.Buttons
            .Select(b => DialogCard.CreateCommandButton(b.Text, b.IsEnabled, b.Command, b.CommandParameter, () => b.Press(), cleanup, b.IsDefault))
            .ToList();

        var body = DialogCard.ToBody(@params.Content);
        var scroller = body is null ? null : DialogCard.Scrollable(body);
        var card = DialogCard.Create([scroller], buttons);
        if (@params.FullSizeDesired)
        {
            card.MaxWidth = double.PositiveInfinity;
            card.MinWidth = 640;
        }

        session.Opening();
        try
        {
            await DialogPresenter.ShowAsync(owner, icon, @params.Title, card, handle);
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
    public async Task<RouteableTaskDialogResult> ShowAsync(RouteableTaskDialog @params, TopLevel? topLevel = null, params RouteableTaskDialogResult[] results)
    {
        ArgumentNullException.ThrowIfNull(@params);

        var owner = Resolve(topLevel);
        var handle = new RouteableDialogHandle();
        var session = new RouteableTaskDialogSession(@params, results, handle.Complete);

        handle.TryDismiss = session.TryDismiss;
        handle.Opened = session.Opened;

        var buttons = session.Buttons
            .Select((b) => DialogCard.CreateButton(b.Text, () => b.Press(), isDefault: b.IsDefault))
            .ToList();

        var parts = new List<Control?>();
        var release = new List<Action>();

        var texts = new StackPanel { Spacing = 4 };
        if (!string.IsNullOrWhiteSpace(@params.Header))
        {
            texts.Children.Add(new TextBlock
            {
                Text = @params.Header,
                FontSize = 20,
                FontWeight = FontWeight.SemiBold,
                TextWrapping = TextWrapping.Wrap
            });
        }

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
            await DialogPresenter.ShowAsync(owner, icon, @params.Title, card, handle);
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
    public bool IsDisplayable(TopLevel topLevel)
    {
        ArgumentNullException.ThrowIfNull(topLevel);

        return topLevel.IsVisible;
    }

    private static TopLevel Resolve(TopLevel? topLevel) =>
        topLevel
        ?? TopLevelStateManager.Default.GetActive(throwOnNull: true)
        ?? throw new InvalidOperationException("No active top level is available to show the dialog.");
}