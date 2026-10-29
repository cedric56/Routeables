using Routeables.Abstraction;
using Routeables.Avalonia;
using Routeables.Avalonia.Services;
using System.Collections.ObjectModel;
using System.Windows.Input;
using Routeables.Services;

namespace Routeables.Tests.Avalonia;

// ServiceLocator.Services is one static for the process: the classes that read or set it must not run at the same time.
[Collection("Avalonia ServiceLocator")]
public class DialogViewTests
{
    private sealed class TestViewModel : ICloseableViewModel<int>
    {
        public string Title => "Test dialog";
        public ObservableCollection<RouteableCommand> Commands { get; } = [];
        public ICommand? CloseCommand => null;
        public event EventHandler<int>? CloseRequested;
        public bool HasSubscribers => CloseRequested is not null;

        public Task CloseAsync(int value)
        {
            CloseRequested?.Invoke(this, value);
            return Task.CompletedTask;
        }
    }

    [Fact]
    public void Context_maps_to_the_Avalonia_DataContext()
    {
        var view = new DialogView<TestViewModel, int>();
        var viewModel = new TestViewModel();

        view.Context = viewModel;

        Assert.Same(viewModel, view.DataContext);
        Assert.Same(viewModel, view.Context);
    }

    [Fact]
    public async Task ShowModalAsync_requires_a_closeable_view_model_context()
    {
        var view = new DialogView<TestViewModel, int>();

        var exception = await Assert.ThrowsAsync<ArgumentNullException>(() => view.ShowModalAsync());

        Assert.Equal("viewModel", exception.ParamName);
    }

    // ---- ShowModalAsync with a fake message box service ------------------------------------------------------

    private sealed class RecordingCommand(Func<bool> canExecute, Action? execute = null) : ICommand
    {
        public event EventHandler? CanExecuteChanged { add { } remove { } }

        public bool CanExecute(object? parameter) => canExecute();

        public void Execute(object? parameter) => execute?.Invoke();
    }

    private sealed class FakeMessageBox(Func<RouteableContentDialog, Task<RouteableContentDialogResult>> show) : IMessageBoxService
    {
        public bool IsDisplayable(global::Avalonia.Controls.TopLevel topLevel) => true;

        public Task<string?> ShowMessageBoxAsync(string title, string message, string buttons, global::Avalonia.Controls.TopLevel? topLevel = null) =>
            Task.FromResult<string?>(null);

        public Task<RouteableContentDialogResult> ShowContentDialogAsync(RouteableContentDialog @params, global::Avalonia.Controls.TopLevel? topLevel = null) =>
            show(@params);

        public Task<RouteableTaskDialogResult> ShowTaskDialogAsync(RouteableTaskDialog @params, global::Avalonia.Controls.TopLevel? topLevel = null, params RouteableTaskDialogResult[] results) =>
            Task.FromResult(RouteableTaskDialogResult.None);
    }

    private static async Task<T> WithServiceAsync<T>(IMessageBoxService service, Func<Task<T>> body)
    {
        var services = new ServiceCollection();
        services.AddSingleton(service);
        using var provider = services.BuildServiceProvider();
        var previous = ServiceLocator.Services;
        ServiceLocator.Services = provider;
        try
        {
            return await body();
        }
        finally
        {
            ServiceLocator.Services = previous;
        }
    }

    private static RouteableCommand Command(string name, ICommand? command = null) =>
        new() { Name = name, Command = command ?? new RecordingCommand(() => true) };

    [Fact]
    public async Task ShowModalAsync_maps_the_commands_to_the_dialog_buttons()
    {
        var viewModel = new TestViewModel();
        viewModel.Commands.Add(Command("Save"));
        viewModel.Commands.Add(Command("Skip"));
        viewModel.Commands.Add(Command("Cancel"));
        var view = new DialogView<TestViewModel, int> { Context = viewModel };
        RouteableContentDialog? shown = null;

        await WithServiceAsync(new FakeMessageBox(dialog => { shown = dialog; return Task.FromResult(RouteableContentDialogResult.None); }),
            async () => await view.ShowModalAsync());

        Assert.NotNull(shown);
        Assert.Equal("Test dialog", shown!.Title);
        Assert.Same(view, shown.Content);
        Assert.Equal("Save", shown.PrimaryButtonText);
        Assert.Equal("Skip", shown.SecondaryButtonText);
        Assert.Equal("Cancel", shown.CloseButtonText);
        Assert.True(shown.IsPrimaryButtonEnabled);
        Assert.True(shown.IsSecondaryButtonEnabled);
    }

