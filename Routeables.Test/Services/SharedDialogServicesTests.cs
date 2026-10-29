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

    //private sealed class FakeContentDialog(RouteableContentDialogResult result) : RouteableDialogServiceBase
    //{
    //    public List<RouteableContentDialog> Shown { get; } = [];

    //    public override Task<RouteableContentDialogResult> ShowAsync(RouteableContentDialog @params)
    //    {
    //        Shown.Add(@params);
    //        return Task.FromResult(result);
    //    }
    //}

    //[Fact]
    //public async Task Content_dialog_base_builds_the_buttons_of_each_confirmation()
    //{
    //    var service = new FakeContentDialog(RouteableContentDialogResult.Primary);

    //    Assert.True(await service.ShowYesNoAsync("m", "t"));
    //    var yesNo = service.Shown[0];
    //    Assert.Equal(("Yes", "No", "t", "m"), (yesNo.PrimaryButtonText, yesNo.CloseButtonText, yesNo.Title, (string?)yesNo.Content));

    //    Assert.True(await service.ShowOkCancelAsync("m"));
    //    Assert.Equal(("Ok", "Cancel"), (service.Shown[1].PrimaryButtonText, service.Shown[1].CloseButtonText));

    //    Assert.True(await service.ShowOkAbortAsync("m"));
    //    Assert.Equal(("Ok", "Abort"), (service.Shown[2].PrimaryButtonText, service.Shown[2].CloseButtonText));

    //    await service.ShowOkAsync("m");
    //    Assert.Null(service.Shown[3].PrimaryButtonText);
    //    Assert.Equal("Ok", service.Shown[3].CloseButtonText);
    //}

    //[Theory]
    //[InlineData(RouteableContentDialogResult.Primary, 0)]
    //[InlineData(RouteableContentDialogResult.Secondary, 1)]
    //[InlineData(RouteableContentDialogResult.None, 2)]
    //public async Task Content_dialog_base_maps_yes_no_and_the_way_out_to_0_1_2(RouteableContentDialogResult result, int expected)
    //{
    //    var service = new FakeContentDialog(result);

    //    Assert.Equal(expected, await service.ShowYesNoCancelAsync("m"));
    //    Assert.Equal(expected, await service.ShowYesNoAbortAsync("m"));
    //    Assert.Equal("Cancel", service.Shown[0].CloseButtonText);
    //    Assert.Equal("Abort", service.Shown[1].CloseButtonText);
    //}
}
