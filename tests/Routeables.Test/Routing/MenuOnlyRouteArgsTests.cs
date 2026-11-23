using Routeables.Abstraction;
using Routeables.Abstraction.Bases;
using Routeables.Abstraction.Registrations;
using Routeables.Abstraction.Routing;
using Routeables.Routing;
using Routeables.Tests.Core;
using Routeables.Tests.Pages;

namespace Routeables.Tests.Routing;

/// <summary>A page that only the menu knows (no route registered, so no template to read an address with) still gets what the caller gave.</summary>
public class MenuOnlyRouteArgsTests
{

    private static ServiceProvider Build()
    {
        var services = new ServiceCollection();
        services.RegisterRouting();
        services.RegisterRoute<FakeViewFor>("/settings");
        services.RegisterMenuViewModel<FakeMenuViewModel>(ServiceLifetime.Singleton);
        var provider = services.BuildServiceProvider();
        // A page the menu lists but the table of routes does not (a page added by a module or by the generator after the table was read):
        // no template of the table matches "/settings", so only the entry of the menu knows how to open it.
        provider.GetRequiredService<RouteableTable>().Clear();
        provider.GetRequiredService<IRouteableMenuViewModel>().Entries.Add(new RouteableMenuEntry("/settings", "/settings", "Settings"));
        return provider;
    }

    [Fact]
    public async Task The_args_of_the_caller_and_the_query_of_the_address_reach_the_page_of_the_menu()
    {
        using var provider = Build();
        var router = provider.GetRequiredService<IRouter>();
        var args = RouteableArgs.Create().WithValue("source", "mail");

        var result = await router.GoToAsync("/settings?tab=2", args, cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Success, result.Exception?.ToString());
        Assert.IsType<FakeViewFor>(router.CurrentView);
        var values = router.History.Current!.Args.Values.ToDictionary(v => v.name!, v => v.value);
        Assert.Equal("mail", values["source"]);
        Assert.Equal("2", values["tab"]?.ToString());
    }

    [Fact]
    public async Task The_args_of_the_caller_are_not_changed_by_the_query()
    {
        using var provider = Build();
        var router = provider.GetRequiredService<IRouter>();
        var args = RouteableArgs.Create().WithValue("source", "mail");

        await router.GoToAsync("/settings?tab=2", args, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Single(args.Values);
    }

    [Fact]
    public async Task Without_args_and_without_a_query_the_page_of_the_menu_opens_as_before()
    {
        using var provider = Build();
        var router = provider.GetRequiredService<IRouter>();

        var result = await router.GoToAsync("/settings", cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Success, result.Exception?.ToString());
        Assert.IsType<FakeViewFor>(router.CurrentView);
    }

    [Fact]
    public async Task The_history_keeps_the_address_that_was_asked_for_with_its_query()
    {
        using var provider = Build();
        var router = provider.GetRequiredService<IRouter>();

        await router.GoToAsync("/settings?tab=2", cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("/settings?tab=2", router.History.Current!.Args.Route);
        Assert.Equal("2", router.History.Current.Args.Values.Single(v => v.name == "tab").value?.ToString());
    }

    [Fact]
    public async Task A_parameter_of_the_caller_replaces_the_parameter_of_the_entry_that_has_the_same_name()
    {
        using var provider = Build();
        var router = provider.GetRequiredService<IRouter>();
        var entry = provider.GetRequiredService<IRouteableMenuViewModel>().Entries.Single();
        entry.Arguments = RouteableArgs.Create()
            .WithViewModel("mode", "entry").WithViewModel("kept", "entry")
            .WithView("theme", "entry")
            .WithValue("level", "entry");
        var args = RouteableArgs.Create()
            .WithViewModel("mode", "caller")
            .WithView("theme", "caller")
            .WithValue("level", "caller");

        var result = await router.GoToAsync("/settings", args, cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Success, result.Exception?.ToString());
        var shown = router.History.Current!.Args;
        Assert.Equal("caller", shown.ViewModelParameters.Single(v => v.name == "mode").value?.ToString());
        Assert.Equal("entry", shown.ViewModelParameters.Single(v => v.name == "kept").value?.ToString());
        Assert.Equal("caller", shown.ViewParameters.Single(v => v.name == "theme").value?.ToString());
        Assert.Equal("caller", shown.Values.Single(v => v.name == "level").value?.ToString());
        // What the entry holds is not changed by the navigation.
        Assert.Equal("entry", entry.Arguments.ViewModelParameters.Single(v => v.name == "mode").value?.ToString());
    }
}
