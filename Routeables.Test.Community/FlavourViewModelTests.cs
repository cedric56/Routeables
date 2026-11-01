using System.Collections.ObjectModel;
using NSubstitute;
using Routeables.Abstraction;
using Routeables.Core;

namespace Routeables.Test.Flavour;

/// <summary>
/// The modal view model of a flavour (CommunityToolkit.Mvvm or ReactiveUI): its commands, and how it closes. This file is compiled by both
/// test projects, each with the source of its flavour, since the two do the same.
/// </summary>
public class FlavourViewModelTests
{
    private sealed class Modal(IRouter router) : RouteableViewModel<string>(router, initialize: false)
    {
        public override string Title => "A modal";
        public int Validated { get; private set; }
        public bool MayCancel { get; set; } = true;
        public bool MayClose { get; set; } = true;
        public int CommandsBuilt { get; private set; }

        public override Task ValidateAsync()
        {
            Validated++;
            return Task.CompletedTask;
        }

        protected override Task<bool> CanCancelAsync() => Task.FromResult(MayCancel);

        protected override Task<bool> CanCloseAsync() => Task.FromResult(MayClose);

        protected override ObservableCollection<IRouteableEntry> GetRouteables() => [];

        protected override ObservableCollection<RouteableCommand> GetCommands()
        {
            CommandsBuilt++;
            return base.GetCommands();
        }
    }

    private static Modal Create() => new(Substitute.For<IRouter>());

    private static async Task UntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!condition())
        {
            if (DateTime.UtcNow > deadline) throw new TimeoutException("The condition was not met in 5 seconds.");
            await Task.Delay(10, TestContext.Current.CancellationToken);
        }
    }

    private static void Run(RouteableCommand command) => command.Command.Execute(null);

    [Fact]
    public void A_modal_offers_Validate_Cancel_and_Close_in_that_order()
    {
        var modal = Create();

        Assert.Equal(["Validate", "Cancel", "Close"], modal.Commands.Select(c => c.Name));
        Assert.All(modal.Commands, c => Assert.NotNull(c.Command));
    }

    [Fact]
    public void The_commands_are_built_once_and_a_subclass_can_choose_them()
    {
        var modal = Create();

        Assert.Same(modal.Commands, modal.Commands);
        Assert.Equal(1, modal.CommandsBuilt);
    }

    [Fact]
    public void The_modal_names_itself_and_exposes_its_close_command_to_the_modal_service()
    {
        var modal = Create();
        var closeable = modal;

        Assert.Equal("A modal", closeable.Title);
        Assert.NotNull(closeable.CloseCommand);
        Assert.Same(closeable.Commands, modal.Commands);
    }

    [Fact]
    public async Task Validate_runs_the_validation_of_the_modal()
    {
        var modal = Create();

        Run(modal.Commands.Single(c => c.Name == "Validate"));

        await UntilAsync(() => modal.Validated == 1);
        Assert.Equal(1, modal.Validated);
    }

    [Fact]
    public async Task Closing_with_a_value_asks_the_service_to_close_with_that_value()
    {
        var modal = Create();
        var received = new List<string?>();
        modal.CloseRequested += (_, value) => received.Add(value);

        await modal.CloseAsync("done");

        Assert.Equal(["done"], received);
    }

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
