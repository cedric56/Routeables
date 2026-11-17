// WinUI 3 and Uno are what is left when neither WPF nor Avalonia is named.
#if !WPF && !AVALONIA
#define ROUTEABLE_WINUI
#endif
#if AVALONIA
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Routeables.Services;
using Orientation = Avalonia.Layout.Orientation;
using ProgressBar = Avalonia.Controls.ProgressBar;
using Element = Avalonia.Controls.Control;
using Owner = Avalonia.Controls.TopLevel;
#elif WPF
using Routeables.Services;
using System.Windows;
using System.Windows.Controls;
using Element = System.Windows.UIElement;
using Owner = System.Windows.Window;
#else
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Routeables.Native;
using Routeables.Services;
using Element = Microsoft.UI.Xaml.UIElement;
using Owner = Microsoft.UI.Xaml.XamlRoot;
using Orientation = Microsoft.UI.Xaml.Controls.Orientation;
using ProgressBar = Microsoft.UI.Xaml.Controls.ProgressBar;
#endif

// One file for Routeables.Wpf and Routeables.Avalonia, which link it (and define WPF or AVALONIA), and for Routeables.Uno and Routeables.WinUI3. What each
// button returns is decided by the sessions of Routeables.Services; here are only the controls, which differ by a few types (the Element and the Owner of a
// dialog: a Window, a TopLevel or a XamlRoot), the space between the parts (a Margin in WPF, a Spacing elsewhere), the thread of the interface, the names of
// the methods of IMessageBoxService and, in WinUI, the buttons of the ContentDialog and the native dialogs of Routeables.Native.
#if AVALONIA
namespace Routeables.Avalonia.Services;
#elif WPF
namespace Routeables.Wpf.Services;
#elif WINUI3
namespace Routeables.WinUI3.Services;
#else
namespace Routeables.Uno.Services;
#endif

#if ROUTEABLE_WINUI
/// <summary>Draws the dialogs with <c>ContentDialog</c>, in the window the user is working in.</summary>
public class DefaultMessageBoxService : IMessageBoxService
#else
/// <summary>
/// Shows the dialogs of the host with plain controls (no third-party library): in owned windows on the desktop and, where there is a single view
/// (Android, iOS, Browser: Avalonia only), in the overlay layer of the top level. What each button returns, and whether a dismissal can be vetoed,
/// is decided by the sessions of <c>Routeables.Services</c>, shared by every host.
/// </summary>
/// <param name="icon">The icon of the dialog windows (a pack URI or a file); the one of the owner window when not set.</param>
public class DefaultMessageBoxService(string? icon = null) : IMessageBoxService
#endif
{
#if ROUTEABLE_WINUI
    /// <inheritdoc />
    public async Task<string?> ShowMessageBoxAsync(string title, string message, string buttons, XamlRoot? root = null)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(buttons);

        var window = Resolve(root);
        string? chosen = null;
        var handle = new RouteableDialogHandle();

        // The words are what the box answers; the labels are what the buttons say (RouteableTexts.ButtonTranslator).
        var texts = RouteableButtons.Split(buttons);
        var labels = RouteableButtons.Labels(buttons);

        // Three buttons are those of the dialog of the platform (Routeables.Native) where it has one: the ContentDialog of the Windows App SDK,
        // an alert on Android and iOS. Escape or a click outside gives the third button (the one that closes). Elsewhere, and for fewer buttons, the card below.
        if (texts.Count == 3 && NativeDialogs.IsSupported)
        {
            var native = await NativeDialogs.DisplayThreeButtonsAsync<string?>(
                title, message, labels[0], labels[1], labels[2], texts[0], texts[1], texts[2], window, dark: null);
            if (native.Shown)
                return native.Value;
        }

        // More than three buttons are the sheet of the platform (Routeables.Native): a ContentDialog with a column of buttons on Windows. Elsewhere, the sheet drawn below.
        if (texts.Count > 3 && NativeDialogs.IsSupported)
        {
            var sheet = await NativeDialogs.DisplaySheetAsync(title, message, [.. labels.Take(labels.Count - 1)], labels[labels.Count - 1], window, dark: null);
            if (sheet.Shown)
                return texts[sheet.Value];
        }

        var controls = texts
            .Select((text, index) => DialogCard.CreateButton(labels[index], () =>
            {
                chosen = text;
                handle.Complete();
                return true;
            }, isDefault: index == 0))
            .ToList();

        // Escape leaves without a choice.
        handle.TryDismiss = () => true;

        var card = DialogCard.Create([DialogCard.ToBody(message)], controls);
        await DialogPresenter.ShowAsync(window, title, card, handle);
        return chosen;
    }

