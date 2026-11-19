using Routeables.Routing;

namespace Routeables.Tests.Routing;

/// <summary>A route template is read once and kept: matching it again, from many threads, gives what matching it the first time gave.</summary>
public class RouteParserCacheTests
{
    [Fact]
    public void A_template_matches_the_same_way_every_time()
    {
        for (var i = 0; i < 3; i++)
        {
            var args = RouteableArgs.Create();

            Assert.True(RouteParser.TryMatch("orders/{id:int}/{tab?}?mode=grid&{page:int?}", "/orders/42/lines?mode=grid&page=3", args));
            Assert.False(RouteParser.TryMatch("orders/{id:int}/{tab?}?mode=grid&{page:int?}", "/orders/x", RouteableArgs.Create()));
            Assert.Contains("expected", RouteParser.Mismatch("orders/{id:int}", "/customers/42") ?? "");
        }
    }

    [Fact]
    public void The_most_specific_template_still_wins_when_the_templates_are_kept()
    {
        var entries = new (string Template, Type? ViewModelType)[] { ("bears/{id}", null), ("bears/new", null), ("bears/{id:int}", null), ("files/{**path}", null) };

        for (var i = 0; i < 3; i++)
        {
            Assert.Equal("bears/new", RouteParser.BestMatch(entries, "/bears/new").Template);
            Assert.Equal("bears/{id:int}", RouteParser.BestMatch(entries, "/bears/7").Template);
            Assert.Equal("bears/{id}", RouteParser.BestMatch(entries, "/bears/rex").Template);
            Assert.Equal("files/{**path}", RouteParser.BestMatch(entries, "/files/a/b/c").Template);
        }
    }

    [Fact]
    public void Many_threads_matching_the_same_new_template_all_get_the_right_answer()
    {
        var wrong = 0;

        Parallel.For(0, 2000, i =>
        {
            var template = "cache-" + (i % 7) + "/{id:int}/{rest?}";
            var args = RouteableArgs.Create();
            var ok = RouteParser.TryMatch(template, $"/cache-{i % 7}/{i}/x", args)
                     && !RouteParser.TryMatch(template, $"/cache-{i % 7}/not-a-number", RouteableArgs.Create());
            if (!ok)
                Interlocked.Increment(ref wrong);
        });

        Assert.Equal(0, wrong);
    }
}
