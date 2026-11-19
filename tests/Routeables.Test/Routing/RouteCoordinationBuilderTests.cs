using Routeables.Abstraction.Routing;
using Routeables.Segments;
using Routeables.Tests.Pages;

namespace Routeables.Tests.Routing;

/// <summary>The edges of <see cref="RouteCoordination"/>, its builder, the event data and the string overload of the coordinator.</summary>
public class RouteCoordinationBuilderTests
{

    [Fact]
    public void A_segment_without_a_name_is_a_format_error_like_the_other_malformed_uris()
    {
        Assert.False(RouteCoordination.TryParse("side:", out _, out var error));
        Assert.IsType<FormatException>(error);
        Assert.Throws<FormatException>(() => RouteCoordination.Parse("side:"));
    }

    [Theory]
    [InlineData(".")]
    [InlineData("Orders/./Detail")]
    [InlineData("%2E%2E")]
    [InlineData("Orders/%2e%2e")]
    public void The_parser_refuses_the_names_that_the_builder_refuses(string uri)
    {
        Assert.False(RouteCoordination.TryParse(uri, out _, out var error));
        Assert.IsType<FormatException>(error);
    }

    [Fact]
    public void A_plain_way_back_is_still_read_as_a_way_back()
    {
        Assert.True(RouteCoordination.TryParse("Orders/../Detail?id=5", out var coordination, out _));
        Assert.Equal(RouteSegmentKind.Back, coordination.Segments[1].Kind);
    }

    [Theory]
    [InlineData("..")]
    [InlineData(".")]
    public void A_segment_cannot_be_named_like_the_way_back(string name)
    {
        Assert.Throws<ArgumentException>(() => RouteCoordination.Create().Segment(name));
        Assert.Throws<ArgumentException>(() => RouteCoordination.Create().Segment(name, _ => { }));
        Assert.Throws<ArgumentException>(() => new Microsoft.Extensions.DependencyInjection.ServiceCollection().RegisterSegment<OrdersViewModel>(name));
    }

    [Fact]
    public void The_records_refuse_what_the_builder_refuses()
    {
        Assert.Throws<ArgumentException>(() => new RouteSegment("..", null, RouteSegmentKind.Push));
        Assert.Throws<ArgumentException>(() => new RouteCoordination([]));
        Assert.Equal("..", new RouteSegment("..", null, RouteSegmentKind.Back).Name);   // the way back is a segment of its own
    }

    [Fact]
    public void A_null_uri_does_not_parse()
    {
        Assert.False(RouteCoordination.TryParse(null, out var coordination, out var error));
        Assert.Null(coordination);
        Assert.IsType<FormatException>(error);
    }

    [Fact]
    public void A_back_segment_can_name_a_region()
    {
        var coordination = RouteCoordination.Parse("side:..");

        var segment = Assert.Single(coordination.Segments);
        Assert.Equal(RouteSegmentKind.Back, segment.Kind);
        Assert.Equal("side", segment.Region);
        Assert.False(coordination.Absolute);
    }

    [Fact]
    public void The_builder_names_a_typed_segment_after_its_type_and_keeps_the_region()
    {
        var coordination = RouteCoordination.Create().Segment<OrdersViewModel>("side").Build();

        var segment = Assert.Single(coordination.Segments);
        Assert.Equal(nameof(OrdersViewModel), segment.Name);
        Assert.Equal("side", segment.Region);
        Assert.Equal(RouteSegmentKind.Push, segment.Kind);
    }

    [Fact]
    public void The_builder_takes_the_arguments_of_a_segment_from_a_delegate()
    {
        Action<IRouteableArgs> configure = args => args.WithValue("k", 1);

        var coordination = RouteCoordination.Create().Segment<OrdersViewModel>(configure, "side").Segment("Detail", configure).Build();

        Assert.Equal(["side", null], coordination.Segments.Select(s => s.Region));
        Assert.Equal([nameof(OrdersViewModel), "Detail"], coordination.Segments.Select(s => s.Name));
    }

    [Fact]
    public void The_builder_refuses_a_missing_delegate_or_a_blank_name()
    {
        var builder = RouteCoordination.Create();

        Assert.Throws<ArgumentNullException>(() => builder.Segment("Orders", (Action<IRouteableArgs>)null!));
        Assert.Throws<ArgumentException>(() => builder.Segment(" ", _ => { }));
    }

    [Fact]
    public void The_builder_remembers_Absolute_and_a_back_segment_of_a_region()
    {
        var coordination = RouteCoordination.Create().Absolute().Back("side").Build();

        Assert.True(coordination.Absolute);
        Assert.Equal("/side:..", coordination.ToString());
    }

    [Fact]
    public void The_event_data_carry_what_ran_and_how_far_it_got()
    {
        var coordination = RouteCoordination.Parse("Orders");
        var result = RouteableResult.Fail(new InvalidOperationException("stop"));

        var coordinating = new RouteCoordinatingEventArgs(coordination);
        var coordinated = new RouteCoordinatedEventArgs(coordination, result, 2);

        Assert.Same(coordination, coordinating.Coordination);
        Assert.Same(coordination, coordinated.Coordination);
        Assert.Same(result, coordinated.Result);
        Assert.Equal(2, coordinated.CompletedSegments);
    }

    [Fact]
    public async Task The_string_overload_refuses_a_missing_coordinator()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(async () => await RouteCoordinatorExtensions.CoordinateAsync(null!, "Orders", TestContext.Current.CancellationToken));
    }

    [Fact]
    public void A_plus_in_the_query_of_an_address_written_by_hand_is_a_space_and_an_escaped_plus_stays_a_plus()
    {
        var parsed = Routeables.Segments.RouteCoordination.Parse("orders?q=a+b&p=1%2B1");

        var parameters = Assert.Single(parsed.Segments).Parameters!;
        Assert.Equal("a b", parameters.Single(p => p.Key == "q").Value);
        Assert.Equal("1+1", parameters.Single(p => p.Key == "p").Value);
    }
}
