using Microsoft.Extensions.DependencyInjection;
using Routeables.Abstraction;
using Routeables.Routing;
using Routeables.Segments;
using RouteableArgument = Routeables.Abstraction.RouteableArgument;

namespace Routeables.Tests.Routing;

/// <summary>Segment routing: parsing of <see cref="RouteCoordination"/> and its dispatch to routers by <see cref="IRouteCoordinator"/>.</summary>
public class RouteCoordinatorTests
{
    [AttributeUsage(AttributeTargets.Property)]
    public sealed class ParamAttribute : Attribute;

    public sealed class OrdersView : IRouteableFor { public object? Context { get; set; } }
    public sealed class DetailView : IRouteableFor { public object? Context { get; set; } }
    public sealed class SideView : IRouteableFor { public object? Context { get; set; } }
    public sealed class OrphanView : IRouteableFor { public object? Context { get; set; } }

    public sealed class OrdersVm
    {
        [Param] public string? Status { get; set; }
        [Param] public string[]? Tags { get; set; }
    }

    public sealed class DetailVm { [Param] public string? Id { get; set; } }
    public sealed class SideVm { [Param] public string? Topic { get; set; } }
    public sealed class OrphanVm;

    private static ServiceProvider Build(Action<IServiceCollection>? configure = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IInjection>(new PropertyInjection(new RouteablePropertyEntry(typeof(ParamAttribute))));
        services.RegisterRouting();
        services.AddRouteCoordinator();
        services.AddRegion("side", ServiceLifetime.Singleton);
        services.RegisterRoute<OrdersView, OrdersVm>();
        services.RegisterRoute<DetailView, DetailVm>();
        services.RegisterRoute<SideView, SideVm>();
        services.RegisterSegment<OrdersVm>("Orders");
        services.RegisterSegment<DetailVm>("Detail");
        services.RegisterSegment<SideVm>("Help");
        configure?.Invoke(services);
        return services.BuildServiceProvider();
    }

    // ---------------------------------------------------------------- parsing

    [Fact]
    public void Parse_reads_each_segment_with_its_own_parameters()
    {
        var routing = RouteCoordination.Parse("Orders?status=open/Detail?id=5&id=6");

        Assert.False(routing.Absolute);
        Assert.Equal(2, routing.Segments.Count);
        Assert.Equal("Orders", routing.Segments[0].Name);
        Assert.Equal([new KeyValuePair<string, object?>("status", "open")], routing.Segments[0].Parameters!);
        Assert.Equal("Detail", routing.Segments[1].Name);
        Assert.Equal(
            [new KeyValuePair<string, object?>("id", "5"), new KeyValuePair<string, object?>("id", "6")],
            routing.Segments[1].Parameters!);
    }

    [Fact]
    public void Parse_reads_absolute_regions_back_flags_and_escapes()
    {
        var routing = RouteCoordination.Parse("/side:Help?flag&q=a%2Fb%20c/../Orders");

        Assert.True(routing.Absolute);
        var help = routing.Segments[0];
        Assert.Equal("side", help.Region);
        Assert.Equal("Help", help.Name);
        Assert.Equal(
            [new KeyValuePair<string, object?>("flag", null), new KeyValuePair<string, object?>("q", "a/b c")],
            help.Parameters!);
        Assert.Equal(RouteSegmentKind.Back, routing.Segments[1].Kind);
        Assert.Equal(RouteSegmentKind.Push, routing.Segments[2].Kind);
    }

    [Fact]
    public void Parse_rejects_malformed_uris()
    {
        Assert.False(RouteCoordination.TryParse("", out _, out var empty));
        Assert.NotNull(empty);
        Assert.False(RouteCoordination.TryParse("/", out _, out _));
        Assert.False(RouteCoordination.TryParse(":Orders", out _, out _));
        Assert.False(RouteCoordination.TryParse("side:", out _, out _));
        Assert.False(RouteCoordination.TryParse("..?y=1", out _, out _));
        Assert.Throws<FormatException>(() => RouteCoordination.Parse(" "));
    }

