#if AVALONIA
using Avalonia;
using Avalonia.Controls;
#else
using System.Windows.Controls;
#endif
using Microsoft.Extensions.DependencyInjection;
using Routeables.Abstraction.Bases;
using Routeables.Services;

// One file for Routeables.Wpf and Routeables.Avalonia, which link it (and define WPF or AVALONIA).
#if AVALONIA
namespace Routeables.Avalonia.Services;
#else
namespace Routeables.Wpf.Services;
#endif

/// <summary>
/// Base view for a modal dialog backed by a closeable view model. The view is shown as the content of a content
/// dialog by the <see cref="IMessageBoxService"/> (<see cref="DefaultMessageBoxService"/> unless you supplied your own).
/// The buttons come from the view model's <see cref="ICloseableViewModel{TResult}.Commands"/>.
/// </summary>
/// <typeparam name="TViewModel">The closeable view model type driving this dialog.</typeparam>
/// <typeparam name="TResult">The result type produced when the dialog is closed.</typeparam>
public class RouteableModalFor<TViewModel, TResult> : UserControl,
    IRouteableModalFor<TViewModel, TResult?>
    where TViewModel : class, ICloseableViewModel<TResult?>
{
    /// <summary>Gets or sets the dialog's data context, exposed as an untyped <see cref="object"/> for <see cref="IRouteableModalFor{TViewModel, TResult}"/>.</summary>
    public object? Context { get => DataContext; set => DataContext = value; }

    /// <summary>Gets the icon identifier used by dialog implementations that show one. Empty by default.</summary>
    protected virtual string Icon { get; } = "";

    /// <summary>Gets the strongly typed view model bound to this dialog's <c>DataContext</c>, or <see langword="null"/> if not set or of the wrong type.</summary>
    protected TViewModel? ViewModel => DataContext as TViewModel;

    /// <summary>Shows this dialog modally and awaits its result.</summary>
    /// <returns>
    /// The value the view model passed to <see cref="ICloseableViewModel{TResult}.CloseAsync"/> while the dialog was open,
    /// after <see cref="OnValidateAsync(TResult?)"/>; the default value when the dialog was dismissed without one.
    /// </returns>
    /// <exception cref="ArgumentNullException"><see cref="ViewModel"/> is <see langword="null"/> (<see cref="Context"/> was not set to a <typeparamref name="TViewModel"/>).</exception>
    /// <exception cref="InvalidOperationException">No <see cref="IMessageBoxService"/> is available: the application did not call <c>UseRouteablesServices</c>.</exception>
    public async Task<TResult?> ShowModalAsync()
    {
        var viewModel = ViewModel;
        if (viewModel is null)
            throw new ArgumentNullException(nameof(viewModel));

        var service = ServiceLocator.Services?.GetService<IMessageBoxService>()
            ?? throw new InvalidOperationException(
#if AVALONIA
                "No IMessageBoxService is available. Add Routeables to the application with AppBuilder.UseRouteables(services => services.UseRouteablesServices()).");
#else
                "No IMessageBoxService is available. Add Routeables to the application with Application.UseRouteables(...).");
#endif

        using var session = new RouteableModalSession<TResult>(viewModel, this);
#if AVALONIA
        await service.ShowContentDialogAsync(session.Dialog);
#else
        await service.ShowAsync(session.Dialog);
#endif

        await OnValidateAsync(session.Result);
        return session.Result;
    }

    /// <summary>Override to perform custom validation logic if needed.</summary>
    /// <param name="result">The result the dialog produced.</param>
    protected virtual Task OnValidateAsync(TResult? result)
    {
        return Task.CompletedTask;
    }
}
