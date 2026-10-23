using Routeables.Abstraction;
using Routeables.Locking;

namespace Routeables.Tests.Abstraction;

public class RouteableBrokerServiceTests
{
    private sealed record Ping(string Topic = "ping") : RouteableMessage(Topic);

    [Fact]
    public void Publish_calls_the_subscribers_of_the_topic_only()
    {
        var broker = new RouteableBrokerService();
        var pings = 0;
        var others = 0;
        broker.Subscribe("ping", _ => pings++);
        broker.Subscribe("other", _ => others++);

        broker.Publish(new Ping());

        Assert.Equal(1, pings);
        Assert.Equal(0, others);
    }

    [Fact]
    public void Disposing_a_token_removes_that_subscription_and_no_other()
    {
        var broker = new RouteableBrokerService();
        var first = 0;
        var second = 0;
        var token = broker.Subscribe("ping", _ => first++);
        broker.Subscribe("ping", _ => second++);

        token.Dispose();
        token.Dispose();
        broker.Publish(new Ping());

        Assert.Equal(0, first);
        Assert.Equal(1, second);
    }

    [Fact]
    public void A_subscriber_disposed_while_a_message_is_on_its_way_is_not_called()
    {
        var broker = new RouteableBrokerService();
        IDisposable? later = null;
        var laterCalls = 0;
        broker.Subscribe("ping", _ => later!.Dispose());
        later = broker.Subscribe("ping", _ => laterCalls++);

        broker.Publish(new Ping());

        Assert.Equal(0, laterCalls);
    }

    [Fact]
    public void A_subscriber_can_subscribe_and_publish_from_its_callback()
    {
        var broker = new RouteableBrokerService();
        var inner = 0;
        broker.Subscribe("ping", _ =>
        {
            broker.Subscribe("pong", _ => inner++);
            broker.Publish(new Ping("pong"));
        });

        broker.Publish(new Ping());

        Assert.Equal(1, inner);
    }

    [Fact]
    public void A_subscriber_that_throws_does_not_stop_the_others()
    {
        var broker = new RouteableBrokerService();
        var reached = false;
        broker.Subscribe("ping", _ => throw new InvalidOperationException("first"));
        broker.Subscribe("ping", _ => reached = true);

        var error = Assert.Throws<InvalidOperationException>(() => broker.Publish(new Ping()));

        Assert.Equal("first", error.Message);
        Assert.True(reached);
    }

    [Fact]
    public void Several_subscribers_that_throw_are_thrown_together()
    {
        var broker = new RouteableBrokerService();
        broker.Subscribe("ping", _ => throw new InvalidOperationException("a"));
        broker.Subscribe("ping", _ => throw new InvalidOperationException("b"));

        var error = Assert.Throws<AggregateException>(() => broker.Publish(new Ping()));

        Assert.Equal(2, error.InnerExceptions.Count);
    }

    [Fact]
    public async Task Concurrent_subscriptions_and_publications_neither_lose_a_subscriber_nor_throw()
    {
        var broker = new RouteableBrokerService();
        var calls = 0;
        const int subscribers = 200;

        await Task.WhenAll(Enumerable.Range(0, subscribers).Select(_ => Task.Run(() =>
        {
            broker.Subscribe("ping", _ => Interlocked.Increment(ref calls));
            broker.Publish(new Ping());
        })));

        calls = 0;
        broker.Publish(new Ping());

        Assert.Equal(subscribers, calls);
    }

    [Fact]
    public void Subscribing_and_publishing_from_many_threads_loses_nothing_and_throws_nothing()
    {
        var broker = new RouteableBrokerService();
        var permanent = 0;
        broker.Subscribe("ping", _ => Interlocked.Increment(ref permanent));

        Parallel.For(0, 2000, i =>
        {
            if (i % 2 == 0)
            {
                // Subscribers that come and go while others publish.
                using var token = broker.Subscribe("ping", _ => { });
                broker.Publish(new Ping());
            }
            else
            {
                broker.Publish(new Ping());
            }
        });

        // Every publication reached the subscriber that stayed.
        Assert.Equal(2000, permanent);
    }
}
