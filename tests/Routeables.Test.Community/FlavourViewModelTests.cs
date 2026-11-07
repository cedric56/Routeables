using NSubstitute;
using Routeables.Abstraction;
using Routeables.Core;
using System.Collections.ObjectModel;

namespace Routeables.Test.Flavour;

/// <summary>
/// The modal view model of a flavour (CommunityToolkit.Mvvm or ReactiveUI): its commands, and how it closes. This file is compiled by both
/// test projects, each with the source of its flavour, since the two do the same.
/// </summary>
public class FlavourViewModelTests
{
    /// <summary>A minimal modal used by the tests: it records validation and command construction, and can refuse cancel or close.</summary>
    private sealed class Modal(IRouter router) : RouteableViewModel<string>(router, initialize: false)
    {
        /// <summary>The fixed title of the modal.</summary>
        public override string Title => "A modal";

        /// <summary>How many times <see cref="ValidateAsync"/> has been called.</summary>
        public int Validated { get; private set; }

        /// <summary>Whether the modal currently allows cancel. Defaults to <c>true</c>.</summary>
        public bool MayCancel { get; set; } = true;

        /// <summary>Whether the modal currently allows close. Defaults to <c>true</c>.</summary>
        public bool MayClose { get; set; } = true;

        /// <summary>How many times <see cref="GetCommands"/> has been called.</summary>
        public int CommandsBuilt { get; private set; }

        /// <inheritdoc />
        public override Task ValidateAsync()
        {
            Validated++;
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        protected override Task<bool> CanCancelAsync() => Task.FromResult(MayCancel);

        /// <inheritdoc />
        protected override Task<bool> CanCloseAsync() => Task.FromResult(MayClose);

        /// <inheritdoc />
        protected override ObservableCollection<IMenuEntry> GetEntries() => [];

        /// <inheritdoc />
        protected override ObservableCollection<RouteableCommand> GetCommands()
        {
            CommandsBuilt++;
            return base.GetCommands();
        }
    }

    /// <summary>Creates a fresh <see cref="Modal"/> with a substitute router.</summary>
    private static Modal Create() => new(Substitute.For<IRouter>());

    /// <summary>Polls <paramref name="condition"/> until it becomes true or a 5-second timeout elapses.</summary>
    private static async Task UntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!condition())
        {
            if (DateTime.UtcNow > deadline) throw new TimeoutException("The condition was not met in 5 seconds.");
            await Task.Delay(10, TestContext.Current.CancellationToken);
        }
    }

    /// <summary>Executes the underlying command of a <see cref="RouteableCommand"/>.</summary>
    private static void Run(RouteableCommand command) => command.Command.Execute(null);

    /// <summary>A modal offers Validate, Cancel and Close in that order, and each has a non-null command.</summary>
    [Fact]
    public void A_modal_offers_Validate_Cancel_and_Close_in_that_order()
    {
        var modal = Create();

        Assert.Equal(["Validate", "Cancel", "Close"], modal.Commands.Select(c => c.Name));
        Assert.All(modal.Commands, c => Assert.NotNull(c.Command));
    }

    /// <summary>The commands collection is built once and reused; a subclass can override which commands are offered.</summary>
    [Fact]
    public void The_commands_are_built_once_and_a_subclass_can_choose_them()
    {
        var modal = Create();

        Assert.Same(modal.Commands, modal.Commands);
        Assert.Equal(1, modal.CommandsBuilt);
    }

    /// <summary>The modal exposes its title and close command to the modal service, and reuses the same commands collection.</summary>
    [Fact]
    public void The_modal_names_itself_and_exposes_its_close_command_to_the_modal_service()
    {
        var modal = Create();
        var closeable = modal;

        Assert.Equal("A modal", closeable.Title);
        Assert.NotNull(closeable.CloseCommand);
        Assert.Same(closeable.Commands, modal.Commands);
    }

    /// <summary>Running Validate invokes the modal's <see cref="Modal.ValidateAsync"/> once.</summary>
    [Fact]
    public async Task Validate_runs_the_validation_of_the_modal()
    {
        var modal = Create();

        Run(modal.Commands.Single(c => c.Name == "Validate"));

        await UntilAsync(() => modal.Validated == 1);
        Assert.Equal(1, modal.Validated);
    }

    /// <summary>Closing with a value raises <see cref="RouteableViewModel{T}.CloseRequested"/> with that value.</summary>
    [Fact]
    public async Task Closing_with_a_value_asks_the_service_to_close_with_that_value()
    {
        var modal = Create();
        var received = new List<string?>();
        modal.CloseRequested += (_, value) => received.Add(value);

        await modal.CloseAsync("done");

        Assert.Equal(["done"], received);
    }

    /// <summary>Running Cancel raises <see cref="RouteableViewModel{T}.CloseRequested"/> with a null result.</summary>
    [Fact]
    public async Task Cancel_closes_with_no_result()
    {
        var modal = Create();
        var received = new List<string?>();
        modal.CloseRequested += (_, value) => received.Add(value);

        Run(modal.Commands.Single(c => c.Name == "Cancel"));

        await UntilAsync(() => received.Count == 1);
        Assert.Equal([(string?)null], received);
    }

    /// <summary>When the modal refuses cancel, running Cancel does not raise <see cref="RouteableViewModel{T}.CloseRequested"/>.</summary>
    [Fact]
    public async Task Cancel_does_not_close_when_the_modal_refuses_it()
    {
        var modal = Create();
        modal.MayCancel = false;
        var closed = 0;
        modal.CloseRequested += (_, _) => closed++;

        Run(modal.Commands.Single(c => c.Name == "Cancel"));
        await Task.Delay(200, TestContext.Current.CancellationToken);

        Assert.Equal(0, closed);
    }

    /// <summary>When the modal allows close, running Close raises <see cref="RouteableViewModel{T}.CloseRequested"/> with a null result.</summary>
    [Fact]
    public async Task Close_closes_with_no_result_when_the_modal_allows_it()
    {
        var modal = Create();
        var received = new List<string?>();
        modal.CloseRequested += (_, value) => received.Add(value);

        Run(modal.Commands.Single(c => c.Name == "Close"));

        await UntilAsync(() => received.Count == 1);
        Assert.Equal([(string?)null], received);
    }

    /// <summary>When the modal refuses close, running Close does not raise <see cref="RouteableViewModel{T}.CloseRequested"/>.</summary>
    [Fact]
    public async Task Close_does_not_close_when_the_modal_refuses_it()
    {
        var modal = Create();
        modal.MayClose = false;
        var closed = 0;
        modal.CloseRequested += (_, _) => closed++;

        Run(modal.Commands.Single(c => c.Name == "Close"));
        await Task.Delay(200, TestContext.Current.CancellationToken);

        Assert.Equal(0, closed);
    }
}