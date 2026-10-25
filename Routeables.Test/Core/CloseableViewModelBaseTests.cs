namespace Routeables.Tests.Core;

public class CloseableViewModelBaseTests
{
    [Fact]
    public void Commands_is_empty_by_default()
    {
        var sut = new TestCloseableViewModel();

        Assert.Empty(sut.Commands);
    }

    [Fact]
    public async Task CanClose_can_be_overridden_to_return_false()
    {
        var sut = new TestCloseableViewModel { CanCloseResult = false };

        Assert.False(await sut.CanCloseAsync());
    }

    [Fact]
    public async Task CanClose_returns_true_by_default()
    {
        var sut = new TestCloseableViewModel();

        Assert.True(await sut.CanCloseAsync());
    }

    [Fact]
    public async Task Close_raises_CloseRequested_with_the_supplied_value_and_this_instance_as_sender()
    {
        var sut = new TestCloseableViewModel();
        string? received = "unset";
        object? sender = null;
        sut.CloseRequested += (s, e) =>
        {
            sender = s;
            received = e;
        };

        await sut.CloseAsync("done");

        Assert.Same(sut, sender);
        Assert.Equal("done", received);
    }

    [Fact]
    public async Task Close_does_not_throw_when_there_are_no_subscribers()
    {
        var sut = new TestCloseableViewModel();

        await sut.CloseAsync("done");
    }

    [Fact]
    public void Title_returns_the_derived_class_value()
    {
        var sut = new TestCloseableViewModel();

        Assert.Equal("Test Title", sut.Title);
    }
}
