#if !ANDROID && !IOS && !MACCATALYST && !WINDOWS
using Routeables.Native;

namespace Routeables.Tests.Native;

/// <summary>What the plain net10.0 build of Routeables.Native does on any operating system: no native dialog (the caller draws its own), and the notification center as configured.</summary>
public class NativeDialogsAndCenterBasicsTests
{
    [Fact]
    public void The_plain_build_has_no_native_dialogs_and_is_not_a_platform_build()
    {
        Assert.False(NativeDialogs.IsSupported);
        Assert.False(SystemNotificationCenter.IsPlatformBuild);
        NativeDialogs.Initialize();        // nothing to start, and nothing thrown
    }

    [Fact]
    public async Task Dialogs_are_shown_at_once_and_their_result_is_given()
    {
        var result = await NativeDialogs.OneAtATimeAsync(() => Task.FromResult(42));

        Assert.Equal(42, result);
    }

    [Fact]
    public async Task A_dialog_without_something_to_show_is_refused()
        => await Assert.ThrowsAsync<ArgumentNullException>(() => NativeDialogs.OneAtATimeAsync<int>(null!));

    [Fact]
    public async Task The_three_button_dialog_shows_nothing_and_says_so()
    {
        var result = await NativeDialogs.DisplayThreeButtonsAsync("title", "message", "Yes", "No", "Cancel", 1, 2, 3);

        Assert.False(result.Shown);
        Assert.Equal(0, result.Value);
    }

    [Fact]
    public async Task The_sheet_shows_nothing_and_says_so()
    {
        var result = await NativeDialogs.DisplaySheetAsync("title", "message", ["One", "Two"], "Cancel");

        Assert.False(result.Shown);
        Assert.Equal(0, result.Value);
    }

    [Fact]
    public async Task The_sheet_refuses_choices_or_a_way_out_that_are_missing()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => NativeDialogs.DisplaySheetAsync("t", "m", null!, "Cancel"));
        await Assert.ThrowsAsync<ArgumentNullException>(() => NativeDialogs.DisplaySheetAsync("t", "m", ["One"], null!));
    }

    [Fact]
    public void The_notifications_are_supported_where_the_plain_build_has_a_center()
        => Assert.Equal(OperatingSystem.IsLinux() || OperatingSystem.IsWindows() || OperatingSystem.IsBrowser(), SystemNotificationCenter.IsSupported);

    [Fact]
    public async Task Without_options_no_notification_is_created_and_nothing_is_asked_of_the_platform()
    {
        var center = new SystemNotificationCenter();

        Assert.Null(center.Options);
        Assert.Null(await center.CreateNotificationAsync("category"));
        await center.InitializeAsync();
        Assert.Empty(center.ActiveNotifications());
    }

    [Fact]
    public void Configuring_keeps_the_options()
    {
        var center = new SystemNotificationCenter();
        var options = new AppNotificationOptions { ChannelName = "Mine", RequestPermissionOnInitialize = false };

        center.Configure(options);

        Assert.Same(options, center.Options);
    }

    [Fact]
    public async Task Initializing_without_the_request_of_permission_asks_nothing()
    {
        var center = new SystemNotificationCenter();
        center.Configure(new AppNotificationOptions { RequestPermissionOnInitialize = false });

        await center.InitializeAsync();

        Assert.Empty(center.ActiveNotifications());
    }

    [Fact]
    public void The_options_start_as_documented()
    {
        var options = new AppNotificationOptions();

        Assert.False(options.ClearOnAppClose);
        Assert.Equal("Open", options.DefaultActionText);
        Assert.Equal("Notifications", options.ChannelName);
        Assert.True(options.RequestPermissionOnInitialize);
        Assert.Equal("routeables-notifications-worker.js", options.ServiceWorkerUrl);
        Assert.Null(options.DefaultIcon);
    }

    [Fact]
    public void The_current_center_is_one_instance()
        => Assert.Same(SystemNotificationCenter.Current, SystemNotificationCenter.Current);
}
#endif
