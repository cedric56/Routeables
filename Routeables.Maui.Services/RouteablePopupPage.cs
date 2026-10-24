using Microsoft.Maui.Controls.PlatformConfiguration;
using Microsoft.Maui.Controls.PlatformConfiguration.iOSSpecific;
using Routeables.Abstraction;

namespace Routeables.Maui.Services;

/// <summary>
/// A dialog shown as a transparent modal page over the current page: the page behind stays visible under a dimmed layer,
/// and several dialogs can be open at once (each one is a modal on the window's stack), which a native
/// WinUI ContentDialog does not allow. It needs nothing but MAUI.
/// <para>
/// Only the dialog's own buttons close it: the Android back button and a tap outside do nothing.
/// Dialogs are closed in the reverse order they were opened; one that is closed under another waits until it is on top.
/// </para>
/// </summary>
/// <typeparam name="TResult">What the dialog returns when it closes.</typeparam>
internal abstract class RouteablePopupPage<TResult> : ContentPage
{
    private readonly TaskCompletionSource<TResult?> _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private bool _closed;
    private TResult? _result;
    private bool _showing;
    private IDispatcher? _dispatcher;
    private bool _appeared;

    protected RouteablePopupPage()
    {
        BackgroundColor = Color.FromArgb("#80000000");
        Microsoft.Maui.Controls.NavigationPage.SetHasNavigationBar(this, false);
        Shell.SetNavBarIsVisible(this, false);
        Shell.SetTabBarIsVisible(this, false);

        // iOS and Mac Catalyst would hide the page behind a full-screen modal.
        On<iOS>().SetModalPresentationStyle(UIModalPresentationStyle.OverFullScreen);
    }

    /// <summary>Raised once, when the dialog is on screen for the first time.</summary>
    public event EventHandler? FirstAppearing;

    /// <summary>The dialog, centered on the dimmed layer.</summary>
    protected View? Dialog
    {
        set => Content = new Grid
        {
            Padding = new Thickness(24),
            Children = { value! }
        };
    }

    /// <summary>Shows the dialog over <paramref name="host"/> and returns what it was closed with.</summary>
    public async Task<TResult?> ShowAsync(Microsoft.Maui.Controls.Page host)
    {
        ArgumentNullException.ThrowIfNull(host);

        _dispatcher = host.Dispatcher;
        await _dispatcher.DispatchAsync(() => host.Navigation.PushModalAsync(this, animated: false));
        _showing = true;
        return await _completion.Task;
    }

    /// <summary>Closes the dialog; the awaiting <see cref="ShowAsync"/> receives <paramref name="result"/>.</summary>
    protected async Task CloseAsync(TResult? result)
    {
        if (_closed)
            return;
        _closed = true;
        _result = result;       // kept first: removing the page disconnects its handler (see OnHandlerChanging)

        try
        {
            // The view model may close the dialog from any thread; the modal stack belongs to the dispatcher of the host page.
            await (_dispatcher ?? Dispatcher).DispatchAsync(async () =>
            {
                // Popping removes the top modal: wait until this one is it.
                while (Navigation.ModalStack.Contains(this) && !ReferenceEquals(Navigation.ModalStack.LastOrDefault(), this))
                    await Task.Delay(50);

                if (Navigation.ModalStack.Contains(this))
                    await Navigation.PopModalAsync(animated: false);
            });
        }
        catch (Exception ex)
        {
            // Callers are event handlers (async void): an exception here would end the application.
            RouteableErrors.Report(null, ex, typeof(RouteablePopupPage<TResult>));
        }
        finally
        {
            _completion.TrySetResult(result);
        }
    }

    protected override void OnHandlerChanging(HandlerChangingEventArgs args)
    {
        base.OnHandlerChanging(args);

        // The page was removed without CloseAsync (its window was closed, the platform dismissed it): release the caller
        // awaiting ShowAsync instead of leaving it waiting for ever. After CloseAsync, this is the result it was given.
        if (args.NewHandler is null && _showing)
            _completion.TrySetResult(_result);
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        // A page appears again each time a dialog opened over it closes: the dialog is opened only once.
        if (_appeared)
            return;
        _appeared = true;
        FirstAppearing?.Invoke(this, EventArgs.Empty);
    }

    protected override bool OnBackButtonPressed() => true;
}