#else
    /// <inheritdoc />
    public async Task<string?> ShowMessageBoxAsync(string title, string message, string buttons, Owner? owner = null)
    {
        var window = Resolve(owner);
        var labels = RouteableButtons.Split(buttons ?? string.Empty).ToList();
        if (labels.Count == 0)
            labels.Add("Ok");
        // The words are what the box answers; the buttons say what RouteableTexts.ButtonTranslator makes of them.

        string? result = null;
        var handle = new RouteableDialogHandle { TryDismiss = () => true };

        var buttonControls = labels
            .Select((label, index) => DialogCard.CreateButton(RouteableTexts.Button(label), () =>
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
#endif

    /// <inheritdoc />
#if AVALONIA
    public async Task<RouteableContentDialogResult> ShowContentDialogAsync(RouteableContentDialog @params, Owner? owner = null)
#elif WPF
    public async Task<RouteableContentDialogResult> ShowAsync(RouteableContentDialog @params, Owner? owner = null)
#else
    public async Task<RouteableContentDialogResult> ShowAsync(RouteableContentDialog @params, XamlRoot? root = null)
#endif
    {
        ArgumentNullException.ThrowIfNull(@params);

#if ROUTEABLE_WINUI
        var window = Resolve(root);
#else
        var window = Resolve(owner);
#endif
        var handle = new RouteableDialogHandle();
        var session = new RouteableContentDialogSession(@params, handle.Complete);

        // Lets the caller close the dialog later, from any thread.
#if AVALONIA
        session.AllowCloseFromCode(action =>
        {
            if (Dispatcher.UIThread.CheckAccess()) action();
            else Dispatcher.UIThread.Post(action);
        });
#elif WPF
        session.AllowCloseFromCode(UiThread.Post);
#else
        session.AllowCloseFromCode(action =>
        {
            if (RouteableWindows.ActiveRoot?.DispatcherQueue is { HasThreadAccess: false } queue)
                queue.TryEnqueue(() => action());
            else
                action();
        });
#endif
        handle.TryDismiss = session.TryDismiss;
        handle.Opened = session.Opened;

        var cleanup = new List<Action>();
        var buttons = session.Buttons
            .Select(b => DialogCard.CreateCommandButton(b.Text, b.IsEnabled, b.Command, b.CommandParameter, () => b.Press(), cleanup, b.IsDefault))
            .ToList();

        var body = DialogCard.ToBody(@params.Content);
        var scroller = body is null ? null : DialogCard.Scrollable(body);
        var card = DialogCard.Create([scroller], buttons);
#if AVALONIA
        if (@params.FullSizeDesired)
        {
            card.MaxWidth = double.PositiveInfinity;
            card.MinWidth = 640;
        }
#elif ROUTEABLE_WINUI
        // The size is the business of the ContentDialog: the presenter gets FullSizeDesired.
#else
        if (@params.FullSizeDesired && card is FrameworkElement element)
        {
            element.MaxWidth = double.PositiveInfinity;
            element.MinWidth = 640;
        }
#endif

        session.Opening();
        try
        {
#if ROUTEABLE_WINUI
            await DialogPresenter.ShowAsync(window, @params.Title, card, handle, @params.FullSizeDesired);
#else
            await DialogPresenter.ShowAsync(window, icon, @params.Title, card, handle);
#endif
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
#if AVALONIA
    public async Task<RouteableTaskDialogResult> ShowTaskDialogAsync(RouteableTaskDialog @params, Owner? owner = null, params RouteableTaskDialogResult[] results)
#elif WPF
    public async Task<RouteableTaskDialogResult> ShowAsync(RouteableTaskDialog @params, Owner? owner = null, params RouteableTaskDialogResult[] results)
#else
    public async Task<RouteableTaskDialogResult> ShowAsync(RouteableTaskDialog @params, XamlRoot? root = null, params RouteableTaskDialogResult[] results)
#endif
    {
        ArgumentNullException.ThrowIfNull(@params);

#if ROUTEABLE_WINUI
        var window = Resolve(root);
#else
        var window = Resolve(owner);
#endif
        var handle = new RouteableDialogHandle();
        var session = new RouteableTaskDialogSession(@params, results, handle.Complete);

        handle.TryDismiss = session.TryDismiss;
        handle.Opened = session.Opened;

        var buttons = session.Buttons
            .Select(b => DialogCard.CreateButton(b.Text, () => b.Press(), isDefault: b.IsDefault))
            .ToList();

        var parts = new List<Element?>();
        var release = new List<Action>();

#if AVALONIA || ROUTEABLE_WINUI
        var texts = new StackPanel { Spacing = 4 };
#else
        var texts = new StackPanel();
#endif
        if (!string.IsNullOrWhiteSpace(@params.Header))
        {
#if ROUTEABLE_WINUI
            texts.Children.Add(DialogCard.Header(@params.Header));
#else
            texts.Children.Add(new TextBlock
            {
                Text = @params.Header,
                FontSize = 20,
#if AVALONIA
                FontWeight = FontWeight.SemiBold,
                TextWrapping = TextWrapping.Wrap
#else
                FontWeight = FontWeights.SemiBold,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 4)
#endif
            });
#endif
        }

        if (!string.IsNullOrWhiteSpace(@params.SubHeader))
            texts.Children.Add(new TextBlock { Text = @params.SubHeader, TextWrapping = TextWrapping.Wrap, Opacity = 0.8 });

        var iconSource = DialogCard.ToIcon(@params.IconSource);
        if (iconSource is not null)
        {
#if AVALONIA || ROUTEABLE_WINUI
            var header = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
#else
            if (iconSource is FrameworkElement iconElement)
                iconElement.Margin = new Thickness(0, 0, 12, 0);

            var header = new StackPanel { Orientation = Orientation.Horizontal };
#endif
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
#if AVALONIA || ROUTEABLE_WINUI
            parts.Add(new ProgressBar { IsIndeterminate = true });
#else
            parts.Add(new ProgressBar { IsIndeterminate = true, Height = 8 });
#endif

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
#if ROUTEABLE_WINUI
            await DialogPresenter.ShowAsync(window, @params.Title, card, handle);
#else
            await DialogPresenter.ShowAsync(window, icon, @params.Title, card, handle);
#endif
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
#if ROUTEABLE_WINUI
    public bool IsDisplayable(XamlRoot root)
    {
        ArgumentNullException.ThrowIfNull(root);

        return root.Content?.Visibility == Visibility.Visible;
    }
#else
    public bool IsDisplayable(Owner owner)
    {
        ArgumentNullException.ThrowIfNull(owner);

        return owner.IsVisible;
    }
#endif

#if AVALONIA
    private static Owner Resolve(Owner? owner) =>
        owner
        ?? TopLevelStateManager.Default.GetActive(throwOnNull: true)
        ?? throw new InvalidOperationException("No active top level is available to show the dialog.");
#elif ROUTEABLE_WINUI
    private static XamlRoot Resolve(XamlRoot? root) =>
        root
        ?? RouteableWindows.ActiveXamlRoot
        ?? throw new InvalidOperationException("No active window is available to show the dialog. Open the window with Application.OpenMainWindow(...), or attach it with Window.AttachRouteables() once it has content.");
#else
    private static Owner Resolve(Owner? owner) =>
        owner
        ?? RouteableWindows.Active
        ?? throw new InvalidOperationException("No active window is available to show the dialog. Open it with Application.OpenMainWindow or attach it with Window.AttachRouteables.");
#endif
}
