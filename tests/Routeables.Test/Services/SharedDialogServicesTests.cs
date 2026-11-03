using Routeables.Services;

namespace Routeables.Tests.Services;

public class SharedDialogServicesTests
{
    [Theory]
    [InlineData("YesNoCancel", new[] { "Yes", "No", "Cancel" })]
    [InlineData("Ok", new[] { "Ok" })]
    [InlineData("", new string[0])]
    [InlineData(null, new string[0])]
    public void Split_gives_one_word_per_capital(string? text, string[] expected)
    {
        Assert.Equal(expected, RouteableButtons.Split(text));
    }

    private sealed class FakeMessageBox(Func<string, int> choose) : RouteableDialogServiceBase
    {
        public List<(string Message, string Title, string Buttons)> Shown { get; } = [];

        public override Task<RouteableContentDialogResult> ShowContentDialogAsync(RouteableContentDialog @params)
        {
            throw new NotImplementedException();
        }

        public override Task<int> ShowMessageAsync(string message, string title, string buttonText)
        {
            Shown.Add((message, title, buttonText));
            return Task.FromResult(choose(buttonText));
        }

        public override Task<RouteableTaskDialogResult> ShowTaskDialogAsync(RouteableTaskDialog @params, params RouteableTaskDialogResult[] results)
        {
            throw new NotImplementedException();
        }
    }

    [Fact]
    public async Task Message_box_base_asks_for_the_right_buttons_and_reads_the_index()
    {
        var service = new FakeMessageBox(_ => 0);

        Assert.True(await service.ShowYesNoAsync("m", "t"));
        Assert.True(await service.ShowOkCancelAsync("m"));
        Assert.True(await service.ShowOkAbortAsync("m"));
        Assert.Equal(0, await service.ShowYesNoCancelAsync("m"));
        Assert.Equal(0, await service.ShowYesNoAbortAsync("m"));
        await service.ShowOkAsync("m");
        await service.ShowErrorAsync(new InvalidOperationException("boom"));

        Assert.Equal(["YesNo", "OkCancel", "OkAbort", "YesNoCancel", "YesNoAbort", "Ok", "Ok"], service.Shown.Select(s => s.Buttons));
        Assert.Equal("boom", service.Shown[^1].Message);
        Assert.Equal("Error", service.Shown[^1].Title);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(-1)]
    public async Task Message_box_base_treats_any_other_index_as_a_refusal(int index)
    {
        var service = new FakeMessageBox(_ => index);

        Assert.False(await service.ShowYesNoAsync("m"));
        Assert.False(await service.ShowOkCancelAsync("m"));
        Assert.False(await service.ShowOkAbortAsync("m"));
    }

    [Fact]
    public async Task Message_box_base_rejects_a_null_exception()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => new FakeMessageBox(_ => 0).ShowErrorAsync(null!));
    }
}
