using Microsoft.Extensions.DependencyInjection;
using Reshapes.Avalonia.Fluent;
using Routeables.Avalonia.Services;
using Xunit;

namespace Routeables.Test.Avalonia;

/// <summary>
/// The FluentAvalonia Reshape: how it registers itself, and the message boxes it refuses
/// (IsDisplayable needs a window, which the headless platform of this version of Avalonia cannot create next to FluentAvalonia).
/// </summary>
[Collection("Avalonia headless")]
public sealed class HeadlessReshapeFluentTests
{
    /// <summary>UseFluentAvalonia replaces the message-box service with <see cref="ReshapeMessageBoxService"/> and returns the same collection.</summary>
    [Fact]
    public void UseFluentAvalonia_replaces_the_message_box_service_with_the_Fluent_one()
    {
        var services = new ServiceCollection();

        var returned = services.UseFluentAvalonia("avares://Test/icon.ico");

        Assert.Same(services, returned);
        using var provider = services.BuildServiceProvider();
        Assert.IsType<ReshapeMessageBoxService>(provider.GetRequiredService<IMessageBoxService>());
    }

    /// <summary>A message box whose button string implies more than three buttons is refused with an <see cref="ArgumentException"/> on the buttons parameter.</summary>
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