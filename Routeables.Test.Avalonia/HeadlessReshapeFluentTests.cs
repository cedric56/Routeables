using Avalonia.Controls;
using FluentAvalonia.UI.Windowing;
using Microsoft.Extensions.DependencyInjection;
using Reshapes.Avalonia.Fluent;
using Routeables.Avalonia.Services;
using Routeables.Services;
using Xunit;

namespace Routeables.Test.Avalonia;

/// <summary>The FluentAvalonia Reshape: how it registers itself, the windows it draws over, and the message boxes it refuses.</summary>
[Collection("Avalonia headless")]
public sealed class HeadlessReshapeFluentTests(HeadlessApp app)
{
    [Fact]
    public void UseFluentAvalonia_replaces_the_message_box_service_with_the_Fluent_one()
    {
        var services = new ServiceCollection();

        var returned = services.UseFluentAvalonia("avares://Test/icon.ico");

        Assert.Same(services, returned);
        using var provider = services.BuildServiceProvider();
        Assert.IsType<ReshapeMessageBoxService>(provider.GetRequiredService<IMessageBoxService>());
    }

    [Fact]
    public Task A_plain_window_is_displayable_but_a_FluentAvalonia_window_is_not() => app.RunAsync(() =>
    {
        var service = new ReshapeMessageBoxService();

        Assert.True(service.IsDisplayable(new Window()));
        Assert.False(service.IsDisplayable(new FAAppWindow()));
        return Task.CompletedTask;
    });

    [Theory]
    [InlineData("YesNoCancelRetry")]
    [InlineData("OkYesNoCancel")]
    public async Task A_message_box_of_more_than_three_buttons_is_refused(string buttons)
    {
        var service = new ReshapeMessageBoxService();

        var exception = await Assert.ThrowsAsync<ArgumentException>(() => service.ShowMessageBoxAsync("T", "M", buttons));

        Assert.Equal("buttons", exception.ParamName);
    }
}