    [Fact]
    public async Task ShowModalAsync_adds_a_close_button_when_the_view_model_has_no_commands()
    {
        var view = new DialogView<TestViewModel, int> { Context = new TestViewModel() };
        RouteableContentDialog? shown = null;

        await WithServiceAsync(new FakeMessageBox(dialog => { shown = dialog; return Task.FromResult(RouteableContentDialogResult.None); }),
            async () => await view.ShowModalAsync());

        Assert.Equal("Close", shown!.CloseButtonText);
        Assert.Null(shown.PrimaryButtonText);
    }

    [Fact]
    public async Task Primary_and_secondary_keep_the_dialog_open_until_the_view_model_asks_to_close()
    {
        var viewModel = new TestViewModel();
        viewModel.Commands.Add(Command("Save"));
        viewModel.Commands.Add(Command("Skip"));
        var view = new DialogView<TestViewModel, int> { Context = viewModel };
        bool? primaryBefore = null, secondaryBefore = null, primaryAfter = null;

        await WithServiceAsync(new FakeMessageBox(async dialog =>
        {
            primaryBefore = dialog.Closing!("Primary");
            secondaryBefore = dialog.Closing("Secondary");
            await viewModel.CloseAsync(1);
            primaryAfter = dialog.Closing("Primary");
            return RouteableContentDialogResult.Primary;
        }), async () => await view.ShowModalAsync());

        Assert.True(primaryBefore == true);
        Assert.True(secondaryBefore == true);
        Assert.True(primaryAfter == false);
    }

    [Fact]
    public async Task The_close_button_is_vetoed_only_when_its_command_cannot_execute()
    {
        var viewModel = new TestViewModel();
        viewModel.Commands.Add(Command("Save"));
        viewModel.Commands.Add(Command("Skip"));
        viewModel.Commands.Add(Command("Cancel", new RecordingCommand(() => false)));
        var view = new DialogView<TestViewModel, int> { Context = viewModel };
        bool? vetoed = null;

        await WithServiceAsync(new FakeMessageBox(dialog =>
        {
            vetoed = dialog.Closing!("None");
            return Task.FromResult(RouteableContentDialogResult.None);
        }), async () => await view.ShowModalAsync());

        Assert.True(vetoed == true);
    }

    [Fact]
    public async Task A_close_request_after_the_buttons_closes_the_dialog_through_the_Close_hook()
    {
        var viewModel = new TestViewModel();
        viewModel.Commands.Add(Command("Save"));
        var view = new DialogView<TestViewModel, int> { Context = viewModel };
        var closes = 0;

        var result = await WithServiceAsync(new FakeMessageBox(async dialog =>
        {
            // What the real service does: it hands the dialog a way to be closed from code.
            dialog.Close = () => closes++;
            await viewModel.CloseAsync(7);
            return RouteableContentDialogResult.Primary;
        }), async () => await view.ShowModalAsync());

        Assert.Equal(7, result);
        Assert.Equal(1, closes);
    }

    [Fact]
    public async Task ShowModalAsync_returns_the_value_of_the_close_request_and_unsubscribes()
    {
        var viewModel = new TestViewModel();
        var view = new DialogView<TestViewModel, int> { Context = viewModel };

        var result = await WithServiceAsync(new FakeMessageBox(async _ =>
        {
            await viewModel.CloseAsync(42);
            return RouteableContentDialogResult.Primary;
        }), async () => await view.ShowModalAsync());

        Assert.Equal(42, result);
        Assert.False(viewModel.HasSubscribers);
    }

    [Fact]
    public async Task ShowModalAsync_unsubscribes_when_the_dialog_is_dismissed_or_fails()
    {
        var viewModel = new TestViewModel();
        var view = new DialogView<TestViewModel, int> { Context = viewModel };

        await Assert.ThrowsAsync<InvalidOperationException>(() => WithServiceAsync(
            new FakeMessageBox(_ => throw new InvalidOperationException("boom")),
            async () => await view.ShowModalAsync()));

        Assert.False(viewModel.HasSubscribers);
    }

    [Fact]
    public async Task ShowModalAsync_without_a_message_box_service_explains_what_is_missing()
    {
        var view = new DialogView<TestViewModel, int> { Context = new TestViewModel() };
        var previous = ServiceLocator.Services;
        ServiceLocator.Services = null;
        try
        {
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => view.ShowModalAsync());

            Assert.Contains("UseRouteablesServices", exception.Message);
        }
        finally
        {
            ServiceLocator.Services = previous;
        }
    }
}