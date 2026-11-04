using MudBlazor;
using NSubstitute;
using Reshapes.Razor.MudBlazor;
using Routeables.Abstraction;
using Routeables.Razor.Services;
using Routeables.Services;
using Extensions = Reshapes.Razor.MudBlazor.Extensions;

namespace Routeables.Tests.Razor;

public class ReshapeMudBlazorTests
{
    // The MudBlazor services are replaced by substitutes: only the Reshape's own work is tested.
    private static (ServiceProvider Provider, IDialogService Dialogs, ISnackbar Snackbar) Build()
    {
        var dialogs = Substitute.For<IDialogService>();
        var snackbar = Substitute.For<ISnackbar>();
        var services = new ServiceCollection();
        services.UseReshapeMudBlazor();
        services.AddSingleton(dialogs);
        services.AddSingleton(snackbar);
        return (services.BuildServiceProvider(), dialogs, snackbar);
    }

    private static object?[] ArgumentsOf(object substitute, string methodStart) =>
        substitute.ReceivedCalls().Single(c => c.GetMethodInfo().Name.StartsWith(methodStart, StringComparison.Ordinal)).GetArguments();

    [Fact]
    public void UseReshapeMudBlazor_registers_its_message_box_and_notification_services()
    {
        var (provider, _, _) = Build();
        using var _ = provider;

        Assert.IsType<ReshapeMessageBoxService>(provider.GetRequiredService<IMessageBoxService>());
        Assert.IsType<ReshapeNotificationService>(provider.GetRequiredService<IRouteableNotificationService>());
    }

    [Fact]
    public void UseReshapeMudBlazor_refuses_a_null_collection()
    {
        Assert.Throws<ArgumentNullException>(() => Extensions.UseReshapeMudBlazor(null!));
    }

    [Theory]
    [InlineData("Ok", new[] { "OK" })]
    [InlineData("YesNo", new[] { "Yes", "No" })]
    [InlineData("YesNoCancel", new[] { "Yes", "No", "Cancel" })]
    [InlineData("", new[] { "OK" })]
    [InlineData("OkCancel", new[] { "OK", "Cancel" })]
    public async Task A_message_box_draws_one_button_for_each_word(string buttons, string[] expected)
    {
        var dialogs = Substitute.For<IDialogService>();
        var service = new ReshapeMessageBoxService(dialogs);

        await service.ShowMessageAsync("Title", "Message", buttons);

        var strings = ArgumentsOf(dialogs, "ShowMessageBox").OfType<string>().ToList();
        Assert.Contains("Title", strings);
        foreach (var word in expected)
            Assert.Contains(word, strings);
    }

    [Theory]
    [InlineData("Ok", 0)]
    [InlineData("YesNo", 1)]
    [InlineData("YesNoCancel", 2)]
    [InlineData("", 0)]
    public async Task A_dismissed_message_box_answers_with_its_last_button(string buttons, int expected)
    {
        // A substitute answers a dialog nobody chose in (a null result): the box was dismissed.
        var service = new ReshapeMessageBoxService(Substitute.For<IDialogService>());

        Assert.Equal(expected, await service.ShowMessageAsync("T", "M", buttons));
    }

    [Fact]
    public async Task A_message_box_is_html_encoded_and_keeps_its_line_breaks()
    {
        var dialogs = Substitute.For<IDialogService>();
        var service = new ReshapeMessageBoxService(dialogs);

        await service.ShowMessageAsync("T", "<b>a</b>\r\nb\nc", "Ok");

        var markup = ArgumentsOf(dialogs, "ShowMessageBox").OfType<Microsoft.AspNetCore.Components.MarkupString>().Single();
        Assert.Equal("&lt;b&gt;a&lt;/b&gt;<br />b<br />c", markup.Value);
    }

    [Fact]
    public async Task A_modal_needs_its_own_component()
    {
        var service = new ReshapeMessageBoxService(Substitute.For<IDialogService>());

        await Assert.ThrowsAsync<NotSupportedException>(() => service.ShowModalAsync<ClosingViewModel, int>(new ClosingViewModel()));
    }

    private sealed class ClosingViewModel : ICloseableViewModel<int>
    {
        public string Title => "Modal";
        public System.Collections.ObjectModel.ObservableCollection<RouteableCommand> Commands { get; } = [];
        public System.Windows.Input.ICommand? CloseCommand => null;
        public event EventHandler<int>? CloseRequested { add { } remove { } }
        public Task CloseAsync(int value) => Task.CompletedTask;
    }

    [Theory]
    [InlineData(RouteableNotificationType.Information, Severity.Info)]
    [InlineData(RouteableNotificationType.Success, Severity.Success)]
    [InlineData(RouteableNotificationType.Warning, Severity.Warning)]
    [InlineData(RouteableNotificationType.Error, Severity.Error)]
    public void A_notification_is_a_snackbar_of_the_matching_severity(RouteableNotificationType type, Severity expected)
    {
        var snackbar = Substitute.For<ISnackbar>();
        var service = new ReshapeNotificationService(snackbar);

        service.Show("T", "M", type);

        Assert.Equal(expected, ArgumentsOf(snackbar, "Add").OfType<Severity>().Single());
    }

    [Fact]
    public void A_notification_title_and_message_are_html_encoded()
    {
        var snackbar = Substitute.For<ISnackbar>();
        var service = new ReshapeNotificationService(snackbar);

        service.Show("<i>t</i>", "<script>x</script>");

        var markup = ArgumentsOf(snackbar, "Add").OfType<Microsoft.AspNetCore.Components.MarkupString>().Single();
        Assert.Equal("<h5>&lt;i&gt;t&lt;/i&gt;</h5>&lt;script&gt;x&lt;/script&gt;", markup.Value);
    }

    [Fact]
    public void A_snackbar_that_cannot_be_added_does_not_break_the_caller()
    {
        var snackbar = Substitute.For<ISnackbar>();
        snackbar.When(s => s.Add(Arg.Any<Microsoft.AspNetCore.Components.MarkupString>(), Arg.Any<Severity>(), Arg.Any<Action<SnackbarOptions>>(), Arg.Any<string>()))
            .Do(_ => throw new InvalidOperationException("no snackbar"));
        var service = new ReshapeNotificationService(snackbar);

        var exception = Record.Exception(() => service.Show("T", "M"));

        Assert.Null(exception);
    }
}