    [Fact]
    public void The_builder_and_ToString_round_trip_through_Parse()
    {
        var routing = RouteCoordination.Create()
            .Absolute()
            .Segment("Orders", parameters: [("status", "open")])
            .Back()
            .Segment<DetailVm>("side", ("id", 5), ("flag", null))
            .Build();

        Assert.Equal("/Orders?status=open/../side:DetailVm?id=5&flag", routing.ToString());
        var parsed = RouteCoordination.Parse(routing.ToString());
        Assert.Equal(routing.ToString(), parsed.ToString());
        Assert.True(parsed.Absolute);
    }

    [Fact]
    public void The_builder_needs_at_least_one_segment()
    {
        Assert.Throws<InvalidOperationException>(() => RouteCoordination.Create().Build());
        Assert.Throws<ArgumentException>(() => RouteCoordination.Create().Segment(" "));
    }

    // -------------------------------------------------------------- routing

    [Fact]
    public async Task Segments_are_pushed_in_order_and_each_receives_only_its_own_parameters()
    {
        using var provider = Build();
        var coordinator = provider.GetRequiredService<IRouteCoordinator>();
        var router = provider.GetRequiredService<IRouter>();

        var result = await coordinator.CoordinateAsync(
            "Orders?status=open&tags=a&tags=b/Detail?id=5", TestContext.Current.CancellationToken);

        Assert.True(result.Success, result.Exception?.Message);
        Assert.Equal(2, router.History.Entries.Count);
        var detail = Assert.IsType<DetailVm>(router.CurrentViewModel);
        Assert.Equal("5", detail.Id);

        await router.GoBackAsync(TestContext.Current.CancellationToken);
        var orders = Assert.IsType<OrdersVm>(router.CurrentViewModel);
        Assert.Equal("open", orders.Status);
        Assert.Equal(["a", "b"], orders.Tags!);
    }

