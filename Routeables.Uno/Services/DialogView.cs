using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Routeables.Abstraction;
using Routeables.Services;
using Routeables.Uno;

namespace Routeables.Uno.Services;

/// <summary>
/// Non-generic base class for dialog views, allowing dialog controls to be referenced or
/// grouped without knowing their view model or result types.
/// </summary>
public partial class DialogViewBase : UserControl
{
}

/// <summary>
/// Base view for a modal dialog backed by a closeable view model. The view is shown as the content of a content
/// dialog by the <see cref="IMessageBoxService"/> (<see cref="DefaultMessageBoxService"/> unless you supplied your own).
/// The buttons come from the view model's <see cref="ICloseableViewModel{TResult}.Commands"/>.
/// WinUI cannot declare a generic root element in XAML, so a dialog view derives from this class in code and builds its content there
/// (see <c>NoteView</c> in the sample).
/// </summary>
/// <typeparam name="TViewModel">The closeable view model type driving this dialog.</typeparam>
/// <typeparam name="TResult">The result type produced when the dialog is closed.</typeparam>
public partial class DialogView<TViewModel, TResult> : DialogViewBase,
    IRouteableModalFor<TViewModel, TResult?>
    where TViewModel : class, ICloseableViewModel<TResult?>
{
    /// <summary>The dialog's data context, exposed as an untyped <see cref="object"/> for <see cref="IRouteableModalFor{TViewModel, TResult}"/>.</summary>
#if ANDROID
    // A native Android view already has a Context (the Android one): this one is the view model.
    public new object? Context { get => DataContext; set => DataContext = value; }
#else
    public object? Context { get => DataContext; set => DataContext = value; }
#endif

    /// <summary>The view model bound to this dialog's <c>DataContext</c>, or <see langword="null"/> if not set or of the wrong type.</summary>
    protected TViewModel? ViewModel => DataContext as TViewModel;

    /// <summary>Shows this dialog modally and awaits its result: the value the view model passed to <c>Close</c>, or the default when dismissed.</summary>
    /// <exception cref="ArgumentNullException"><see cref="Context"/> was not set to a <typeparamref name="TViewModel"/>.</exception>
    /// <exception cref="InvalidOperationException">No <see cref="IMessageBoxService"/> is available: the application did not call <c>UseRouteablesServices</c>.</exception>
    public async Task<TResult?> ShowModalAsync()
    {
        var viewModel = ViewModel;
        if (viewModel is null)
            throw new ArgumentNullException(nameof(viewModel));

        var service = ServiceLocator.Services?.GetService<IMessageBoxService>()
            ?? throw new InvalidOperationException(
                "No IMessageBoxService is available. Add Routeables to the application with Application.UseRouteables(services => services.UseRouteablesServices()).");

        using var session = new RouteableModalSession<TResult>(viewModel, this);
        await UiThread.RunAsync(() => service.ShowAsync(session.Dialog));

        await OnValidateAsync(session.Result);
        return session.Result;
    }

    /// <summary>Override to perform custom validation logic if needed.</summary>
    /// <param name="result">The result the dialog produced.</param>
    protected virtual Task OnValidateAsync(TResult? result) => Task.CompletedTask;
}
