using Routeables.Abstraction.Bases;
using Routeables.Abstraction.Errors;
using Routeables.Services;

namespace Routeables.Maui.Services;

#if ANDROID || WINDOWS || IOS || MACCATALYST
/// <summary>Message boxes of .NET MAUI (the dialog of the platform, with up to three buttons), shown over the current page of the window. On Windows one dialog is open at a time.</summary>
/// <param name="provider">The service provider used to create the MAUI context.</param>
/// <param name="windowContext">Where the page to show the dialog over is found.</param>
/// <param name="messageBoxService">A message box service of the application that takes over every dialog, or <see langword="null"/> to use the platform's own.</param>
#else
/// <summary>Message boxes of .NET MAUI (the dialog of the platform, with up to three buttons), shown over the current page of the window. On Windows one dialog is open at a time.</summary>
/// <param name="windowContext">Where the page to show the dialog over is found.</param>
/// <param name="messageBoxService">A message box service of the application that takes over every dialog, or <see langword="null"/> to use the platform's own.</param>
#endif
public class RouteableDialogService(
#if ANDROID || WINDOWS || IOS || MACCATALYST    
    IServiceProvider provider,
#endif
    IRouteableDialogWindowContext windowContext,
    IMessageBoxService? messageBoxService = null) :
    RouteableDialogServiceBase,
    IRouteableDialogService,
    IMessageBoxService
{
    // The native implementation of IMessageBoxService: the interface's order is (title, message, buttons).
    Task<int> IMessageBoxService.ShowMessageAsync(string title, string message, string buttons) => ShowMessageAsync(message, title, buttons);

    /// <summary>
    /// Shows the dialog of the platform (<see cref="DisplayThreeButtonsAsync{T}"/>): one button for one word, two for two, three for three. The confirmations (<see cref="IRouteableDialogService"/>) are written once, in
    /// <see cref="RouteableDialogServiceBase"/>.
    /// </summary>
    /// <param name="message">The message to display.</param>
    /// <param name="title">The dialog title.</param>
    /// <param name="buttonText">The buttons written together, one word per capital letter: <c>"YesNoCancel"</c>.</param>
    /// <returns>The index of the button chosen: 0 for the first.</returns>
    public override async Task<int> ShowMessageAsync(string message, string title, string buttonText)
    {
        if (messageBoxService != null)
            return await messageBoxService.ShowMessageAsync(title, message, buttonText);

        // Fails like the dialog of the platform when there is no page to show it over.
        _ = windowContext.CurrentPage ?? throw new InvalidOperationException(
            "No active Page to host the content dialog popup.");

        // Every message box is the dialog of the platform with up to three buttons (not DisplayAlert): the answer is the index of the button, and the
        // dialog dismissed without a button (Back, Escape) is the last one, the cancel of an alert.
        var words = RouteableButtons.Split(buttonText);
        switch (words.Count)
        {
            case 0:
            case 1:
                await DisplayThreeButtonsAsync<int>(title, message, words.Count == 0 ? "Ok" : words[0], null, null, 0, 1, 0);
                return 0;
            case 2:
                return await DisplayThreeButtonsAsync(title, message, words[0], words[1], null, 0, 1, 1);
            default:
                return await DisplayThreeButtonsAsync(title, message, words[0], words[1], words[2], 0, 1, 2);
        }
    }

    /// <summary>
    /// Displays a platform-native dialog with up to three buttons, using the appropriate native API
    /// for the current platform (Android alert dialog, WinUI content dialog, or an iOS/Mac Catalyst
    /// alert controller or form-sheet view controller), and awaits the user's selection.
    /// </summary>
    /// <typeparam name="T">The result type associated with each button.</typeparam>
    /// <param name="title">The dialog title.</param>
    /// <param name="content">The dialog content: a message string, or a view/element to embed.</param>
    /// <param name="primaryButtonText">The primary button's text, or <see langword="null"/> to omit it.</param>
    /// <param name="secondaryButtonText">The secondary button's text, or <see langword="null"/> to omit it.</param>
    /// <param name="closeButtonText">The close button's text, or <see langword="null"/> to omit it.</param>
    /// <param name="primaryResult">The result returned if the primary button is selected.</param>
    /// <param name="secondaryResult">The result returned if the secondary button is selected.</param>
    /// <param name="closeResult">The result returned if the close button is selected.</param>
    /// <returns>The result associated with the button the user selected.</returns>
    /// <exception cref="InvalidOperationException">There is no activity, window or page to show the dialog on.</exception>

    public async Task<T?> DisplayThreeButtonsAsync<T>(
         string? title, object? content,
         string? primaryButtonText, string? secondaryButtonText, string? closeButtonText,
         T primaryResult, T secondaryResult, T closeResult)
    {
        // The native platforms show the view as a platform view. The plain net10.0 target has no platform view: the view is
        // given as it is to a popup (see the end of the method), which is the caller's, so it is not disconnected below.
        Element? hosted = null;
#if ANDROID || WINDOWS || IOS || MACCATALYST
        hosted = content as Element;
        if (hosted is not null)
        {
            content = Microsoft.Maui.Platform.ElementExtensions.ToPlatform(hosted, windowContext.CurrentPage?.Handler?.MauiContext ?? new MauiContext(provider));
        }
#endif

        try
        {
            // The platform dialog is that of Routeables.Native, shared with WinUI 3 and Uno.
            object? owner = null;
            bool? dark = null;
#if WINDOWS
            owner = (windowContext.CurrentPage
                ?? throw new InvalidOperationException("No active Page to host the content dialog popup.")).Handler?.PlatformView is Microsoft.UI.Xaml.UIElement page ? page.XamlRoot : null;
            dark = Application.Current?.RequestedTheme == AppTheme.Dark ? true : Application.Current?.RequestedTheme == AppTheme.Light ? false : null;
#endif
            var native = await Routeables.Native.NativeDialogs.DisplayThreeButtonsAsync(
                title, content, primaryButtonText, secondaryButtonText, closeButtonText, primaryResult, secondaryResult, closeResult, owner, dark);
            if (native.Shown)
                return native.Value;

            // No native dialog for this content: the plain net10.0 target (what a platform without a MAUI backend of its own runs),
            // or, on Apple platforms, a content that is neither text nor a UIView (a MAUI view, say). The three buttons are those
            // of a content dialog, drawn by the content dialog service with MAUI views.
            var dialog = new RouteableContentDialog
            {
                Title = title,
                Content = content,
                PrimaryButtonText = primaryButtonText,
                SecondaryButtonText = secondaryButtonText,
                CloseButtonText = closeButtonText
            };

            var result = await ShowContentDialogAsync(dialog);
            return result switch
            {
                RouteableContentDialogResult.Primary => primaryResult,
                RouteableContentDialogResult.Secondary => secondaryResult,
                _ => closeResult
            };
        }
        finally
        {
            // The platform view was created for this dialog only: its handler holds it (and the element) until it is
            // disconnected.
            hosted?.Handler?.DisconnectHandler();
        }
    }

    /// <inheritdoc />
    public override async Task<RouteableContentDialogResult> ShowContentDialogAsync(RouteableContentDialog @params)
    {
        ArgumentNullException.ThrowIfNull(@params);

        if (messageBoxService != null)
            return await messageBoxService.ShowContentDialogAsync(@params);

        var page = windowContext.CurrentPage
            ?? throw new InvalidOperationException(
                "No active Page to host the content dialog popup.");

        var popup = new ContentDialogPopup(@params);

        // The popup closes itself on the main thread, whichever thread asks.
        popup.Session.AllowCloseFromCode(close => close());
        popup.FirstAppearing += (_, _) => popup.Session.Opened();

        popup.Session.Opening();
        try
        {
            return await popup.ShowAsync(page);
        }
        finally
        {
            popup.Session.Release();
        }
    }

    private sealed class ContentDialogPopup : RouteablePopupPage<RouteableContentDialogResult>
    {
        public ContentDialogPopup(RouteableContentDialog p)
        {
            // The dialog is closed with the result its session decided.
            Session = new RouteableContentDialogSession(p, () => _ = CloseAsync(Session!.Result));
            Dialog = RouteableDialogContents.ForContentDialog(p, Session);
        }

        public RouteableContentDialogSession Session { get; }
    }

    /// <inheritdoc />
    public override async Task<RouteableTaskDialogResult> ShowTaskDialogAsync(RouteableTaskDialog @params, params RouteableTaskDialogResult[] results)
    {
        ArgumentNullException.ThrowIfNull(@params);

        if (messageBoxService != null)
            return await messageBoxService.ShowTaskDialogAsync(@params, results);

        var page = windowContext.CurrentPage
            ?? throw new InvalidOperationException("No active Page to host the task dialog.");

        var popup = new TaskDialogPopup(@params, results);
        popup.FirstAppearing += (_, _) => popup.Session.Opened();

        popup.Session.Opening();
        try
        {
            return await popup.ShowAsync(page);
        }
        finally
        {
            popup.Session.Release();
        }
    }

    /// <inheritdoc />
    public async Task<TResult?> ShowModalAsync<TViewModel, TResult>(
    TViewModel viewModel,
    Func<View> viewFactory)
    where TViewModel : ICloseableViewModel<TResult>
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        ArgumentNullException.ThrowIfNull(viewFactory);

        if (messageBoxService != null)
            return await messageBoxService.ShowModalAsync<TViewModel, TResult>(viewModel, viewFactory);

        var page = windowContext.CurrentPage
            ?? throw new InvalidOperationException(
                "No active Page to host the modal popup.");

        var view = viewFactory()
            ?? throw new InvalidOperationException(
                "The modal view factory returned null.");

        var popup = new ModalPopup<TResult>(viewModel, view);

        return await popup.ShowAsync(page);
    }

    private sealed class ModalPopup<TResult> : RouteablePopupPage<TResult>
    {
        private readonly ICloseableViewModel<TResult> _viewModel;

        public ModalPopup(ICloseableViewModel<TResult> viewModel, View view)
        {
            _viewModel = viewModel;
            _viewModel.CloseRequested += OnCloseRequested;

            // Dressed like the platform's own dialog; the first command is the primary button.
            Dialog = RouteableDialogContents.ForModal(viewModel, view);
        }

        private async void OnCloseRequested(object? sender, TResult? result)
        {
            _viewModel.CloseRequested -= OnCloseRequested;
            try { await CloseAsync(result); }
            catch (Exception ex) { RouteableErrors.Report(null, ex, typeof(RouteableDialogService)); }   // async void: an exception would end the application
        }

        // The page went away by other means than the view model closing it: stop listening to the view model.
        protected override void OnHandlerChanging(HandlerChangingEventArgs args)
        {
            base.OnHandlerChanging(args);
            if (args.NewHandler is null)
                _viewModel.CloseRequested -= OnCloseRequested;
        }
    }

    private sealed class TaskDialogPopup : RouteablePopupPage<RouteableTaskDialogResult>
    {
        public TaskDialogPopup(RouteableTaskDialog p, RouteableTaskDialogResult[] results)
        {
            // The dialog is closed with the result its session decided; without results it offers Close.
            Session = new RouteableTaskDialogSession(p, results, () => _ = CloseAsync(Session!.Result));
            Dialog = RouteableDialogContents.ForTaskDialog(p, Session);
        }

        public RouteableTaskDialogSession Session { get; }
    }
}