    [Fact]
    public async Task A_segment_goes_to_the_router_of_its_region()
    {
        using var provider = Build();
        var coordinator = provider.GetRequiredService<IRouteCoordinator>();

        var result = await coordinator.CoordinateAsync(
            "Orders/side:Help?topic=faq", TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.IsType<OrdersVm>(provider.GetRequiredService<IRouter>().CurrentViewModel);
        var side = provider.GetRegion("side");
        Assert.Equal("faq", Assert.IsType<SideVm>(side.CurrentViewModel).Topic);
    }

    [Fact]
    public async Task A_back_segment_pops_then_the_next_segment_is_pushed()
    {
        using var provider = Build();
        var coordinator = provider.GetRequiredService<IRouteCoordinator>();
        var router = provider.GetRequiredService<IRouter>();
        await coordinator.CoordinateAsync("Orders/Detail?id=1", TestContext.Current.CancellationToken);

        var result = await coordinator.CoordinateAsync("../Detail?id=2", TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.Equal(2, router.History.Entries.Count);
        Assert.Equal("2", Assert.IsType<DetailVm>(router.CurrentViewModel).Id);
    }

    [Fact]
    public async Task An_absolute_routing_clears_the_history_first()
    {
        using var provider = Build();
        var coordinator = provider.GetRequiredService<IRouteCoordinator>();
        var router = provider.GetRequiredService<IRouter>();
        await coordinator.CoordinateAsync("Orders/Detail?id=1", TestContext.Current.CancellationToken);

        var result = await coordinator.CoordinateAsync("/Detail?id=9", TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.Single(router.History.Entries);
        Assert.Equal("9", Assert.IsType<DetailVm>(router.CurrentViewModel).Id);
    }

    [Fact]
    public async Task Unknown_segments_and_regions_are_rejected_before_any_router_moves()
    {
        using var provider = Build();
        var coordinator = provider.GetRequiredService<IRouteCoordinator>();
        var router = provider.GetRequiredService<IRouter>();
        var raised = 0;
        coordinator.Coordinating += (_, _) => raised++;

        var unknownSegment = await coordinator.CoordinateAsync("Orders/Nope", TestContext.Current.CancellationToken);
        
        Assert.False(unknownSegment.Success);
        Assert.Contains("Nope", unknownSegment.Exception?.Message);
        Assert.Empty(router.History.Entries);
        Assert.Equal(0, raised);
    }

    [Fact]
    public async Task A_malformed_uri_fails_without_throwing()
    {
        using var provider = Build();
        var coordinator = provider.GetRequiredService<IRouteCoordinator>();

        var result = await coordinator.CoordinateAsync("side:", TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.NotNull(result.Exception);
    }

    [Fact]
    public async Task Back_with_nothing_to_go_back_to_stops_the_routing()
    {
        using var provider = Build();
        var coordinator = provider.GetRequiredService<IRouteCoordinator>();
        var router = provider.GetRequiredService<IRouter>();
        await coordinator.CoordinateAsync("Orders", TestContext.Current.CancellationToken);

        var result = await coordinator.CoordinateAsync("../Detail?id=1", TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.True(result.Cancelled);
        Assert.IsType<OrdersVm>(router.CurrentViewModel);
    }

    [Fact]
    public async Task Routing_and_Routed_are_raised_once_with_the_progress_made()
    {
        using var provider = Build();
        var coordinator = provider.GetRequiredService<IRouteCoordinator>();
        var log = new List<string>();
        RouteCoordinatedEventArgs? ended = null;
        coordinator.Coordinating += (_, e) => log.Add("routing " + e.Coordination);
        coordinator.Coordinated += (_, e) => { log.Add("routed"); ended = e; };

        await coordinator.CoordinateAsync("Orders/../Detail", TestContext.Current.CancellationToken);

        Assert.Equal(["routing Orders/../Detail", "routed"], log);
        Assert.NotNull(ended);
        Assert.False(ended!.Result.Success);          // '..' right after the first push has nothing behind it
        Assert.Equal(1, ended.CompletedSegments);
    }

    [Fact]
    public async Task A_throwing_observer_does_not_break_the_routing()
    {
        using var provider = Build();
        var coordinator = provider.GetRequiredService<IRouteCoordinator>();
        coordinator.Coordinating += (_, _) => throw new InvalidOperationException("boom");
        coordinator.Coordinated += (_, _) => throw new InvalidOperationException("boom");

        var result = await coordinator.CoordinateAsync("Orders", TestContext.Current.CancellationToken);

        Assert.True(result.Success);
    }

    [Fact]
    public async Task A_segment_without_a_registered_view_throws_and_still_raises_Routed()
    {
        using var provider = Build(s => s.RegisterSegment<OrphanVm>());
        var coordinator = provider.GetRequiredService<IRouteCoordinator>();
        RouteCoordinatedEventArgs? ended = null;
        coordinator.Coordinated += (_, e) => ended = e;

        var result = await coordinator.CoordinateAsync("Orders/OrphanVm", TestContext.Current.CancellationToken);
        Assert.IsType<InvalidOperationException>(result.Exception);
        Assert.NotNull(ended);
        Assert.False(ended!.Result.Success);
        Assert.Equal(1, ended.CompletedSegments);
    }

    private sealed class Sink : IRouteableErrorSink
    {
        private readonly List<(string Source, Exception Exception)> _all = [];

        // The test setup injects [Param] properties from the container, which reports its own errors: only the coordinator's count here.
        public List<(string Source, Exception Exception)> Reported => _all.Where(r => r.Source.StartsWith(nameof(RouteCoordinator) + ".")).ToList();
        public void Report(string source, Exception exception) => _all.Add((source, exception));
    }

    [Fact]
    public async Task A_rejected_coordination_is_reported_to_the_error_sink()
    {
        var sink = new Sink();
        using var provider = Build(s => s.AddSingleton<IRouteableErrorSink>(sink));
        var coordinator = provider.GetRequiredService<IRouteCoordinator>();

        var result = await coordinator.CoordinateAsync("Orders/Nope", TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        var reported = Assert.Single(sink.Reported);
        Assert.StartsWith(nameof(RouteCoordinator) + ".", reported.Source);
        Assert.Same(result.Exception, reported.Exception);
    }

    [Fact]
    public async Task A_segment_that_fails_is_reported_to_the_error_sink()
    {
        var sink = new Sink();
        using var provider = Build(s =>
        {
            s.AddSingleton<IRouteableErrorSink>(sink);
            s.RegisterSegment<OrphanVm>();
        });
        var coordinator = provider.GetRequiredService<IRouteCoordinator>();

        var result = await coordinator.CoordinateAsync("Orders/OrphanVm", TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Same(result.Exception, Assert.Single(sink.Reported).Exception);
    }

    [Fact]
    public async Task A_successful_coordination_reports_nothing()
    {
        var sink = new Sink();
        using var provider = Build(s => s.AddSingleton<IRouteableErrorSink>(sink));
        var coordinator = provider.GetRequiredService<IRouteCoordinator>();

        var result = await coordinator.CoordinateAsync("Orders", TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.Empty(sink.Reported);
    }

    public sealed class PositionalView(string title) : IRouteableFor
    {
        public string Title { get; } = title;
        public object? Context { get; set; }
    }

    public sealed class PositionalVm(int count)
    {
        public int Count { get; } = count;
        [Param] public string? Status { get; set; }
    }

    [Fact]
    public async Task Configure_fills_the_same_argument_bags_as_RouteableArgs()
    {
        using var provider = Build(s =>
        {
            s.RegisterRoute<PositionalView, PositionalVm>();
            s.RegisterSegment<PositionalVm>();
        });
        var coordinator = provider.GetRequiredService<IRouteCoordinator>();
        var router = provider.GetRequiredService<IRouter>();

        var result = await coordinator.CoordinateAsync(
            RouteCoordination.Create()
                .Segment<PositionalVm>(args => args
                    .ForViewModel(new RouteableArgument(7), new RouteableArgument("open", "status", true))   // positional ctor argument + named
                    .ForView(new RouteableArgument("Hello")))                                    // positional view argument
                .Build(),
            TestContext.Current.CancellationToken);

        Assert.True(result.Success, result.Exception?.Message);
        var vm = Assert.IsType<PositionalVm>(router.CurrentViewModel);
        Assert.Equal(7, vm.Count);
        Assert.Equal("open", vm.Status);
        Assert.Equal("Hello", Assert.IsType<PositionalView>(router.CurrentView).Title);
    }

    [Fact]
    public async Task Configure_completes_the_named_parameters()
    {
        using var provider = Build();
        var coordinator = provider.GetRequiredService<IRouteCoordinator>();
        var router = provider.GetRequiredService<IRouter>();

        var routing = RouteCoordination.Create()
            .Segment("Orders", args => args.ForViewModel(new RouteableArgument(new[] { "x" }, "tags", true)))
            .Build();
        var withNamed = routing with
        {
            Segments = [routing.Segments[0] with { Parameters = [new("status", "open")] }]
        };

        await coordinator.CoordinateAsync(withNamed, TestContext.Current.CancellationToken);

        var orders = Assert.IsType<OrdersVm>(router.CurrentViewModel);
        Assert.Equal("open", orders.Status);
        Assert.Equal(["x"], orders.Tags!);
    }

    [Fact]
    public void Configure_cannot_be_written_in_a_uri_and_is_validated()
    {
        var routing = RouteCoordination.Create().Segment("Orders", args => args.ForValues(new RouteableArgument(1))).Build();

        Assert.Equal("Orders", routing.ToString());
        Assert.Throws<ArgumentNullException>(() => RouteCoordination.Create().Segment("Orders", (Action<IRouteableArgs>)null!));
        Assert.Throws<ArgumentException>(() => RouteCoordination.Create().Segment(" ", _ => { }));
    }

    // ------------------------------------------------------------ registration

    [Fact]
    public void RegisterSegment_rejects_a_duplicate_name_case_insensitively()
    {
        var services = new ServiceCollection();
        services.RegisterSegment<OrdersVm>("Orders");

        Assert.Throws<InvalidOperationException>(() => services.RegisterSegment<DetailVm>("orders"));
    }

    [Fact]
    public async Task RegisterSegment_can_run_before_or_after_RegisterRouting()
    {
        var services = new ServiceCollection();
        services.RegisterSegment<OrdersVm>("Orders");
        services.RegisterRouting();
        services.AddRouteCoordinator();
        services.RegisterRoute<OrdersView, OrdersVm>();
        using var provider = services.BuildServiceProvider();

        var result = await provider.GetRequiredService<IRouteCoordinator>().CoordinateAsync("Orders", TestContext.Current.CancellationToken);

        Assert.True(result.Success);
    }

    [Fact]
    public void AddRouteCoordinator_validates_its_arguments_and_registers_once()
    {
        Assert.Throws<ArgumentNullException>(() => SegmentExtensions.AddRouteCoordinator(null!));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new ServiceCollection().AddRouteCoordinator(ServiceLifetime.Transient));

        var services = new ServiceCollection();
        services.RegisterRouting();
        services.AddRouteCoordinator();

        Assert.Equal(1, services.Count(d => d.ServiceType == typeof(IRouteCoordinator)));
    }
}