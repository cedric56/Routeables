using Routeables.Abstraction;
using System.Windows.Input;
using Routeables.Services;

namespace Routeables.Maui.Services;

/// <summary>
/// The content of the dialogs, dressed by <see cref="RouteableNativeDialogLook"/>. It does not know how it is shown: the default
/// services show it in a modal page (<see cref="RouteablePopupPage{TResult}"/>), the CommunityToolkit reshape in a toolkit popup.
/// </summary>
public static class RouteableDialogContents
{
    /// <summary>A content dialog: title, content and up to three buttons (primary, secondary, close).</summary>
    /// <param name="p">The dialog's configuration.</param>
    /// <param name="session">What the buttons do and when the dialog may close; it closes the surface once a button decided.</param>
    public static Border ForContentDialog(RouteableContentDialog p, RouteableContentDialogSession session)
    {
        ArgumentNullException.ThrowIfNull(p);
        ArgumentNullException.ThrowIfNull(session);

        View body = p.Content is View view
            ? view
            : RouteableNativeDialogLook.Text(p.Content?.ToString() ?? string.Empty, secondary: true);

        // WinUI order: primary, secondary, close.
        return RouteableNativeDialogLook.Build(p.Title, body, session.Buttons.Select(ToButton).ToList());
    }

    // A button of the session, drawn once: it is enabled when its command can execute when the dialog opens.
    private static DialogButton ToButton(RouteableDialogButton button) => new(
        button.Text,
        button.IsDefault,
        button.IsEnabled && (button.Command is null || button.Command.CanExecute(button.CommandParameter)),
        () =>
        {
            button.Press();
            return Task.CompletedTask;
        });

    /// <summary>
    /// A task dialog: header, subheader, content, optional progress, expandable footer, up to 3 buttons,
    /// dressed like the platform's own dialog.
    /// </summary>
    /// <param name="p">The dialog's configuration.</param>
    /// <param name="session">The buttons, one per result asked (the first is the primary one), and when the dialog may close.</param>
    public static Border ForTaskDialog(RouteableTaskDialog p, RouteableTaskDialogSession session)
    {
        ArgumentNullException.ThrowIfNull(p);
        ArgumentNullException.ThrowIfNull(session);

        var body = new VerticalStackLayout { Spacing = 12 };

        // ----- Header: icon + header text -----
        // Without a title the header is the dialog's title instead.
        var title = string.IsNullOrWhiteSpace(p.Title) ? p.Header : p.Title;
        var showHeader = !string.IsNullOrWhiteSpace(p.Title) && p.Header is not null;
        if (showHeader || p.IconSource is not null)
        {
            var headerHost = new HorizontalStackLayout { Spacing = 12 };
            if (p.IconSource is ImageSource icon)
                headerHost.Add(new Image { WidthRequest = 32, HeightRequest = 32, Source = icon, VerticalOptions = LayoutOptions.Center, AutomationId = "IconElement" });
            if (showHeader)
            {
                var headerText = RouteableNativeDialogLook.Text(p.Header, 16, FontAttributes.Bold);
                headerText.VerticalOptions = LayoutOptions.Center;
                headerHost.Add(headerText);
            }
            body.Add(headerHost);
        }

        // ----- Subheader -----
        if (!string.IsNullOrWhiteSpace(p.SubHeader))
            body.Add(RouteableNativeDialogLook.Text(p.SubHeader));

        // ----- Content + progress + footer -----
        if (p.Content is View contentView)
            body.Add(contentView);
        else if (p.Content is not null)
            body.Add(RouteableNativeDialogLook.Text(p.Content.ToString(), secondary: true));

        if (p.ShowProgressBar)
            body.Add(CreateIndeterminateBar());

        if (p.ShowsFooter)
        {
            var footerHost = new VerticalStackLayout
            {
                Spacing = 12,
                IsVisible = p.FooterStartsExpanded
            };

            var separator = new BoxView { HeightRequest = 1, Opacity = 0.25 };
            separator.SetAppThemeColor(BoxView.ColorProperty, Colors.Gray, Colors.DimGray);
            footerHost.Children.Add(separator);

            if (p.Footer is View footerView)
                footerHost.Children.Add(footerView);
            else
                footerHost.Children.Add(RouteableNativeDialogLook.Text(p.Footer?.ToString(), 12, secondary: true));

            if (p.IsFooterCollapsible)
            {
                var moreDetailsButton = new Button
                {
                    Text = footerHost.IsVisible ? "Fewer Details" : "More Details",
                    BorderWidth = 0,
                    Padding = 0,
                    HorizontalOptions = LayoutOptions.Start,
                    MinimumHeightRequest = 0,
                    MinimumWidthRequest = 0,
                    FontSize = 14,
                    BackgroundColor = Colors.Transparent
                };
                RouteableNativeDialogLook.ApplyLinkColor(moreDetailsButton);

                moreDetailsButton.Clicked += (_, _) =>
                {
                    footerHost.IsVisible = !footerHost.IsVisible;
                    moreDetailsButton.Text = footerHost.IsVisible ? "Fewer Details" : "More Details";
                };

                body.Add(moreDetailsButton);
            }

            body.Add(footerHost);
        }

        var scroll = new ScrollView
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Never,
            Content = body,
            MaximumHeightRequest = 420
        };

        // ----- Buttons (up to 3): the first result is the primary one -----
        return RouteableNativeDialogLook.Build(title, scroll, session.Buttons.Select(ToButton).ToList());
    }

    /// <summary>
    /// A modal: the title and the view of the view model, and its first three commands as buttons (the first is the primary one).
    /// </summary>
    /// <param name="viewModel">The view model whose <see cref="ICloseableViewModel{TResult}.Commands"/> become the buttons.</param>
    /// <param name="view">The view shown in the dialog.</param>
    public static Border ForModal<TResult>(ICloseableViewModel<TResult> viewModel, View view)
    {
        var buttons = new List<DialogButton>();
        var commands = viewModel.Commands;
        for (var i = 0; i < Math.Min(3, commands?.Count ?? 0); i++)
        {
            var named = commands![i];
            if (named is null || string.IsNullOrWhiteSpace(named.Name))
                continue;

            buttons.Add(new DialogButton(named.Name, i == 0, true, Command: named.Command));
        }

        // Keep MAUI view hierarchy (no ToPlatform)
        return RouteableNativeDialogLook.Build(
            viewModel.Title,
            new ScrollView { Content = view, HorizontalScrollBarVisibility = ScrollBarVisibility.Never, MaximumHeightRequest = 420 },
            buttons);
    }

    // A MAUI ProgressBar has no indeterminate mode and would stay empty: fill and empty it
    // while the dialog is on screen, the way an indeterminate bar reads.
    private static ProgressBar CreateIndeterminateBar()
    {
        const string name = "RouteablesIndeterminate";
        var bar = new ProgressBar { Progress = 0, HorizontalOptions = LayoutOptions.Fill };
        RouteableNativeDialogLook.ApplyAccentColor(bar);

        bar.Loaded += (_, _) =>
        {
            var animation = new Animation
            {
                { 0, 0.5, new Animation(v => bar.Progress = v, 0, 1, Easing.CubicInOut) },
                { 0.5, 1, new Animation(v => bar.Progress = v, 1, 0, Easing.CubicInOut) }
            };
            animation.Commit(bar, name, length: 2400, repeat: () => true);
        };
        bar.Unloaded += (_, _) => bar.AbortAnimation(name);
        return bar;
    }
}
